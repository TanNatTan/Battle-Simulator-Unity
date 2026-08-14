using BattleSimulator.Core;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class CombatSystem : IBattleSystem
    {
        public int Order => 300;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState attacker = world.Units[i];
                if (!attacker.Active || attacker.IsDead || !attacker.CombatCapable) continue;
                attacker.FireCooldown = Mathf.Max(0f, attacker.FireCooldown - step.DeltaTime);
                attacker.Suppression = Mathf.Max(0f, attacker.Suppression - step.DeltaTime * 0.08f);
                if (attacker.Order != UnitOrder.Attack || attacker.FireCooldown > 0f || attacker.Ammunition <= 0) continue;
                if (!world.TryGetEntity(attacker.TargetEntityId, out BattleEntityState target) || !target.Active || world.AreAllies(attacker.OwnerId, target.OwnerId)) continue;
                float distance = Vector2.Distance(attacker.Position, target.Position);
                if (distance > attacker.WeaponRange) continue;

                attacker.LastEnemyContactTime = world.Time;
                attacker.EnemyContactId = target.Id;
                attacker.Ammunition--;
                attacker.FireCooldown = attacker.FireInterval * Mathf.Lerp(1.35f, 0.82f, attacker.Morale) * (1f + attacker.Suppression * 0.5f);
                Vector2 direction = (target.Position - attacker.Position).normalized;
                float spread = (1f - attacker.Accuracy) * 0.18f;
                float angle = Mathf.Atan2(direction.y, direction.x) + world.Random.Range(-spread, spread);
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = ProjectileSpeed(attacker.ProjectileClass);
                ProjectileState projectile = world.AcquireProjectile();
                projectile.Name = attacker.ProjectileClass.ToString();
                projectile.Kind = EntityKind.Projectile;
                projectile.OwnerId = attacker.OwnerId;
                projectile.TeamId = attacker.TeamId;
                projectile.Position = attacker.Position + direction * (attacker.Radius + 1f);
                projectile.Velocity = direction * speed;
                projectile.Radius = attacker.ProjectileClass == ProjectileClass.Artillery ? 3f : 1.5f;
                projectile.HitPoints = 1f;
                projectile.MaximumHitPoints = 1f;
                projectile.ShooterId = attacker.Id;
                projectile.TargetId = target.Id;
                projectile.ProjectileClass = attacker.ProjectileClass;
                projectile.Behavior = attacker.ProjectileBehavior;
                projectile.Damage = attacker.Damage;
                projectile.Penetration = attacker.Penetration;
                projectile.Speed = speed;
                projectile.RemainingRange = attacker.WeaponRange * 1.25f;
                projectile.SplashRadius = (attacker.ProjectileBehavior & ProjectileBehavior.Explosive) != 0 ? Mathf.Max(5f, attacker.Damage * 0.35f) : 0f;
                world.AddEntity(projectile);
            }
        }

        private static float ProjectileSpeed(ProjectileClass projectileClass)
        {
            switch (projectileClass)
            {
                case ProjectileClass.Beam:
                case ProjectileClass.Melta: return 1800f;
                case ProjectileClass.HeavyShell: return 390f;
                case ProjectileClass.EnergyBolt: return 330f;
                case ProjectileClass.Bolt: return 285f;
                case ProjectileClass.HomingMissile: return 270f;
                case ProjectileClass.Rocket: return 250f;
                case ProjectileClass.Plasma: return 245f;
                case ProjectileClass.Pellet: return 220f;
                case ProjectileClass.BioProjectile: return 190f;
                case ProjectileClass.Artillery: return 170f;
                case ProjectileClass.Flame:
                case ProjectileClass.Grenade: return 150f;
                case ProjectileClass.Mortar: return 135f;
                default: return 245f;
            }
        }
    }
}
