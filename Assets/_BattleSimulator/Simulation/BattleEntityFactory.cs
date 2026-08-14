using UnityEngine;

namespace BattleSimulator.Simulation
{
    public static class BattleEntityFactory
    {
        public static UnitState CreateUnit(PlayerState player, UnitRole role, Vector2 position, string name = null)
        {
            bool vehicle = role == UnitRole.Vehicle;
            bool aircraft = role == UnitRole.Aircraft;
            bool support = role == UnitRole.Builder || role == UnitRole.SupplyCarrier || role == UnitRole.Medic || role == UnitRole.Engineer;
            float maximumHitPoints = vehicle ? 320f : aircraft ? 240f : role == UnitRole.Commander ? 180f : 100f;
            var unit = new UnitState
            {
                Name = name ?? role.ToString(),
                Kind = aircraft ? EntityKind.Aircraft : vehicle ? EntityKind.Vehicle : EntityKind.Unit,
                OwnerId = player.Id,
                TeamId = player.TeamId,
                Position = position,
                LastProgressPosition = position,
                SpawnOrigin = player.SpawnOrigin,
                Role = role,
                MovementLayer = aircraft ? MovementLayer.Air : MovementLayer.Ground,
                Radius = vehicle ? 9f : aircraft ? 8f : 4f,
                HitPoints = maximumHitPoints,
                MaximumHitPoints = maximumHitPoints,
                Speed = aircraft ? 62f : vehicle ? 44f : role == UnitRole.SupplyCarrier ? 39f : role == UnitRole.Scout ? 36f : 28f,
                VisionRange = role == UnitRole.Scout ? 240f : role == UnitRole.Commander ? 210f : 180f,
                AuspexRange = player.Faction == "Space Marines" ? 280f : 210f,
                WeaponRange = support ? 45f : role == UnitRole.Scout ? 145f : vehicle ? 175f : aircraft ? 190f : 115f,
                Damage = support ? 4f : role == UnitRole.Commander ? 20f : vehicle ? 30f : aircraft ? 26f : 13f,
                Penetration = vehicle ? 24f : aircraft ? 20f : 11f,
                Accuracy = player.Faction == "Space Marines" ? 0.82f : player.Race == "Orks" ? 0.58f : 0.7f,
                FireInterval = vehicle ? 1.8f : aircraft ? 1.1f : 0.72f,
                Ammunition = support ? 12 : vehicle ? 72 : 64,
                MaximumAmmunition = support ? 12 : vehicle ? 72 : 64,
                CombatCapable = !support || role == UnitRole.Engineer,
                ProjectileClass = player.Race == "Orks" ? ProjectileClass.Ballistic : player.Faction == "Space Marines" ? ProjectileClass.Bolt : ProjectileClass.Ballistic,
                ProjectileBehavior = player.Faction == "Space Marines"
                    ? ProjectileBehavior.Explosive | ProjectileBehavior.Suppression | ProjectileBehavior.AntiInfantry
                    : ProjectileBehavior.Ricochet
            };
            return unit;
        }

        public static BuildingState CreateBuilding(PlayerState player, BuildingType type, Vector2 position, float constructionProgress = 1f)
        {
            float maximumHitPoints = type == BuildingType.Headquarters ? 1200f : type == BuildingType.Defense ? 650f : 720f;
            return new BuildingState
            {
                Name = type.ToString(),
                Kind = EntityKind.Building,
                OwnerId = player.Id,
                TeamId = player.TeamId,
                Position = position,
                Radius = type == BuildingType.Headquarters ? 22f : 15f,
                HitPoints = maximumHitPoints * Mathf.Clamp01(constructionProgress),
                MaximumHitPoints = maximumHitPoints,
                BuildingType = type,
                ConstructionProgress = constructionProgress,
                Operational = constructionProgress >= 1f
            };
        }
    }
}
