using System.Collections.Generic;
using BattleSimulator.Core;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class EconomyTerritorySystem : IBattleSystem
    {
        private readonly Dictionary<int, float> incomeAccumulator = new Dictionary<int, float>();
        public int Order => 500;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            CaptureTerritory(world, step.DeltaTime);
            CaptureResourceZones(world, step.DeltaTime);
            RunCarriers(world, step.DeltaTime);
            AccrueTerritoryIncome(world, step.DeltaTime);
        }

        private static void CaptureTerritory(BattleWorld world, float dt)
        {
            for (int c = 0; c < world.TerritoryCells.Count; c++)
            {
                TerritoryCellState cell = world.TerritoryCells[c];
                int claimant = 0;
                bool contested = false;
                for (int i = 0; i < world.Units.Count; i++)
                {
                    UnitState unit = world.Units[i];
                    if (!unit.Active || unit.IsDead || !unit.CombatCapable || !Inside(cell, unit.Position)) continue;
                    if (claimant == 0) claimant = unit.OwnerId;
                    else if (!world.AreAllies(claimant, unit.OwnerId)) { contested = true; break; }
                }
                cell.Contested = contested;
                if (contested || claimant == 0) continue;
                if (cell.OwnerId == claimant) { cell.CaptureProgress = 0f; cell.CapturingPlayerId = 0; continue; }
                if (cell.CapturingPlayerId != claimant) { cell.CapturingPlayerId = claimant; cell.CaptureProgress = 0f; }
                cell.CaptureProgress += dt / 3f;
                if (cell.CaptureProgress < 1f) continue;
                cell.OwnerId = claimant;
                cell.CaptureProgress = 0f;
                cell.CapturingPlayerId = 0;
                world.Events.Publish(new BattleEvent(BattleEventType.TerritoryCaptured, world.Time, cell.Id, claimant, cell.Center, "Territory captured by physical units."));
            }
        }

        private static void CaptureResourceZones(BattleWorld world, float dt)
        {
            for (int z = 0; z < world.ResourceZones.Count; z++)
            {
                ResourceZoneState zone = world.ResourceZones[z];
                int claimant = 0;
                bool contested = false;
                for (int i = 0; i < world.Units.Count; i++)
                {
                    UnitState unit = world.Units[i];
                    if (!unit.Active || unit.IsDead || !unit.CombatCapable || !zone.Contains(unit.Position)) continue;
                    if (claimant == 0) claimant = unit.OwnerId;
                    else if (!world.AreAllies(claimant, unit.OwnerId)) { contested = true; break; }
                }
                if (contested || claimant == 0 || zone.OwnerId == claimant) continue;
                if (zone.CapturingPlayerId != claimant) { zone.CapturingPlayerId = claimant; zone.CaptureProgress = 0f; }
                zone.CaptureProgress += dt * 0.4f;
                if (zone.CaptureProgress < 1f) continue;
                zone.OwnerId = claimant;
                zone.CapturingPlayerId = 0;
                zone.CaptureProgress = 0f;
            }
        }

        private static void RunCarriers(BattleWorld world, float dt)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState carrier = world.Units[i];
                if (!carrier.Active || carrier.IsDead || carrier.Role != UnitRole.SupplyCarrier && carrier.Role != UnitRole.Builder) continue;
                if (carrier.Cargo < carrier.CargoCapacity)
                {
                    ResourceZoneState zone = AssignedZone(world, carrier);
                    if (zone == null) continue;
                    carrier.AssignedResourceZoneId = zone.Id;
                    carrier.Order = UnitOrder.Gather;
                    carrier.Destination = zone.Position;
                    if (zone.Contains(carrier.Position) && zone.Remaining > 0f)
                    {
                        float amount = Mathf.Min(zone.Remaining, Mathf.Min(carrier.CargoCapacity - carrier.Cargo, zone.GatherRate * dt));
                        carrier.CargoType = zone.ResourceType;
                        carrier.Cargo += amount;
                        zone.Remaining -= amount;
                    }
                }
                else
                {
                    BuildingState warehouse = NearestWarehouse(world, carrier);
                    if (warehouse == null) continue;
                    carrier.Order = UnitOrder.Deliver;
                    carrier.Destination = warehouse.Position;
                    if (Vector2.Distance(carrier.Position, warehouse.Position) <= warehouse.Radius + 5f)
                    {
                        world.GetPlayer(carrier.OwnerId)?.AddResource(carrier.CargoType, carrier.Cargo);
                        carrier.Cargo = 0f;
                        carrier.AssignedResourceZoneId = 0;
                    }
                }
            }
        }

        private void AccrueTerritoryIncome(BattleWorld world, float dt)
        {
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                int territories = 0;
                for (int c = 0; c < world.TerritoryCells.Count; c++) if (world.TerritoryCells[c].OwnerId == player.Id) territories++;
                incomeAccumulator.TryGetValue(player.Id, out float accumulated);
                accumulated += dt * territories * 5f;
                int whole = Mathf.FloorToInt(accumulated);
                if (whole > 0) { player.AddResource(ResourceType.Requisition, whole); accumulated -= whole; }
                incomeAccumulator[player.Id] = accumulated;
            }
        }

        private static bool Inside(TerritoryCellState cell, Vector2 point)
        {
            Vector2 half = cell.Size * 0.5f;
            return Mathf.Abs(point.x - cell.Center.x) <= half.x && Mathf.Abs(point.y - cell.Center.y) <= half.y;
        }

        private static ResourceZoneState AssignedZone(BattleWorld world, UnitState carrier)
        {
            ResourceZoneState current = world.GetEntity<ResourceZoneState>(carrier.AssignedResourceZoneId);
            if (current != null && current.Active && current.OwnerId == carrier.OwnerId && current.Remaining > 0f) return current;
            ResourceZoneState best = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < world.ResourceZones.Count; i++)
            {
                ResourceZoneState zone = world.ResourceZones[i];
                if (!zone.Active || zone.OwnerId != carrier.OwnerId || zone.Remaining <= 0f) continue;
                float candidate = Vector2.SqrMagnitude(zone.Position - carrier.Position);
                if (candidate < distance) { best = zone; distance = candidate; }
            }
            return best;
        }

        private static BuildingState NearestWarehouse(BattleWorld world, UnitState carrier)
        {
            BuildingState best = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState building = world.Buildings[i];
                if (!building.Active || building.OwnerId != carrier.OwnerId || !building.Operational
                    || building.BuildingType != BuildingType.Warehouse && building.BuildingType != BuildingType.Headquarters) continue;
                float candidate = Vector2.SqrMagnitude(building.Position - carrier.Position);
                if (candidate < distance) { best = building; distance = candidate; }
            }
            return best;
        }
    }
}
