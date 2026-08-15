using System.Collections.Generic;
using BattleSimulator.Core;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class AutonomousAISystem : IBattleSystem, ICadencedBattleSystem
    {
        private readonly List<int> nearbyIds = new List<int>(128);

        public int Order => 100;
        public float UpdatesPerSecond => 10f;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (!unit.Active || unit.IsDead || unit.EmbarkedInId > 0 || !unit.CombatCapable && unit.Role != UnitRole.Builder && unit.Role != UnitRole.SupplyCarrier) continue;
                UnitState enemy = FindVisibleEnemy(world, unit);
                if (enemy != null)
                {
                    bool newContact = unit.EnemyContactId != enemy.Id
                        || !unit.HasRecentEnemyContact(world.Time, SimulationConstants.ContactGraceSeconds);
                    unit.EnemyContactId = enemy.Id;
                    unit.LastEnemyContactTime = world.Time;
                    if (newContact)
                    {
                        world.Events.Publish(new BattleEvent(BattleEventType.EnemySpotted, world.Time, unit.Id, unit.OwnerId, enemy.Position, $"{unit.Name} spotted {enemy.Name}."));
                    }
                }

                PlayerState player = world.GetPlayer(unit.OwnerId);
                bool contact = unit.HasRecentEnemyContact(world.Time, SimulationConstants.ContactGraceSeconds);
                bool canWithdraw = player != null && player.Race != "Orks" && player.Race != "Necrons" && player.Race != "Tyranids";
                float withdrawalThreshold = unit.Role == UnitRole.Builder || unit.Role == UnitRole.SupplyCarrier ? 0.55f : 0.28f;
                if (contact && canWithdraw && unit.Condition < withdrawalThreshold)
                {
                    unit.Order = UnitOrder.Withdraw;
                    unit.TargetEntityId = 0;
                    unit.Destination = NearestSafeBuilding(world, unit)?.Position ?? unit.SpawnOrigin;
                    continue;
                }

                if (unit.Order == UnitOrder.Withdraw)
                {
                    if (Vector2.Distance(unit.Position, unit.Destination) <= 14f && !contact)
                    {
                        unit.Order = UnitOrder.Idle;
                    }
                    continue;
                }

                if (enemy != null && unit.CombatCapable)
                {
                    unit.Order = UnitOrder.Attack;
                    unit.TargetEntityId = enemy.Id;
                    unit.Destination = enemy.Position;
                    continue;
                }

                if (unit.Role == UnitRole.Builder || unit.Role == UnitRole.SupplyCarrier) continue;
                BattleEntityState rememberedTarget = world.GetEntity<BattleEntityState>(unit.TargetEntityId);
                if (rememberedTarget != null && rememberedTarget.Active && !world.AreAllies(unit.OwnerId, rememberedTarget.OwnerId))
                {
                    unit.Destination = rememberedTarget.Position;
                    unit.Order = UnitOrder.Attack;
                    continue;
                }

                SquadState squad = world.GetSquad(unit.SquadId);
                if (squad != null && squad.Objective != Vector2.zero)
                {
                    unit.Destination = squad.Objective;
                    unit.Order = squad.PrimaryRole == SquadPrimaryRole.Capture || squad.PrimaryRole == SquadPrimaryRole.Reconnaissance
                        ? UnitOrder.Capture : UnitOrder.Move;
                    continue;
                }

                TerritoryCellState territory = NearestUncontrolledTerritory(world, unit);
                if (territory != null)
                {
                    unit.Order = UnitOrder.Capture;
                    unit.Destination = territory.Center;
                }
            }
        }

        private UnitState FindVisibleEnemy(BattleWorld world, UnitState unit)
        {
            float scanRange = Mathf.Max(unit.VisionRange, unit.AuspexRange * 0.72f);
            world.Spatial.Query(unit.Position, scanRange, nearbyIds);
            UnitState best = null;
            float bestScore = float.NegativeInfinity;
            Vector2 facing = new Vector2(Mathf.Cos(unit.FacingRadians), Mathf.Sin(unit.FacingRadians));
            for (int i = 0; i < nearbyIds.Count; i++)
            {
                UnitState candidate = world.GetEntity<UnitState>(nearbyIds[i]);
                if (candidate == null || !candidate.Active || candidate.IsDead || world.AreAllies(unit.OwnerId, candidate.OwnerId)) continue;
                Vector2 offset = candidate.Position - unit.Position;
                float distance = offset.magnitude;
                if (distance > scanRange) continue;
                bool optical = distance <= unit.VisionRange && (offset.sqrMagnitude < 0.01f
                    || Vector2.Angle(facing, offset) <= unit.VisionArcDegrees * 0.5f);
                bool auspex = distance <= unit.AuspexRange && (!candidate.Camouflaged || distance <= unit.AuspexRange * 0.55f);
                if (!optical && !auspex) continue;
                // Threat first, but consciously finish enemies whose condition has collapsed.
                float finishBonus = candidate.Condition <= 0.28f ? 180f * (1f - candidate.Condition) : 0f;
                float threatBonus = candidate.Role == UnitRole.Commander ? 45f : candidate.Role == UnitRole.Medic ? 24f : candidate.Role == UnitRole.Vehicle ? 34f : 0f;
                float score = finishBonus + threatBonus - distance;
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private static BuildingState NearestSafeBuilding(BattleWorld world, UnitState unit)
        {
            BuildingState best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState building = world.Buildings[i];
                if (!building.Active || !building.Operational || !world.AreAllies(unit.OwnerId, building.OwnerId)) continue;
                float distance = Vector2.SqrMagnitude(building.Position - unit.Position);
                if (distance < bestDistance)
                {
                    best = building;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static TerritoryCellState NearestUncontrolledTerritory(BattleWorld world, UnitState unit)
        {
            TerritoryCellState best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < world.TerritoryCells.Count; i++)
            {
                TerritoryCellState cell = world.TerritoryCells[i];
                if (cell.OwnerId == unit.OwnerId) continue;
                float distance = Vector2.SqrMagnitude(cell.Center - unit.Position);
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
