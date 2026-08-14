using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleSimulator.Simulation
{
    public enum EntityKind { Unit, Vehicle, Aircraft, Building, Projectile, ResourceZone, EnvironmentalObject }
    public enum UnitRole { Trooper, Scout, Commander, Builder, SupplyCarrier, Medic, Engineer, Vehicle, Aircraft }
    public enum UnitOrder { Idle, Move, Attack, Capture, Gather, Deliver, Build, Repair, Withdraw, Defend }
    public enum MovementLayer { Ground, Air }
    public enum BuildingType { Headquarters, Barracks, Workshop, AirSupport, Research, Hospital, Warehouse, Generator, Defense, ResourceExtractor, ForwardOutpost }
    public enum ResourceType { Materials, Fuel, Energy, Food, Scrap, Biomass, Requisition, Influence, Medical, Parts, Ammunition, Security }
    public enum ProjectileClass { Ballistic, Bolt, Pellet, Beam, EnergyBolt, Plasma, Melta, Flame, Rocket, HomingMissile, Grenade, Mortar, Artillery, HeavyShell, BioProjectile }

    [Flags]
    public enum ProjectileBehavior
    {
        None = 0,
        Piercing = 1 << 0,
        Explosive = 1 << 1,
        Guided = 1 << 2,
        Incendiary = 1 << 3,
        Corrosive = 1 << 4,
        Chain = 1 << 5,
        Stun = 1 << 6,
        Suppression = 1 << 7,
        AntiArmor = 1 << 8,
        AntiInfantry = 1 << 9,
        Indirect = 1 << 10,
        Persistent = 1 << 11,
        Stealthy = 1 << 12,
        Ricochet = 1 << 13,
        ProximityFuse = 1 << 14
    }

    [Serializable]
    public abstract class BattleEntityState
    {
        public int Id;
        public string Name;
        public EntityKind Kind;
        public int OwnerId;
        public int TeamId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius = 4f;
        public float HitPoints = 100f;
        public float MaximumHitPoints = 100f;
        public bool Active = true;
        public bool Visible = true;

        public float Condition => MaximumHitPoints <= 0f ? 0f : Mathf.Clamp01(HitPoints / MaximumHitPoints);
    }

    [Serializable]
    public sealed class UnitState : BattleEntityState
    {
        public UnitRole Role = UnitRole.Trooper;
        public MovementLayer MovementLayer;
        public UnitOrder Order;
        public Vector2 Destination;
        public Vector2 SpawnOrigin;
        public int SquadId;
        public int TargetEntityId;
        public int EnemyContactId;
        public double LastEnemyContactTime = double.NegativeInfinity;
        public float FacingRadians;
        public float Speed = 28f;
        public float Acceleration = 90f;
        public float VisionRange = 180f;
        public float VisionArcDegrees = 215f;
        public float AuspexRange = 240f;
        public float WeaponRange = 110f;
        public float Damage = 12f;
        public float Penetration = 10f;
        public float Accuracy = 0.7f;
        public float FireInterval = 0.8f;
        public float FireCooldown;
        public int Ammunition = 64;
        public int MaximumAmmunition = 64;
        public float Morale = 1f;
        public float Suppression;
        public bool CombatCapable = true;
        public bool InCover;
        public bool Camouflaged;
        public bool IsDead;
        public double DeathTime;
        public float StuckSeconds;
        public Vector2 LastProgressPosition;
        public ResourceType CargoType;
        public float Cargo;
        public float CargoCapacity = 32f;
        public int AssignedResourceZoneId;
        public int AssignedBuildingId;
        public ProjectileClass ProjectileClass = ProjectileClass.Ballistic;
        public ProjectileBehavior ProjectileBehavior = ProjectileBehavior.Ricochet;

        public bool HasRecentEnemyContact(double now, float graceSeconds)
        {
            double age = now - LastEnemyContactTime;
            return age >= 0d && age <= graceSeconds;
        }
    }

    [Serializable]
    public sealed class BuildingState : BattleEntityState
    {
        public BuildingType BuildingType;
        public float ConstructionProgress = 1f;
        public float ProductionCooldown;
        public bool Operational = true;
        public readonly Queue<UnitRole> ProductionQueue = new Queue<UnitRole>();
        public readonly Dictionary<ResourceType, float> Storage = new Dictionary<ResourceType, float>();
    }

    [Serializable]
    public sealed class ProjectileState : BattleEntityState
    {
        public int ShooterId;
        public int TargetId;
        public ProjectileClass ProjectileClass;
        public ProjectileBehavior Behavior;
        public float Damage;
        public float Penetration;
        public float Speed;
        public float RemainingRange;
        public float SplashRadius;
    }

    [Serializable]
    public sealed class ResourceZoneState : BattleEntityState
    {
        public ResourceType ResourceType;
        public float Capacity = 25000f;
        public float Remaining = 25000f;
        public float GatherRate = 20f;
        public float RegenerationRate;
        public int CapturingPlayerId;
        public float CaptureProgress;
        public readonly List<Vector2> Polygon = new List<Vector2>();

        public bool Contains(Vector2 point)
        {
            if (Polygon.Count < 3)
            {
                return Vector2.Distance(Position, point) <= Radius;
            }

            bool inside = false;
            for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
            {
                Vector2 a = Polygon[i];
                Vector2 b = Polygon[j];
                float denominator = b.y - a.y;
                if (Mathf.Abs(denominator) < 0.0001f) denominator = denominator < 0f ? -0.0001f : 0.0001f;
                bool intersects = (a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / denominator + a.x;
                if (intersects) inside = !inside;
            }

            return inside;
        }
    }

    [Serializable]
    public sealed class TerritoryCellState
    {
        public int Id;
        public Vector2 Center;
        public Vector2 Size;
        public int OwnerId;
        public int CapturingPlayerId;
        public float CaptureProgress;
        public bool Contested;
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int Id;
        public int TeamId;
        public string Name;
        public string Race;
        public string Faction;
        public string Subfaction;
        public string BattleObjective = "annihilation";
        public Color PrimaryColor = Color.white;
        public Color SecondaryColor = Color.gray;
        public Color AccentColor = Color.yellow;
        public Color BodyColor = Color.white;
        public Vector2 SpawnOrigin;
        public float SpawnRadius = 160f;
        public bool Defeated;
        public readonly Dictionary<ResourceType, float> Resources = new Dictionary<ResourceType, float>();

        public float Resource(ResourceType type)
        {
            return Resources.TryGetValue(type, out float amount) ? amount : 0f;
        }

        public void AddResource(ResourceType type, float amount)
        {
            Resources[type] = Mathf.Max(0f, Resource(type) + amount);
        }
    }
}
