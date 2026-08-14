using System.Collections.Generic;
using BattleSimulator.Core;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class MovementSystem : IBattleSystem
    {
        private readonly List<int> nearbyIds = new List<int>(64);

        public int Order => 200;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            float dt = step.DeltaTime;
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (!unit.Active) continue;
                Vector2 desired = DesiredVelocity(world, unit);
                desired += Separation(world, unit);
                unit.Velocity = Vector2.MoveTowards(unit.Velocity, desired, unit.Acceleration * dt);
                Vector2 candidate = world.ClampToWorld(unit.Position + unit.Velocity * dt, unit.Radius);
                if (unit.MovementLayer == MovementLayer.Air || !CollidesWithBuilding(world, unit, candidate))
                {
                    unit.Position = candidate;
                }
                else
                {
                    unit.Velocity *= 0.25f;
                }

                if (unit.Velocity.sqrMagnitude > 0.1f)
                {
                    unit.FacingRadians = Mathf.Atan2(unit.Velocity.y, unit.Velocity.x);
                }

                UpdateStuckRecovery(world, unit, dt);
            }
        }

        private static Vector2 DesiredVelocity(BattleWorld world, UnitState unit)
        {
            if (unit.Order == UnitOrder.Idle || unit.Order == UnitOrder.Defend || unit.Order == UnitOrder.Build || unit.Order == UnitOrder.Repair)
            {
                return Vector2.zero;
            }

            Vector2 destination = unit.Destination;
            if (unit.Order == UnitOrder.Attack && world.TryGetEntity(unit.TargetEntityId, out BattleEntityState target) && target.Active)
            {
                destination = target.Position;
                unit.Destination = destination;
                float preferredRange = Mathf.Max(8f, unit.WeaponRange * 0.82f);
                if (Vector2.Distance(unit.Position, destination) <= preferredRange)
                {
                    return Vector2.zero;
                }
            }

            Vector2 offset = destination - unit.Position;
            if (offset.sqrMagnitude <= 9f) return Vector2.zero;
            float speedMultiplier = unit.Order == UnitOrder.Withdraw ? 1.25f : unit.Condition < 0.35f ? 0.72f : 1f;
            return offset.normalized * unit.Speed * speedMultiplier;
        }

        private Vector2 Separation(BattleWorld world, UnitState unit)
        {
            world.Spatial.Query(unit.Position, unit.Radius * 3f + 4f, nearbyIds);
            Vector2 force = Vector2.zero;
            for (int i = 0; i < nearbyIds.Count; i++)
            {
                UnitState other = world.GetEntity<UnitState>(nearbyIds[i]);
                if (other == null || other.Id == unit.Id || !other.Active || other.MovementLayer != unit.MovementLayer) continue;
                Vector2 away = unit.Position - other.Position;
                float minimum = unit.Radius + other.Radius + 1f;
                float distance = away.magnitude;
                if (distance <= 0.01f)
                {
                    away = new Vector2((unit.Id & 1) == 0 ? 1f : -1f, (unit.Id & 2) == 0 ? 1f : -1f);
                    distance = 0.01f;
                }
                if (distance < minimum) force += away.normalized * (minimum - distance) * 6f;
            }
            return Vector2.ClampMagnitude(force, unit.Speed * 0.8f);
        }

        private static bool CollidesWithBuilding(BattleWorld world, UnitState unit, Vector2 candidate)
        {
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState building = world.Buildings[i];
                if (!building.Active) continue;
                if (Vector2.Distance(candidate, building.Position) < unit.Radius + building.Radius + 1f) return true;
            }
            return false;
        }

        private static void UpdateStuckRecovery(BattleWorld world, UnitState unit, float dt)
        {
            bool intendsMovement = unit.Order != UnitOrder.Idle && unit.Order != UnitOrder.Defend
                && Vector2.Distance(unit.Position, unit.Destination) > 12f;
            if (!intendsMovement)
            {
                unit.StuckSeconds = 0f;
                unit.LastProgressPosition = unit.Position;
                return;
            }

            if (Vector2.Distance(unit.Position, unit.LastProgressPosition) < 0.5f) unit.StuckSeconds += dt;
            else
            {
                unit.StuckSeconds = 0f;
                unit.LastProgressPosition = unit.Position;
            }

            if (unit.StuckSeconds < 8f) return;
            unit.Position = world.ClampToWorld(unit.SpawnOrigin + new Vector2((unit.Id % 5 - 2) * 5f, (unit.Id % 7 - 3) * 5f), unit.Radius);
            unit.Velocity = Vector2.zero;
            unit.StuckSeconds = 0f;
            unit.LastProgressPosition = unit.Position;
        }
    }
}
