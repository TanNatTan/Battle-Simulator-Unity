using BattleSimulator.Core;
using BattleSimulator.Data;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    /// <summary>Evaluates authored battle objectives while preserving the strict Phase-20 annihilation rule.</summary>
    public sealed class VictorySystem : IBattleSystem, ICadencedBattleSystem
    {
        private readonly BattleDataRepository data;
        public VictorySystem(BattleDataRepository data = null) { this.data = data ?? BattleDataRepository.Instance; }
        public int Order => 900;
        public float UpdatesPerSecond => 2f;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                if (!player.Defeated && IsAnnihilated(world, player)) player.Defeated = true;
                if (!player.Defeated && EvaluateObjective(world, player, step.DeltaTime))
                {
                    EndBattle(world, player.TeamId, $"{player.Name} completed {data.Objective(player.BattleObjective).Name}.");
                    return;
                }
            }

            int survivingTeams = 0, lastTeam = 0;
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                if (player.Defeated || TeamCounted(world, player.TeamId, p)) continue;
                survivingTeams++; lastTeam = player.TeamId;
            }
            if (world.Players.Count > 1 && survivingTeams <= 1) EndBattle(world, survivingTeams == 1 ? lastTeam : 0, survivingTeams == 1 ? $"Team {lastTeam} wins by annihilation." : "Mutual annihilation.");
        }

        private bool EvaluateObjective(BattleWorld world, PlayerState player, float dt)
        {
            BattleObjectiveDefinition objective = data.Objective(player.BattleObjective);
            float progress = Metric(world, player, objective);
            player.ObjectiveProgress = Mathf.Clamp01(progress);
            if (progress >= objective.Threshold) player.ObjectiveHoldSeconds += dt;
            else player.ObjectiveHoldSeconds = Mathf.Max(0f, player.ObjectiveHoldSeconds - dt * 0.5f);
            if (objective.Metric == "enemyElimination") return EnemyTeamsAnnihilated(world, player.TeamId);
            return progress >= objective.Threshold && (objective.HoldSeconds <= 0f || player.ObjectiveHoldSeconds >= objective.HoldSeconds);
        }

        private static float Metric(BattleWorld world, PlayerState player, BattleObjectiveDefinition objective)
        {
            switch (objective.Metric)
            {
                case "headquartersDestruction": case "strongholdAssault": return EnemyHeadquartersAlive(world, player.TeamId) ? 0f : 1f;
                case "territoryControl": return OwnedTerritoryRatio(world, player.Id);
                case "strategicPointControl": return OwnedNodeRatio(world, player.Id);
                case "resourceControl": return OwnedResourceRatio(world, player.Id);
                case "breakthroughProgress": return Breakthrough(world, player);
                case "defenseDuration": return HeadquartersAlive(world, player.Id) ? (float)(world.Time / Mathf.Max(1f, objective.DurationSeconds)) : 0f;
                case "survivalDuration": case "delayDuration": case "lastStandDuration": return (float)(world.Time / Mathf.Max(1f, objective.DurationSeconds));
                case "convoyEscort": return Mathf.Clamp01(player.ResourcesDelivered / 1000f);
                case "convoyInterdiction": return Mathf.Clamp01(player.UnitsKilled / 30f + player.BuildingsDestroyed / 10f);
                case "assassination": return EnemyCommandersAlive(world, player.TeamId) ? 0f : 1f;
                case "sabotageProgress": return Mathf.Clamp01(player.BuildingsDestroyed / 4f);
                case "relicRecovery": return Mathf.Clamp01(player.CapturedNodes / 2f);
                case "extractionProgress": case "evacuationProgress": return ExtractionProgress(world, player);
                default: return EnemyTeamsAnnihilated(world, player.TeamId) ? 1f : 0f;
            }
        }

        private static bool IsAnnihilated(BattleWorld world, PlayerState player)
        {
            bool combatForces = false, production = false, reinforcement = false, builders = false, alliedRescue = false;
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i]; if (!unit.Active || unit.IsDead) continue;
                if (unit.OwnerId == player.Id) { combatForces |= unit.CombatCapable; builders |= unit.Role == UnitRole.Builder || unit.Role == UnitRole.Engineer; }
                else if (unit.TeamId == player.TeamId) alliedRescue = true;
            }
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState b = world.Buildings[i]; if (!b.Active || !b.Operational || b.OwnerId != player.Id) continue;
                production |= b.BuildingType == BuildingType.Barracks || b.BuildingType == BuildingType.Workshop || b.BuildingType == BuildingType.AirSupport;
                reinforcement |= b.BuildingType == BuildingType.Headquarters || b.BuildingType == BuildingType.AirSupport;
            }
            return !combatForces && !production && !reinforcement && !builders && !alliedRescue;
        }

        private static bool EnemyTeamsAnnihilated(BattleWorld world, int teamId) { for (int i = 0; i < world.Players.Count; i++) if (world.Players[i].TeamId != teamId && !world.Players[i].Defeated) return false; return true; }
        private static bool HeadquartersAlive(BattleWorld world, int ownerId) { for (int i = 0; i < world.Buildings.Count; i++) { BuildingState b = world.Buildings[i]; if (b.Active && b.Operational && b.OwnerId == ownerId && b.BuildingType == BuildingType.Headquarters) return true; } return false; }
        private static bool EnemyHeadquartersAlive(BattleWorld world, int teamId) { for (int i = 0; i < world.Buildings.Count; i++) { BuildingState b = world.Buildings[i]; if (b.Active && b.Operational && b.BuildingType == BuildingType.Headquarters && b.TeamId != teamId) return true; } return false; }
        private static bool EnemyCommandersAlive(BattleWorld world, int teamId) { for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (u.Active && !u.IsDead && u.CombatCapable && u.Role == UnitRole.Commander && u.TeamId != teamId) return true; } return false; }
        private static float OwnedTerritoryRatio(BattleWorld world, int ownerId) { int owned = 0; for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId == ownerId) owned++; return world.TerritoryCells.Count == 0 ? 0f : (float)owned / world.TerritoryCells.Count; }
        private static float OwnedResourceRatio(BattleWorld world, int ownerId) { int owned = 0; for (int i = 0; i < world.ResourceZones.Count; i++) if (world.ResourceZones[i].OwnerId == ownerId) owned++; return world.ResourceZones.Count == 0 ? 0f : (float)owned / world.ResourceZones.Count; }
        private static float OwnedNodeRatio(BattleWorld world, int ownerId) { int owned = 0; for (int i = 0; i < world.EconomicNodes.Count; i++) if (world.EconomicNodes[i].OwnerId == ownerId) owned++; return world.EconomicNodes.Count == 0 ? 0f : (float)owned / world.EconomicNodes.Count; }
        private static float Breakthrough(BattleWorld world, PlayerState player) { for (int p = 0; p < world.Players.Count; p++) { PlayerState enemy = world.Players[p]; if (enemy.TeamId == player.TeamId) continue; for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (u.Active && !u.IsDead && u.OwnerId == player.Id && u.CombatCapable && Vector2.Distance(u.Position, enemy.SpawnOrigin) <= enemy.SpawnRadius) return 1f; } } return 0f; }
        private static float ExtractionProgress(BattleWorld world, PlayerState player) { int total = 0, safe = 0; for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (!u.Active || u.IsDead || u.OwnerId != player.Id || !u.CombatCapable) continue; total++; if (Vector2.Distance(u.Position, player.SpawnOrigin) <= player.SpawnRadius) safe++; } return total == 0 ? 0f : (float)safe / total; }
        private static bool TeamCounted(BattleWorld world, int teamId, int beforeIndex) { for (int i = 0; i < beforeIndex; i++) if (!world.Players[i].Defeated && world.Players[i].TeamId == teamId) return true; return false; }
        private static void EndBattle(BattleWorld world, int teamId, string message) { world.BattleEnded = true; world.WinningTeamId = teamId; world.Events.Publish(new BattleEvent(BattleEventType.BattleEnded, world.Time, playerId: teamId, message: message)); }
    }
}
