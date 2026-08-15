using System;
using BattleSimulator.Core;
using BattleSimulator.Data;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    /// <summary>Plans the base first, then creates the bounded workforce and continuous force queues needed to execute it.</summary>
    public sealed class ConstructionProductionSystem : IBattleSystem, ICadencedBattleSystem
    {
        private readonly BattleDataRepository data;
        private double nextPlanningTime;
        public ConstructionProductionSystem(BattleDataRepository data = null) { this.data = data ?? BattleDataRepository.Instance; }
        public int Order => 600;
        public float UpdatesPerSecond => 10f;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            ProgressConstructionAndRepairs(world, step.DeltaTime);
            RunProduction(world, step.DeltaTime);
            if (world.Time < nextPlanningTime) return;
            nextPlanningTime = world.Time + 1.5d;
            for (int i = 0; i < world.Players.Count; i++) PlanForPlayer(world, world.Players[i]);
        }

        private static void ProgressConstructionAndRepairs(BattleWorld world, float dt)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState builder = world.Units[i];
                if (!builder.Active || builder.IsDead || builder.Role != UnitRole.Builder && builder.Role != UnitRole.Engineer) continue;
                PlayerState player = world.GetPlayer(builder.OwnerId);
                BuildingState assigned = world.GetEntity<BuildingState>(builder.AssignedBuildingId);
                if (assigned == null || !assigned.Active || assigned.OwnerId != builder.OwnerId)
                {
                    builder.AssignedBuildingId = 0;
                    assigned = FindUnclaimedConstruction(world, builder.OwnerId) ?? FindConstruction(world, builder.OwnerId);
                    if (assigned == null && CountRepairers(world, builder.OwnerId) == 0) assigned = FindUnclaimedRepair(world, builder.OwnerId);
                    if (assigned != null) builder.AssignedBuildingId = assigned.Id;
                }
                if (assigned == null) { builder.Order = UnitOrder.Idle; KeepBuilderInZone(builder, player); continue; }
                if (assigned.OwnerId != builder.OwnerId) { builder.AssignedBuildingId = 0; continue; }
                builder.Destination = assigned.Position;
                if (Vector2.Distance(builder.Position, assigned.Position) > assigned.Radius + 8f) { builder.Order = UnitOrder.Move; continue; }
                if (assigned.ConstructionProgress < 1f)
                {
                    builder.Order = UnitOrder.Build;
                    assigned.ConstructionProgress = Mathf.Min(1f, assigned.ConstructionProgress + dt * 0.24f);
                    assigned.HitPoints = assigned.MaximumHitPoints * assigned.ConstructionProgress;
                    if (assigned.ConstructionProgress >= 1f)
                    {
                        assigned.Operational = true; builder.AssignedBuildingId = 0;
                        world.Events.Publish(new BattleEvent(BattleEventType.BuildingConstructed, world.Time, assigned.Id, assigned.OwnerId, assigned.Position, $"{assigned.Name} completed."));
                    }
                }
                else if (assigned.HitPoints < assigned.MaximumHitPoints)
                {
                    builder.Order = UnitOrder.Repair;
                    assigned.HitPoints = Mathf.Min(assigned.MaximumHitPoints, assigned.HitPoints + dt * 180f);
                    if (assigned.HitPoints >= assigned.MaximumHitPoints) builder.AssignedBuildingId = 0;
                }
                else builder.AssignedBuildingId = 0;
            }
        }

        private void RunProduction(BattleWorld world, float dt)
        {
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState building = world.Buildings[i];
                if (!building.Active || !building.Operational) continue;
                building.ProductionCooldown = Mathf.Max(0f, building.ProductionCooldown - dt);
                if (building.ProductionCooldown > 0f) continue;
                PlayerState owner = world.GetPlayer(building.OwnerId);
                if (owner == null) continue;
                ProductionOrder order = building.DetailedProductionQueue.Count > 0 ? building.DetailedProductionQueue.Dequeue() : LegacyOrder(building);
                if (order == null) continue;
                float cost = order.ProducerRole == "GeneSeed" ? 0f : order.Role == UnitRole.Vehicle || order.Role == UnitRole.Aircraft ? 85f : order.Role == UnitRole.Builder ? 18f : 26f;
                ResourceType primary = owner.Faction == "Orks" ? ResourceType.Scrap : owner.Faction == "Tyranids" ? ResourceType.Biomass : ResourceType.Requisition;
                if (owner.Resource(primary) < cost) { building.DetailedProductionQueue.Enqueue(order); building.ProductionCooldown = 1f; continue; }
                owner.AddResource(primary, -cost);
                Vector2 spawn = FindSpawnPoint(world, building, order.Role);
                UnitState unit = world.AddEntity(BattleEntityFactory.CreateUnit(owner, order.Role, spawn, order.Specialty, data));
                AttachProducedUnit(world, owner, unit, order);
                building.ProductionCooldown = order.Role == UnitRole.Vehicle || order.Role == UnitRole.Aircraft ? 7f : order.Role == UnitRole.Builder ? 2.7f : owner.Faction == "Space Marines" ? 1.45f : 1.1f;
                world.Events.Publish(new BattleEvent(BattleEventType.UnitCreated, world.Time, unit.Id, owner.Id, spawn, $"{unit.Name} deployed."));
            }
        }

        private void PlanForPlayer(BattleWorld world, PlayerState player)
        {
            if (player.Defeated) return;
            int active = CountUnits(world, player.Id, null), builders = CountUnits(world, player.Id, UnitRole.Builder), carriers = CountUnits(world, player.Id, UnitRole.SupplyCarrier);
            BuilderPolicyDefinition policy = data.BuilderPolicy(player.Faction);
            ProductionPlanDefinition plan = data.ProductionPlan(player.Subfaction);
            FactionDefinition faction = FactionCatalog.For(player.Faction);

            int desiredParallel = Mathf.Clamp(1 + CountBuildings(world, player.Id) / 5, 1, Mathf.Max(1, policy.ConstructionReserve));
            while (CountFoundations(world, player.Id) < desiredParallel)
            {
                string role = NextBuildingRole(world, player, plan);
                if (string.IsNullOrEmpty(role) || !CanAffordBuilding(player, role)) break;
                Vector2 position = FindBuildingSite(world, player, role);
                if (position.x < 0f) break;
                SpendBuildingCost(player, role);
                BuildingState foundation = world.AddEntity(BattleEntityFactory.CreateBuilding(player, BattleEntityFactory.BuildingTypeForRole(role), position, 0.04f, role));
                foundation.Name = faction.BuildingLabel(role); foundation.DisplayName = foundation.Name;
                TerritoryCellState cell = CellAt(world, position); foundation.TerritoryCellId = cell?.Id ?? 0;
                player.ConstructionSequence++;
            }

            int targetBuilders = Mathf.Clamp(Mathf.Max(policy.StartingMinimum, CountFoundations(world, player.Id) + policy.RepairReserve + policy.GatherReserve), policy.StartingMinimum, policy.HardCap);
            BuildingState headquarters = FindOperational(world, player.Id, "HQ");
            if (headquarters != null)
            {
                if (player.Faction == "Space Marines" && player.GeneSeedStock > 0 && headquarters.DetailedProductionQueue.Count < 8 && world.Random.Range(0f, 1f) <= 0.35f)
                {
                    Queue(headquarters, UnitRole.Trooper, "Tactical Marine", "GeneSeed"); player.GeneSeedStock--;
                }
                while (builders + CountQueued(headquarters, UnitRole.Builder) < targetBuilders && headquarters.DetailedProductionQueue.Count < 8)
                    Queue(headquarters, UnitRole.Builder, Cycle(faction.RosterFor("builder"), builders), "HQ");
                while (carriers + CountQueued(headquarters, UnitRole.SupplyCarrier) < 2 && headquarters.DetailedProductionQueue.Count < 8)
                    Queue(headquarters, UnitRole.SupplyCarrier, Cycle(faction.RosterFor("supply"), carriers), "HQ");
            }
            if (active < player.ForceCap) QueueContinuousForces(world, player, faction);
        }

        private static void QueueContinuousForces(BattleWorld world, PlayerState player, FactionDefinition faction)
        {
            BuildingState muster = FindOperational(world, player.Id, "Muster");
            if (muster != null)
            {
                int wave = player.Faction == "Space Marines" ? 4 : 5;
                while (muster.DetailedProductionQueue.Count < wave)
                {
                    int sequence = player.ProductionSequence++; string specialty; UnitRole role;
                    if (sequence % 17 == 0) { specialty = Cycle(faction.RosterFor("commander"), sequence / 17); role = UnitRole.Commander; }
                    else if (sequence % 13 == 0) { specialty = Cycle(faction.RosterFor("medic"), sequence / 13); role = UnitRole.Medic; }
                    else if (sequence % 9 == 0) { specialty = Cycle(faction.RosterFor("standard"), sequence / 9); role = UnitRole.Trooper; }
                    else if (sequence % 6 == 0) { specialty = Cycle(faction.RosterFor("scout"), sequence / 6); role = UnitRole.Scout; }
                    else { specialty = Cycle(faction.RosterFor("trooper"), sequence); role = UnitRole.Trooper; }
                    Queue(muster, role, specialty, "Muster");
                }
            }
            BuildingState forge = FindOperational(world, player.Id, "War Forge");
            if (forge != null && forge.DetailedProductionQueue.Count < 2)
            {
                string vehicle = Cycle(faction.RosterFor("vehicle"), player.ProductionSequence++);
                Queue(forge, IsAircraft(vehicle) ? UnitRole.Aircraft : UnitRole.Vehicle, vehicle, "War Forge");
            }
            BuildingState deployment = FindOperational(world, player.Id, "Deployment");
            if (deployment != null && deployment.DetailedProductionQueue.Count < 1)
            {
                string[] vehicles = faction.RosterFor("vehicle");
                for (int n = 0; n < vehicles.Length; n++) { string candidate = vehicles[(player.ProductionSequence + n) % vehicles.Length]; if (IsAircraft(candidate)) { Queue(deployment, UnitRole.Aircraft, candidate, "Deployment"); player.ProductionSequence++; break; } }
            }
        }

        private static void AttachProducedUnit(BattleWorld world, PlayerState owner, UnitState unit, ProductionOrder order)
        {
            if (unit.Role == UnitRole.Builder || unit.Role == UnitRole.SupplyCarrier || unit.Role == UnitRole.Vehicle || unit.Role == UnitRole.Aircraft) return;
            SquadState target = order.TargetSquadId > 0 ? world.GetSquad(order.TargetSquadId) : null;
            for (int i = 0; target == null && i < world.Squads.Count; i++) if (world.Squads[i].OwnerId == owner.Id && world.Squads[i].MemberIds.Count < world.Squads[i].NominalSize) target = world.Squads[i];
            if (target == null) target = world.AddSquad(new SquadState { OwnerId = owner.Id, TeamId = owner.TeamId, Name = $"{owner.Subfaction} Squad {world.Squads.Count + 1}", NominalSize = owner.Faction == "Space Marines" && (unit.Specialty ?? string.Empty).IndexOf("Terminator", StringComparison.OrdinalIgnoreCase) >= 0 ? 5 : owner.Faction == "Space Marines" ? 10 : 8 });
            if (unit.Role == UnitRole.Commander) { unit.AttachedSquadId = target.Id; target.AttachedCharacterIds.Add(unit.Id); }
            else { unit.SquadId = target.Id; target.MemberIds.Add(unit.Id); }
        }

        private static string NextBuildingRole(BattleWorld world, PlayerState player, ProductionPlanDefinition plan)
        {
            string[] fallback = { "HQ", "Power", "Logistics", "Muster", "Industry", "Intel", "War Forge", "Sustainment", "Doctrine", "Deployment", "Fortification", "Emplacement", "Signature" };
            System.Collections.Generic.IList<string> order = plan != null && plan.BuildingOrder.Count > 0 ? plan.BuildingOrder : fallback;
            if (player.Faction == "Orks" && CountBuildings(world, player.Id) >= 3 && CountBuildings(world, player.Id, "Signature") == 0) return "Signature";
            for (int i = 0; i < order.Count; i++)
            {
                string role = order[i];
                if (role == "Intel" && player.EnemyBaseObservedAt + 180d < world.Time) continue;
                if (CountBuildings(world, player.Id, role) == 0) return role;
            }
            int territories = 0; for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId == player.Id) territories++;
            if (territories >= 5 && CountBuildings(world, player.Id, "Fortification") + CountBuildings(world, player.Id, "Emplacement") < territories / 5 + 1) return player.ConstructionSequence % 2 == 0 ? "Fortification" : "Emplacement";
            string best = null; int smallest = int.MaxValue;
            for (int i = 1; i < order.Count; i++)
            {
                string role = order[i]; int count = CountBuildings(world, player.Id, role);
                int cap = role == "Intel" ? 4 : role == "Muster" || role == "War Forge" || role == "Deployment" ? 5 : role == "Fortification" || role == "Emplacement" ? 999 : 8;
                if (count < cap && count < smallest) { smallest = count; best = role; }
            }
            return best;
        }

        private static Vector2 FindBuildingSite(BattleWorld world, PlayerState player, string role)
        {
            for (int attempt = 0; attempt < 48; attempt++)
            {
                TerritoryCellState territory = role == "Intel" && player.EnemyBaseObservedAt + 180d >= world.Time ? TerritoryNearest(world, player, Vector2.Lerp(player.SpawnOrigin, player.LastKnownEnemyBase, 0.5f)) : OwnedTerritory(world, player, attempt);
                bool expansion = territory != null && (role == "Fortification" || role == "Emplacement" || role == "Industry" || role == "Intel");
                Vector2 center = expansion ? territory.Center : player.SpawnOrigin;
                float range = expansion ? Mathf.Min(territory.Size.x, territory.Size.y) * 0.3f : player.SpawnRadius * 0.76f;
                float angle = world.Random.Range(0f, Mathf.PI * 2f), distance = world.Random.Range(28f, Mathf.Max(35f, range));
                Vector2 candidate = world.ClampToWorld(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, 20f);
                if (expansion && !territory.Contains(candidate)) continue;
                bool clear = true; for (int b = 0; b < world.Buildings.Count; b++) { BuildingState other = world.Buildings[b]; if (other.Active && Vector2.Distance(candidate, other.Position) < other.Radius + 19f) { clear = false; break; } }
                if (clear) return candidate;
            }
            return new Vector2(-1f, -1f);
        }

        private static TerritoryCellState OwnedTerritory(BattleWorld world, PlayerState player, int salt) { int count = 0; for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId == player.Id) count++; if (count == 0) return null; int wanted = (player.ConstructionSequence + salt) % count; for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId == player.Id && wanted-- == 0) return world.TerritoryCells[i]; return null; }
        private static TerritoryCellState TerritoryNearest(BattleWorld world, PlayerState player, Vector2 point) { TerritoryCellState best = null; float distance = float.PositiveInfinity; for (int i = 0; i < world.TerritoryCells.Count; i++) { TerritoryCellState cell = world.TerritoryCells[i]; if (cell.OwnerId != player.Id) continue; float candidate = Vector2.SqrMagnitude(cell.Center - point); if (candidate < distance) { distance = candidate; best = cell; } } return best; }
        private static TerritoryCellState CellAt(BattleWorld world, Vector2 point) { for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].Contains(point)) return world.TerritoryCells[i]; return null; }
        private static bool CanAffordBuilding(PlayerState p, string role) { ResourceType resource = p.Faction == "Orks" ? ResourceType.Scrap : p.Faction == "Tyranids" ? ResourceType.Biomass : ResourceType.Materials; return p.Resource(resource) >= (role == "Fortification" || role == "Emplacement" ? 110f : 150f); }
        private static void SpendBuildingCost(PlayerState p, string role) { ResourceType resource = p.Faction == "Orks" ? ResourceType.Scrap : p.Faction == "Tyranids" ? ResourceType.Biomass : ResourceType.Materials; p.AddResource(resource, -(role == "Fortification" || role == "Emplacement" ? 110f : 150f)); }
        private static void Queue(BuildingState b, UnitRole role, string specialty, string producer) { if (string.IsNullOrEmpty(specialty)) specialty = role.ToString(); b.DetailedProductionQueue.Enqueue(new ProductionOrder { Role = role, Specialty = specialty, ProducerRole = producer, Priority = 1f }); }
        private static ProductionOrder LegacyOrder(BuildingState b) { return b.ProductionQueue.Count == 0 ? null : new ProductionOrder { Role = b.ProductionQueue.Dequeue(), ProducerRole = b.OperationalRole }; }
        private static string Cycle(string[] values, int index) { return values.Length == 0 ? string.Empty : values[Math.Abs(index) % values.Length]; }
        private static bool IsAircraft(string value) { string s = (value ?? string.Empty).ToLowerInvariant(); return s.Contains("thunderhawk") || s.Contains("stormraven") || s.Contains("stormtalon") || s.Contains("stormhawk") || s.Contains("dakkajet") || s.Contains("heldrake") || s.Contains("scythe") || s.Contains("barracuda"); }
        private static Vector2 FindSpawnPoint(BattleWorld world, BuildingState building, UnitRole role) { float radius = building.Radius + (role == UnitRole.Vehicle || role == UnitRole.Aircraft ? 16f : 8f); float angle = world.Random.Range(0f, Mathf.PI * 2f); return world.ClampToWorld(building.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, 5f); }
        private static void KeepBuilderInZone(UnitState builder, PlayerState player) { if (player != null && Vector2.Distance(builder.Position, player.SpawnOrigin) > player.SpawnRadius) { builder.Destination = player.SpawnOrigin + (builder.Position - player.SpawnOrigin).normalized * player.SpawnRadius * 0.72f; builder.Order = UnitOrder.Move; } }
        private static int CountRepairers(BattleWorld world, int ownerId) { int count = 0; for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; BuildingState b = world.GetEntity<BuildingState>(u.AssignedBuildingId); if (u.Active && !u.IsDead && u.OwnerId == ownerId && b != null && b.ConstructionProgress >= 1f && b.HitPoints < b.MaximumHitPoints) count++; } return count; }
        private static int CountUnits(BattleWorld world, int ownerId, UnitRole? role) { int count = 0; for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (u.Active && !u.IsDead && u.OwnerId == ownerId && (!role.HasValue || u.Role == role.Value)) count++; } return count; }
        private static int CountQueued(BuildingState building, UnitRole role) { int count = 0; foreach (ProductionOrder order in building.DetailedProductionQueue) if (order.Role == role) count++; return count; }
        private static BuildingState FindConstruction(BattleWorld world, int ownerId) { for (int i = 0; i < world.Buildings.Count; i++) { BuildingState b = world.Buildings[i]; if (b.Active && b.OwnerId == ownerId && b.ConstructionProgress < 1f) return b; } return null; }
        private static BuildingState FindUnclaimedConstruction(BattleWorld world, int ownerId) { for (int b = 0; b < world.Buildings.Count; b++) { BuildingState building = world.Buildings[b]; if (!building.Active || building.OwnerId != ownerId || building.ConstructionProgress >= 1f) continue; bool claimed = false; for (int u = 0; u < world.Units.Count; u++) if (world.Units[u].Active && world.Units[u].AssignedBuildingId == building.Id) { claimed = true; break; } if (!claimed) return building; } return null; }
        private static BuildingState FindUnclaimedRepair(BattleWorld world, int ownerId) { for (int b = 0; b < world.Buildings.Count; b++) { BuildingState building = world.Buildings[b]; if (!building.Active || building.OwnerId != ownerId || building.ConstructionProgress < 1f || building.HitPoints >= building.MaximumHitPoints) continue; bool claimed = false; for (int u = 0; u < world.Units.Count; u++) if (world.Units[u].AssignedBuildingId == building.Id) { claimed = true; break; } if (!claimed) return building; } return null; }
        private static BuildingState FindOperational(BattleWorld world, int ownerId, string role) { for (int i = 0; i < world.Buildings.Count; i++) { BuildingState b = world.Buildings[i]; if (b.Active && b.Operational && b.OwnerId == ownerId && string.Equals(b.OperationalRole, role, StringComparison.OrdinalIgnoreCase)) return b; } return null; }
        private static int CountBuildings(BattleWorld world, int ownerId) { int count = 0; for (int i = 0; i < world.Buildings.Count; i++) if (world.Buildings[i].Active && world.Buildings[i].OwnerId == ownerId) count++; return count; }
        private static int CountBuildings(BattleWorld world, int ownerId, string role) { int count = 0; for (int i = 0; i < world.Buildings.Count; i++) { BuildingState b = world.Buildings[i]; if (b.Active && b.OwnerId == ownerId && string.Equals(b.OperationalRole, role, StringComparison.OrdinalIgnoreCase)) count++; } return count; }
        private static int CountFoundations(BattleWorld world, int ownerId) { int count = 0; for (int i = 0; i < world.Buildings.Count; i++) if (world.Buildings[i].Active && world.Buildings[i].OwnerId == ownerId && world.Buildings[i].ConstructionProgress < 1f) count++; return count; }
    }
}
