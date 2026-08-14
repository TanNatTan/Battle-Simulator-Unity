using BattleSimulator.Core;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class VictorySystem : IBattleSystem
    {
        public int Order => 900;
        public void Tick(BattleWorld world, SimulationStep step)
        {
            int survivingTeams = 0;
            int lastTeam = 0;
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                if (!player.Defeated && IsDefeated(world, player)) player.Defeated = true;
            }
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                if (player.Defeated || TeamCounted(world, player.TeamId, p)) continue;
                survivingTeams++;
                lastTeam = player.TeamId;
            }
            if (world.Players.Count > 1 && survivingTeams <= 1)
            {
                world.BattleEnded = true;
                world.WinningTeamId = survivingTeams == 1 ? lastTeam : 0;
                world.Events.Publish(new BattleEvent(BattleEventType.BattleEnded, world.Time, playerId: lastTeam, message: survivingTeams == 1 ? $"Team {lastTeam} wins." : "Mutual annihilation."));
            }
        }

        private static bool IsDefeated(BattleWorld world, PlayerState player)
        {
            bool combatForces = false, production = false, reinforcement = false, builders = false, alliedRescue = false;
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (!unit.Active || unit.IsDead) continue;
                if (unit.OwnerId == player.Id)
                {
                    combatForces |= unit.CombatCapable;
                    builders |= unit.Role == UnitRole.Builder || unit.Role == UnitRole.Engineer;
                }
                else if (unit.TeamId == player.TeamId) alliedRescue = true;
            }
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState building = world.Buildings[i];
                if (!building.Active || !building.Operational || building.OwnerId != player.Id) continue;
                production |= building.BuildingType == BuildingType.Barracks || building.BuildingType == BuildingType.Workshop || building.BuildingType == BuildingType.AirSupport;
                reinforcement |= building.BuildingType == BuildingType.Headquarters || building.BuildingType == BuildingType.AirSupport;
            }
            return !combatForces && !production && !reinforcement && !builders && !alliedRescue;
        }

        private static bool TeamCounted(BattleWorld world, int teamId, int beforeIndex)
        {
            for (int i = 0; i < beforeIndex; i++) if (!world.Players[i].Defeated && world.Players[i].TeamId == teamId) return true;
            return false;
        }
    }
}
