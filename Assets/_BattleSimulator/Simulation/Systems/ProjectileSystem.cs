using System.Collections.Generic;
using BattleSimulator.Core;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class ProjectileSystem : IBattleSystem
    {
        private readonly List<int> nearbyIds = new List<int>(48);
        public int Order => 400;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            int projectileCount = world.Projectiles.Count;
            for (int i = 0; i < projectileCount; i++)
            {
                ProjectileState projectile = world.Projectiles[i];
                if (!projectile.Active) continue;
                if ((projectile.Behavior & ProjectileBehavior.Guided) != 0
                    && world.TryGetEntity(projectile.TargetId, out BattleEntityState guidedTarget) && guidedTarget.Active)
                {
                    Vector2 desired = (guidedTarget.Position - projectile.Position).normalized * projectile.Speed;
                    projectile.Velocity = Vector2.Lerp(projectile.Velocity, desired, Mathf.Clamp01(step.DeltaTime * 4f));
                }

                Vector2 previous = projectile.Position;
                projectile.Position += projectile.Velocity * step.DeltaTime;
                projectile.RemainingRange -= Vector2.Distance(previous, projectile.Position);
                BattleEntityState hit = FindHit(world, projectile, previous);
                if (hit != null)
                {
                    ResolveImpact(world, projectile, hit);
                    projectile.Active = false;
                }
                else if (projectile.RemainingRange <= 0f || projectile.Position.x < 0f || projectile.Position.y < 0f
                    || projectile.Position.x > world.Width || projectile.Position.y > world.Height)
                {
                    projectile.Active = false;
                }
            }
        }

        private BattleEntityState FindHit(BattleWorld world, ProjectileState projectile, Vector2 previous)
        {
            Vector2 segment = projectile.Position - previous;
            Vector2 midpoint = (previous + projectile.Position) * 0.5f;
            world.Spatial.Query(midpoint, segment.magnitude * 0.5f + projectile.Radius + 12f, nearbyIds);
            BattleEntityState closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < nearbyIds.Count; i++)
            {
                if (!world.TryGetEntity(nearbyIds[i], out BattleEntityState candidate) || !candidate.Active
                    || candidate.Id == projectile.Id || candidate.Id == projectile.ShooterId
                    || candidate.Kind == EntityKind.Projectile || world.AreAllies(projectile.OwnerId, candidate.OwnerId)) continue;
                if (candidate is UnitState unit && unit.IsDead) continue;
                float distance = DistanceToSegment(candidate.Position, previous, projectile.Position);
                if (distance <= projectile.Radius + candidate.Radius && distance < closestDistance)
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private void ResolveImpact(BattleWorld world, ProjectileState projectile, BattleEntityState directHit)
        {
            if (projectile.SplashRadius <= 0f)
            {
                ApplyDamage(world, projectile, directHit, projectile.Damage);
                return;
            }

            world.Spatial.Query(directHit.Position, projectile.SplashRadius, nearbyIds);
            for (int i = 0; i < nearbyIds.Count; i++)
            {
                if (!world.TryGetEntity(nearbyIds[i], out BattleEntityState target) || !target.Active
                    || target.Kind == EntityKind.Projectile || world.AreAllies(projectile.OwnerId, target.OwnerId)) continue;
                float distance = Vector2.Distance(directHit.Position, target.Position);
                float falloff = 1f - Mathf.Clamp01(distance / projectile.SplashRadius);
                ApplyDamage(world, projectile, target, projectile.Damage * Mathf.Max(0.2f, falloff));
            }
        }

        private static void ApplyDamage(BattleWorld world, ProjectileState projectile, BattleEntityState target, float rawDamage)
        {
            float armorFactor = (projectile.Behavior & ProjectileBehavior.AntiArmor) != 0 ? 1f : Mathf.Clamp01(projectile.Penetration / 24f);
            float damage = Mathf.Max(1f, rawDamage * Mathf.Lerp(0.55f, 1f, armorFactor));
            target.HitPoints = Mathf.Max(0f, target.HitPoints - damage);
            if (target is UnitState unit)
            {
                unit.LastEnemyContactTime = world.Time;
                unit.EnemyContactId = projectile.ShooterId;
                if ((projectile.Behavior & ProjectileBehavior.Suppression) != 0) unit.Suppression = Mathf.Clamp01(unit.Suppression + 0.18f);
                world.Events.Publish(new BattleEvent(BattleEventType.UnitWounded, world.Time, unit.Id, projectile.OwnerId, unit.Position, $"{unit.Name} was hit."));
                if (unit.HitPoints <= 0f && !unit.IsDead)
                {
                    unit.IsDead = true;
                    unit.DeathTime = world.Time;
                    unit.CombatCapable = false;
                    unit.Velocity = Vector2.zero;
                    unit.Order = UnitOrder.Idle;
                    world.Events.Publish(new BattleEvent(BattleEventType.UnitKilled, world.Time, unit.Id, projectile.OwnerId, unit.Position, $"{unit.Name} was killed."));
                }
            }
            else if (target is BuildingState building && building.HitPoints <= 0f)
            {
                building.Operational = false;
                building.Active = false;
                world.Events.Publish(new BattleEvent(BattleEventType.BaseAttacked, world.Time, building.Id, projectile.OwnerId, building.Position, $"{building.Name} was destroyed."));
            }
        }
    }
}
