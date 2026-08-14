using BattleSimulator.Core;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class ConstructionProductionSystem : IBattleSystem
    {
        private double nextPlanningTime;
        public int Order => 600;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            ProgressConstructionAndRepairs(world, step.DeltaTime);
            RunProduction(world, step.DeltaTime);
            if (world.Time < nextPlanningTime) return;
            nextPlanningTime = world.Time + 2d;
            for (int i = 0; i < world.Players.Count; i++) PlanForPlayer(world, world.Players[i]);
        }

        private static void ProgressConstructionAndRepairs(BattleWorld world, float dt)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState builder = world.Units[i];
                if (!builder.Active || builder.IsDead || builder.Role != UnitRole.Builder && builder.Role != UnitRole.Engineer) continue;
                BuildingState assigned = world.GetEntity<BuildingState>(builder.AssignedBuildingId);
                if (assigned == null || !assigned.Active || assigned.OwnerId != builder.OwnerId)
                {
                    builder.AssignedBuildingId = 0;
                    assigned = FindUnclaimedConstruction(world, builder.OwnerId) ?? FindConstruction(world, builder.OwnerId) ?? FindUnclaimedRepair(world, builder.OwnerId);
                    if (assigned != null) builder.AssignedBuildingId = assigned.Id;
                }
                if (assigned == null) { builder.Order = UnitOrder.Idle; continue; }
                builder.Destination = assigned.Position;
                float distance = Vector2.Distance(builder.Position, assigned.Position);
                if (distance > assigned.Radius + 7f) { builder.Order = UnitOrder.Move; continue; }
                if (assigned.ConstructionProgress < 1f)
                {
                    builder.Order = UnitOrder.Build;
                    assigned.ConstructionProgress = Mathf.Min(1f, assigned.ConstructionProgress + dt * 0.16f);
                    assigned.HitPoints = assigned.MaximumHitPoints * assigned.ConstructionProgress;
                    if (assigned.ConstructionProgress >= 1f)
                    {
                        assigned.Operational = true;
                        builder.AssignedBuildingId = 0;
                        world.Events.Publish(new BattleEvent(BattleEventType.BuildingConstructed, world.Time, assigned.Id, assigned.OwnerId, assigned.Position, $"{assigned.Name} completed."));
                    }
                }
                else if (assigned.HitPoints < assigned.MaximumHitPoints)
                {
                    builder.Order = UnitOrder.Repair;
                    assigned.HitPoints = Mathf.Min(assigned.MaximumHitPoints, assigned.HitPoints + dt * 90f);
                    if (assigned.HitPoints >= assigned.MaximumHitPoints) builder.AssignedBuildingId = 0;
                }
            }
        }

        private static void RunProduction(BattleWorld world, float dt)
        {
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState building = world.Buildings[i];
                if (!building.Active || !building.Operational) continue;
                building.ProductionCooldown = Mathf.Max(0f, building.ProductionCooldown - dt);
                if (building.ProductionQueue.Count == 0 || building.ProductionCooldown > 0f) continue;
                PlayerState owner = world.GetPlayer(building.OwnerId);
                if (owner == null) continue;
                UnitRole role = building.ProductionQueue.Dequeue();
                Vector2 spawn = FindSpawnPoint(world, building, role);
                UnitState unit = world.AddEntity(BattleEntityFactory.CreateUnit(owner, role, spawn));
                building.ProductionCooldown = role == UnitRole.Vehicle ? 8f : role == UnitRole.Aircraft ? 10f : 2.2f;
                world.Events.Publish(new BattleEvent(BattleEventType.UnitCreated, world.Time, unit.Id, owner.Id, spawn, $"{unit.Name} deployed."));
            }
        }

        private static void PlanForPlayer(BattleWorld world, PlayerState player)
        {
            if (player.Defeated) return;
            int units = 0, builders = 0, carriers = 0, vehicles = 0;
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (!unit.Active || unit.IsDead || unit.OwnerId != player.Id) continue;
                units++;
                if (unit.Role == UnitRole.Builder) builders++;
                else if (unit.Role == UnitRole.SupplyCarrier) carriers++;
                else if (unit.Role == UnitRole.Vehicle) vehicles++;
            }
            int desiredBuilders = player.Race == "Orks" || player.Race == "Necrons" ? 6 : player.Faction == "Imperial Guard" ? 2 : 3;
            BuildingState headquarters = FindOperational(world, player.Id, BuildingType.Headquarters);
            if (headquarters != null)
            {
                if (builders < desiredBuilders && !QueueContains(headquarters, UnitRole.Builder)) headquarters.ProductionQueue.Enqueue(UnitRole.Builder);
                if (carriers < 2 && !QueueContains(headquarters, UnitRole.SupplyCarrier)) headquarters.ProductionQueue.Enqueue(UnitRole.SupplyCarrier);
            }

            BuildingType needed = NextBuilding(world, player.Id);
            if (needed != (BuildingType)(-1) && CountFoundations(world, player.Id) < Mathf.Max(1, builders))
            {
                Vector2 position = FindBuildingSite(world, player, needed);
                if (position.x >= 0f)
                {
                    BuildingState foundation = world.AddEntity(BattleEntityFactory.CreateBuilding(player, needed, position, 0.05f));
                    foundation.Name = BuildingName(player, needed);
                }
            }

            if (units >= 140) return;
            BuildingState barracks = FindOperational(world, player.Id, BuildingType.Barracks);
            if (barracks != null && barracks.ProductionQueue.Count < 2)
            {
                barracks.ProductionQueue.Enqueue(units % 10 == 0 ? UnitRole.Commander : units % 5 == 0 ? UnitRole.Scout : UnitRole.Trooper);
            }
            BuildingState workshop = FindOperational(world, player.Id, BuildingType.Workshop);
            if (workshop != null && vehicles < 20 && workshop.ProductionQueue.Count < 1) workshop.ProductionQueue.Enqueue(UnitRole.Vehicle);
            BuildingState air = FindOperational(world, player.Id, BuildingType.AirSupport);
            if (air != null && air.ProductionQueue.Count < 1 && units % 8 == 0) air.ProductionQueue.Enqueue(UnitRole.Aircraft);
        }

        private static BuildingType NextBuilding(BattleWorld world, int ownerId)
        {
            BuildingType[] progression = { BuildingType.Barracks, BuildingType.Warehouse, BuildingType.Generator, BuildingType.Workshop, BuildingType.Defense, BuildingType.Hospital, BuildingType.Research, BuildingType.ForwardOutpost, BuildingType.AirSupport, BuildingType.ResourceExtractor };
            for (int i = 0; i < progression.Length; i++) if (CountBuildings(world, ownerId, progression[i]) == 0) return progression[i];
            int defenses = CountBuildings(world, ownerId, BuildingType.Defense);
            int territories = 0;
            for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId == ownerId) territories++;
            if (defenses < 1 + territories / 5) return BuildingType.Defense;
            return (BuildingType)(-1);
        }

        private static Vector2 FindBuildingSite(BattleWorld world, PlayerState player, BuildingType type)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float angle = world.Random.Range(0f, Mathf.PI * 2f);
                float distance = world.Random.Range(35f, Mathf.Max(45f, player.SpawnRadius - 24f));
                Vector2 candidate = world.ClampToWorld(player.SpawnOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, 20f);
                bool clear = true;
                for (int b = 0; b < world.Buildings.Count; b++)
                {
                    BuildingState other = world.Buildings[b];
                    if (other.Active && Vector2.Distance(candidate, other.Position) < other.Radius + 30f) { clear = false; break; }
                }
                if (clear) return candidate;
            }
            return new Vector2(-1f, -1f);
        }

        private static Vector2 FindSpawnPoint(BattleWorld world, BuildingState building, UnitRole role)
        {
            float radius = building.Radius + (role == UnitRole.Vehicle ? 13f : 7f);
            float angle = world.Random.Range(0f, Mathf.PI * 2f);
            return world.ClampToWorld(building.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, 5f);
        }

        private static BuildingState FindConstruction(BattleWorld world, int ownerId)
        {
            for (int i = 0; i < world.Buildings.Count; i++) if (world.Buildings[i].Active && world.Buildings[i].OwnerId == ownerId && world.Buildings[i].ConstructionProgress < 1f) return world.Buildings[i];
            return null;
        }

        private static BuildingState FindUnclaimedConstruction(BattleWorld world, int ownerId)
        {
            for (int b = 0; b < world.Buildings.Count; b++)
            {
                BuildingState building = world.Buildings[b];
                if (!building.Active || building.OwnerId != ownerId || building.ConstructionProgress >= 1f) continue;
                bool claimed = false;
                for (int u = 0; u < world.Units.Count; u++)
                {
                    UnitState builder = world.Units[u];
                    if (builder.Active && !builder.IsDead && builder.AssignedBuildingId == building.Id) { claimed = true; break; }
                }
                if (!claimed) return building;
            }
            return null;
        }

        private static BuildingState FindUnclaimedRepair(BattleWorld world, int ownerId)
        {
            for (int b = 0; b < world.Buildings.Count; b++)
            {
                BuildingState building = world.Buildings[b];
                if (!building.Active || building.OwnerId != ownerId || building.ConstructionProgress < 1f || building.HitPoints >= building.MaximumHitPoints) continue;
                bool claimed = false;
                for (int u = 0; u < world.Units.Count; u++) if (world.Units[u].AssignedBuildingId == building.Id) { claimed = true; break; }
                if (!claimed) return building;
            }
            return null;
        }

        private static BuildingState FindOperational(BattleWorld world, int ownerId, BuildingType type)
        {
            for (int i = 0; i < world.Buildings.Count; i++) { BuildingState b = world.Buildings[i]; if (b.Active && b.Operational && b.OwnerId == ownerId && b.BuildingType == type) return b; }
            return null;
        }

        private static int CountBuildings(BattleWorld world, int ownerId, BuildingType type)
        {
            int count = 0; for (int i = 0; i < world.Buildings.Count; i++) if (world.Buildings[i].Active && world.Buildings[i].OwnerId == ownerId && world.Buildings[i].BuildingType == type) count++; return count;
        }

        private static int CountFoundations(BattleWorld world, int ownerId)
        {
            int count = 0; for (int i = 0; i < world.Buildings.Count; i++) if (world.Buildings[i].Active && world.Buildings[i].OwnerId == ownerId && world.Buildings[i].ConstructionProgress < 1f) count++; return count;
        }

        private static bool QueueContains(BuildingState building, UnitRole role)
        {
            foreach (UnitRole queued in building.ProductionQueue) if (queued == role) return true;
            return false;
        }

        private static string BuildingName(PlayerState player, BuildingType type)
        {
            if (type == BuildingType.Headquarters) return player.Race == "Orks" ? "Big Hut" : player.Faction == "Space Marines" ? "Fortress Monastery" : "Headquarters";
            if (type == BuildingType.Barracks) return player.Race == "Orks" ? "Boyz Hut" : player.Faction == "Space Marines" ? "Chapel Barracks" : "Barracks";
            return type.ToString();
        }
    }
}
