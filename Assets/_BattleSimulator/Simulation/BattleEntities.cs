using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleSimulator.Simulation
{
    public enum EntityKind { Unit, Vehicle, Aircraft, Building, Projectile, ResourceZone, EconomicNode, EnvironmentalObject }
    public enum UnitRole { Trooper, Scout, Commander, Builder, SupplyCarrier, Medic, Engineer, Vehicle, Aircraft }
    public enum UnitOrder { Idle, Move, Attack, Capture, Gather, Deliver, Build, Repair, Withdraw, Defend }
    public enum MovementLayer { Ground, Air }
    public enum SquadPrimaryRole { Offensive, Capture, TerritoryDefense, EconomyDefense, RouteSecurity, Reconnaissance, Siege, Escort, MedicalSupport, RepairSupport, Reinforcement, Ambush, Reserve }
    public enum FormationType { None, Line, Column, Circle, Wedge, Triangle, Staggered, Escort, DefensiveRing, Flanking }
    public enum InjuryState { Healthy, Injured, GravelyInjured, KnockedDown, Incapacitated, Dead }
    public enum IntelContactState { Unexplored, Explored, Visible, SensorConfirmed, Remembered }
    public enum AircraftPhase { Landed, TakingOff, Flying, Hovering, Attacking, Landing, Crashed }
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
        public string Specialty;
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
        public string Rank = "Trooper";
        public string Species;
        public string Subfaction;
        public string WeaponId = "rifle";
        public MovementLayer MovementLayer;
        public UnitOrder Order;
        public Vector2 Destination;
        public Vector2 SpawnOrigin;
        public int SquadId;
        public int AttachedSquadId;
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
        public float ArmorProtection = 8f;
        public float Accuracy = 0.7f;
        public float Precision = 0.7f;
        public float FireInterval = 0.8f;
        public float FireCooldown;
        public int Ammunition = 64;
        public int MaximumAmmunition = 64;
        public int Magazine = 8;
        public int MagazineSize = 8;
        public float ReloadRemaining;
        public float ReloadDuration = 2.4f;
        public float Heat;
        public float HeatPerShot = 0.08f;
        public float MaximumHeat = 1f;
        public float CoolRate = 0.22f;
        public float Morale = 1f;
        public float Aggression = 0.5f;
        public float Confidence = 0.65f;
        public float Fatigue;
        public float Experience;
        public float Suppression;
        public float SuppressionResistance;
        public float Bleeding;
        public float KnockedDownRemaining;
        public bool Incapacitated;
        public bool Stabilized;
        public float TreatmentProgress;
        public float ReanimationProgress;
        public float RitualProgress;
        public bool CombatCapable = true;
        public bool InCover;
        public bool Camouflaged;
        public float Camouflage = 0.25f;
        public double RevealedUntil;
        public double LastWeaponDischargeAt;
        public float Alertness;
        public float Tension;
        public float Fear;
        public string LastAction;
        public bool IsDead;
        public double DeathTime;
        public float StuckSeconds;
        public Vector2 LastProgressPosition;
        public ResourceType CargoType;
        public float Cargo;
        public float CargoCapacity = 32f;
        public int AssignedResourceZoneId;
        public int AssignedBuildingId;
        public int EmbarkedInId;
        public int PassengerCapacity;
        public readonly List<int> PassengerIds = new List<int>();
        public float Fuel = 100f;
        public float MaximumFuel = 100f;
        public float Altitude;
        public AircraftPhase AircraftPhase = AircraftPhase.Landed;
        public float HullSystem = 1f;
        public float EngineSystem = 1f;
        public float TrackSystem = 1f;
        public float TurretSystem = 1f;
        public float WeaponSystem = 1f;
        public float CrewSystem = 1f;
        public float IronHalo;
        public float MaximumIronHalo;
        public double IronHaloLastHitAt;
        public double AbilityReadyAt;
        public double BuffedUntil;
        public bool GeneSeedBearing;
        public bool GeneSeedRecovered;
        public ProjectileClass ProjectileClass = ProjectileClass.Ballistic;
        public ProjectileBehavior ProjectileBehavior = ProjectileBehavior.Ricochet;

        public bool HasRecentEnemyContact(double now, float graceSeconds)
        {
            double age = now - LastEnemyContactTime;
            return age >= 0d && age <= graceSeconds;
        }

        public InjuryState Injury
        {
            get
            {
                if (IsDead) return InjuryState.Dead;
                if (Incapacitated || Condition <= 0.16f) return InjuryState.Incapacitated;
                if (KnockedDownRemaining > 0f) return InjuryState.KnockedDown;
                if (Condition < 0.48f) return InjuryState.GravelyInjured;
                if (Condition < 0.78f) return InjuryState.Injured;
                return InjuryState.Healthy;
            }
        }
    }

    [Serializable]
    public sealed class BuildingState : BattleEntityState
    {
        public BuildingType BuildingType;
        public string OperationalRole = "HQ";
        public string DisplayName;
        public string Subfaction;
        public float ConstructionProgress = 1f;
        public float ProductionCooldown;
        public bool Operational = true;
        public readonly Queue<UnitRole> ProductionQueue = new Queue<UnitRole>();
        public readonly Queue<ProductionOrder> DetailedProductionQueue = new Queue<ProductionOrder>();
        public readonly Dictionary<ResourceType, float> Storage = new Dictionary<ResourceType, float>();
        public int TerritoryCellId;
        public float SupplyThroughput = 1f;
        public bool SupplyConnected = true;
        public int CaretakerUnitId;
    }

    [Serializable]
    public sealed class ProductionOrder
    {
        public UnitRole Role;
        public string Specialty;
        public string ProducerRole;
        public int TargetSquadId;
        public float Priority;
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
        public int PreviousOwnerId;
        public int CapturingPlayerId;
        public float CaptureProgress;
        public bool Contested;
        public double CapturedAt;
        public readonly List<Vector2> Polygon = new List<Vector2>();
        public readonly List<int> NeighborIds = new List<int>();
        public bool Contains(Vector2 point)
        {
            if (Polygon.Count < 3)
            {
                Vector2 half = Size * 0.5f;
                return Mathf.Abs(point.x - Center.x) <= half.x && Mathf.Abs(point.y - Center.y) <= half.y;
            }
            bool inside = false;
            for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
            {
                Vector2 a = Polygon[i], b = Polygon[j];
                float denominator = b.y - a.y;
                if (Mathf.Abs(denominator) < 0.0001f) denominator = denominator < 0f ? -0.0001f : 0.0001f;
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / denominator + a.x) inside = !inside;
            }
            return inside;
        }
    }

    [Serializable]
    public sealed class SquadState
    {
        public int Id;
        public int OwnerId;
        public int TeamId;
        public string Name;
        public SquadPrimaryRole PrimaryRole = SquadPrimaryRole.Reserve;
        public FormationType Formation = FormationType.None;
        public Vector2 Objective;
        public int ObjectiveEntityId;
        public double RoleCommittedUntil;
        public bool FormationActive;
        public int NominalSize = 10;
        public string SquadClass = "line";
        public readonly List<int> MemberIds = new List<int>();
        public readonly List<int> AttachedCharacterIds = new List<int>();
    }

    [Serializable]
    public sealed class IntelContactRecord
    {
        public int EntityId;
        public int ObserverPlayerId;
        public IntelContactState ContactState;
        public Vector2 Position;
        public string Classification;
        public float Confidence;
        public float UncertaintyRadius;
        public double ObservedAt;
        public double ExpiresAt;
    }

    [Serializable]
    public sealed class EconomicNodeState : BattleEntityState
    {
        public string NodeType;
        public float Capacity;
        public float StrategicValue;
        public readonly Dictionary<ResourceType, float> Imports = new Dictionary<ResourceType, float>();
        public readonly Dictionary<ResourceType, float> Exports = new Dictionary<ResourceType, float>();
        public readonly Dictionary<ResourceType, float> CaptureStock = new Dictionary<ResourceType, float>();
        public int CapturingPlayerId;
        public float CaptureProgress;
        public int LastCaptureRecipient;
    }

    [Serializable]
    public sealed class TradeRouteState
    {
        public string Id, Name, Type, FromNodeId, ToNodeId;
        public float Capacity;
        public bool RoadRequired, Bidirectional, Active = true, Authored = true;
        public readonly List<ResourceType> Resources = new List<ResourceType>();
        public readonly List<string> AllowedFactions = new List<string>();
        public readonly List<Vector2> Points = new List<Vector2>();
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
        public string ObjectiveMethod = "adaptive operation";
        public Color PrimaryColor = Color.white;
        public Color SecondaryColor = Color.gray;
        public Color AccentColor = Color.yellow;
        public Color BodyColor = Color.white;
        public Vector2 SpawnOrigin;
        public float SpawnRadius = 160f;
        public bool Defeated;
        public float Aggression = 50f;
        public float Caution = 50f;
        public float Expansion = 50f;
        public float Economy = 50f;
        public float ObjectiveProgress;
        public float ObjectiveHoldSeconds;
        public float Morale = 0.7f;
        public float SupplyCondition = 1f;
        public int Casualties;
        public int UnitsKilled;
        public int BuildingsDestroyed;
        public float ResourcesDelivered;
        public int CapturedTerritories;
        public int CapturedNodes;
        public int GeneSeedStock;
        public int ForceCap = 220;
        public int ProductionSequence;
        public int ConstructionSequence;
        public float WaaaghMomentum;
        public double EnemyBaseObservedAt = double.NegativeInfinity;
        public Vector2 LastKnownEnemyBase;
        public readonly Dictionary<ResourceType, float> Resources = new Dictionary<ResourceType, float>();
        public readonly Dictionary<int, IntelContactRecord> IntelContacts = new Dictionary<int, IntelContactRecord>();
        public readonly Dictionary<string, float> StrategicWeights = new Dictionary<string, float>();
        public readonly Dictionary<string, float> IdentityWeights = new Dictionary<string, float>();

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
