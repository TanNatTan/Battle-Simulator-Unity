using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleSimulator.Data
{
    public sealed class BattleObjectiveDefinition
    {
        public string Id, Name, Category, Summary, Metric;
        public float Threshold = 1f, HoldSeconds, DurationSeconds;
        public readonly Dictionary<string, float> Signals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public float Signal(string key) => Signals.TryGetValue(key, out float value) ? value : 0f;
    }

    public sealed class FactionAiDefinition
    {
        public string Id;
        public readonly Dictionary<string, float> Behavior = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, float> Weights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, float> Identity = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public float BehaviorValue(string key, float fallback = 50f) => Behavior.TryGetValue(key, out float value) ? value : fallback;
        public float Weight(string key, float fallback = 1f) => Weights.TryGetValue(key, out float value) ? value : fallback;
    }

    public sealed class ProductionPlanDefinition
    {
        public string Subfaction, Race, ProductionStyle;
        public readonly List<string> BuildingOrder = new List<string>();
        public readonly List<string> UnitPriority = new List<string>();
        public readonly List<string> VehiclePriority = new List<string>();
    }

    public sealed class BuilderPolicyDefinition
    {
        public string Id, BuilderName;
        public int StartingMinimum, StartingMaximum, GrowthMultiplier, HardCap;
        public int RepairReserve, GatherReserve, ConstructionReserve;
        public bool ReplaceDead;
    }

    public sealed class WeaponDefinition
    {
        public string Id, Label, ProjectileClass;
        public float Damage, Penetration, Range, RateOfFire, ReloadTime, ProjectileSpeed, Accuracy, Precision, Suppression, HeatPerShot, CoolRate, MaximumHeat, SplashRadius;
        public int MagazineSize;
    }

    public sealed class ProjectileDefinition
    {
        public string Id;
        public float Speed;
        public readonly HashSet<string> Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class MapResourceZoneDefinition
    {
        public string Id, Name, ResourceType;
        public float Capacity, GatherRate, Regeneration;
        public bool Infinite, RequiresBuilding;
        public readonly List<Vector2> NormalizedPoints = new List<Vector2>();
    }

    public sealed class MapEconomicNodeDefinition
    {
        public string Id, Name, Type;
        public Vector2 NormalizedPosition;
    }

    public sealed class MapTradeRouteDefinition
    {
        public string Id, Name, Type, FromNodeId, ToNodeId;
        public float Capacity;
        public bool RoadRequired, Bidirectional = true;
        public readonly List<string> Resources = new List<string>();
        public readonly List<string> AllowedFactions = new List<string>();
        public readonly List<Vector2> NormalizedPoints = new List<Vector2>();
    }

    public sealed class EconomicMapPresetDefinition
    {
        public string Id;
        public readonly List<MapResourceZoneDefinition> ResourceZones = new List<MapResourceZoneDefinition>();
        public readonly List<MapEconomicNodeDefinition> EconomicNodes = new List<MapEconomicNodeDefinition>();
        public readonly List<MapTradeRouteDefinition> TradeRoutes = new List<MapTradeRouteDefinition>();
    }

    public sealed class BattleDataRepository
    {
        public readonly Dictionary<string, BattleObjectiveDefinition> Objectives = new Dictionary<string, BattleObjectiveDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, FactionAiDefinition> FactionAi = new Dictionary<string, FactionAiDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, FactionAiDefinition> SubfactionAi = new Dictionary<string, FactionAiDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ProductionPlanDefinition> ProductionPlans = new Dictionary<string, ProductionPlanDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, BuilderPolicyDefinition> BuilderPolicies = new Dictionary<string, BuilderPolicyDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, WeaponDefinition> Weapons = new Dictionary<string, WeaponDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ProjectileDefinition> Projectiles = new Dictionary<string, ProjectileDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, EconomicMapPresetDefinition> MapPresets = new Dictionary<string, EconomicMapPresetDefinition>(StringComparer.OrdinalIgnoreCase);

        private static BattleDataRepository instance;
        public static BattleDataRepository Instance => instance ?? (instance = LoadFromResources());

        public BattleObjectiveDefinition Objective(string id) => id != null && Objectives.TryGetValue(id, out BattleObjectiveDefinition value) ? value : Objectives["annihilation"];
        public FactionAiDefinition FactionProfile(string id) => id != null && FactionAi.TryGetValue(id, out FactionAiDefinition value) ? value : FactionAi["Space Marines"];
        public ProductionPlanDefinition ProductionPlan(string subfaction) => subfaction != null && ProductionPlans.TryGetValue(subfaction, out ProductionPlanDefinition value) ? value : null;
        public BuilderPolicyDefinition BuilderPolicy(string faction)
        {
            string key = NormalizeFactionKey(faction);
            return BuilderPolicies.TryGetValue(key, out BuilderPolicyDefinition value) ? value : BuilderPolicies["space_marines"];
        }

        public static BattleDataRepository LoadFromResources()
        {
            var repository = new BattleDataRepository();
            repository.ReadObjectives(BattleDataCatalog.BattleObjectives);
            repository.ReadFactionBranches(BattleDataCatalog.FactionBranches);
            repository.ReadProductionPlans(BattleDataCatalog.ProductionPlans);
            repository.ReadBuilderPolicies(BattleDataCatalog.BuilderWorkforcePolicy);
            repository.ReadWeapons(BattleDataCatalog.Weapons);
            repository.ReadProjectiles(BattleDataCatalog.Projectiles);
            repository.ReadMapPresets(BattleDataCatalog.EconomicMapPresets);
            repository.EnsureFallbacks();
            return repository;
        }

        public static BattleDataRepository LoadFromJson(Func<string, string> loader)
        {
            var repository = new BattleDataRepository();
            repository.ReadObjectives(loader("ai/battle-objectives"));
            repository.ReadFactionBranches(loader("ai/faction-branches"));
            repository.ReadProductionPlans(loader("ai/subfaction-production-plans"));
            repository.ReadBuilderPolicies(loader("ai/builder-workforce-policy"));
            repository.ReadWeapons(loader("weapons"));
            repository.ReadProjectiles(loader("projectiles"));
            repository.ReadMapPresets(loader("maps/economic-presets"));
            repository.EnsureFallbacks();
            return repository;
        }

        private void ReadObjectives(string json)
        {
            Dictionary<string, object> root = JsonData.Object(RuntimeJson.Parse(json));
            foreach (KeyValuePair<string, object> pair in root.Child("objectives"))
            {
                Dictionary<string, object> value = JsonData.Object(pair.Value);
                var definition = new BattleObjectiveDefinition
                {
                    Id = pair.Key, Name = value.String("name", pair.Key), Category = value.String("category", "control"),
                    Summary = value.String("summary"), Metric = value.String("metric", "enemyElimination"),
                    Threshold = value.Float("threshold", 1f), HoldSeconds = value.Float("holdSeconds"), DurationSeconds = value.Float("durationSeconds")
                };
                ReadFloatMap(value.Child("aiSignals"), definition.Signals);
                Objectives[definition.Id] = definition;
            }
        }

        private void ReadFactionBranches(string json)
        {
            Dictionary<string, object> root = JsonData.Object(RuntimeJson.Parse(json));
            ReadAiProfiles(root.Child("races"), FactionAi);
            ReadAiProfiles(root.Child("subfactions"), SubfactionAi);
        }

        private static void ReadAiProfiles(Dictionary<string, object> source, Dictionary<string, FactionAiDefinition> target)
        {
            foreach (KeyValuePair<string, object> pair in source)
            {
                Dictionary<string, object> value = JsonData.Object(pair.Value);
                var definition = new FactionAiDefinition { Id = pair.Key };
                ReadFloatMap(value.Child("behavior"), definition.Behavior);
                ReadFloatMap(value.Child("weights"), definition.Weights);
                ReadFloatMap(value.Child("identity"), definition.Identity);
                target[pair.Key] = definition;
            }
        }

        private void ReadProductionPlans(string json)
        {
            Dictionary<string, object> root = JsonData.Object(RuntimeJson.Parse(json));
            foreach (KeyValuePair<string, object> pair in root.Child("subfactions"))
            {
                Dictionary<string, object> value = JsonData.Object(pair.Value);
                var definition = new ProductionPlanDefinition { Subfaction = pair.Key, Race = value.String("race"), ProductionStyle = value.String("productionStyle") };
                Dictionary<string, object> buildingPlan = value.Child("buildingPlan");
                ReadStrings(buildingPlan.Children("fallbackFullOrder"), definition.BuildingOrder);
                ReadStrings(value.Children("unitPriority"), definition.UnitPriority);
                ReadStrings(value.Children("vehiclePriority"), definition.VehiclePriority);
                ProductionPlans[pair.Key] = definition;
            }
        }

        private void ReadBuilderPolicies(string json)
        {
            Dictionary<string, object> root = JsonData.Object(RuntimeJson.Parse(json));
            foreach (KeyValuePair<string, object> pair in root.Child("profiles"))
            {
                Dictionary<string, object> value = JsonData.Object(pair.Value);
                BuilderPolicies[pair.Key] = new BuilderPolicyDefinition
                {
                    Id = pair.Key, BuilderName = value.String("builder", "Builder"), StartingMinimum = value.Int("startingMin", 2),
                    StartingMaximum = value.Int("startingMax", 4), GrowthMultiplier = value.Int("growthMultiplier", 2),
                    HardCap = value.Int("hardCap", 8), ReplaceDead = value.Bool("replaceDead", true),
                    RepairReserve = value.Int("repairReserve", 1), GatherReserve = value.Int("gatherReserve", 1),
                    ConstructionReserve = value.Int("constructionReserve", 2)
                };
            }
        }

        private void ReadWeapons(string json)
        {
            Dictionary<string, object> root = JsonData.Object(RuntimeJson.Parse(json));
            foreach (KeyValuePair<string, object> pair in root)
            {
                Dictionary<string, object> value = JsonData.Object(pair.Value);
                if (!value.ContainsKey("damage")) continue;
                Weapons[pair.Key] = new WeaponDefinition
                {
                    Id = value.String("id", pair.Key), Label = value.String("label", pair.Key), ProjectileClass = value.String("projectileClass", InferProjectile(pair.Key)),
                    Damage = value.Float("damage"), Penetration = value.Float("penetration"), Range = value.Float("range"), RateOfFire = value.Float("rateOfFire", 1f),
                    MagazineSize = value.Int("magazineSize"), ReloadTime = value.Float("reloadTime"), ProjectileSpeed = value.Float("projectileSpeed"),
                    Accuracy = value.Float("accuracy", 1f), Precision = value.Float("precision", 1f), Suppression = value.Float("suppression"),
                    HeatPerShot = value.Float("heatPerShot"), CoolRate = value.Float("coolRate"), MaximumHeat = value.Float("maxHeat", 1f), SplashRadius = value.Float("splashRadius")
                };
            }
        }

        private void ReadProjectiles(string json)
        {
            Dictionary<string, object> root = JsonData.Object(RuntimeJson.Parse(json));
            foreach (KeyValuePair<string, object> pair in root.Child("classes"))
            {
                Dictionary<string, object> value = JsonData.Object(pair.Value);
                var definition = new ProjectileDefinition { Id = pair.Key, Speed = value.Float("speed", 245f) };
                foreach (KeyValuePair<string, object> flag in value.Child("flags")) if (Convert.ToBoolean(flag.Value)) definition.Flags.Add(flag.Key);
                Projectiles[pair.Key] = definition;
            }
        }

        private void ReadMapPresets(string json)
        {
            Dictionary<string, object> root = JsonData.Object(RuntimeJson.Parse(json));
            foreach (KeyValuePair<string, object> pair in root.Child("presets"))
            {
                Dictionary<string, object> value = JsonData.Object(pair.Value);
                var preset = new EconomicMapPresetDefinition { Id = pair.Key };
                foreach (object item in value.Children("resourceZones"))
                {
                    Dictionary<string, object> zoneValue = JsonData.Object(item);
                    var zone = new MapResourceZoneDefinition
                    {
                        Id = zoneValue.String("id"), Name = zoneValue.String("name"), ResourceType = zoneValue.String("resourceType"),
                        Capacity = zoneValue.Float("capacity", 1000f), Infinite = zoneValue.Bool("infinite"), GatherRate = zoneValue.Float("gatherRate", 10f),
                        Regeneration = zoneValue.Float("regeneration"), RequiresBuilding = zoneValue.Bool("requiresBuilding")
                    };
                    ReadPoints(zoneValue.Children("points"), zone.NormalizedPoints);
                    preset.ResourceZones.Add(zone);
                }
                foreach (object item in value.Children("economicNodes"))
                {
                    Dictionary<string, object> node = JsonData.Object(item);
                    preset.EconomicNodes.Add(new MapEconomicNodeDefinition { Id = node.String("id"), Name = node.String("name"), Type = node.String("type"), NormalizedPosition = new Vector2(node.Float("x"), node.Float("y")) });
                }
                foreach (object item in value.Children("tradeRoutes"))
                {
                    Dictionary<string, object> routeValue = JsonData.Object(item);
                    var route = new MapTradeRouteDefinition
                    {
                        Id = routeValue.String("id"), Name = routeValue.String("name"), Type = routeValue.String("type"), FromNodeId = routeValue.String("fromNodeId"),
                        ToNodeId = routeValue.String("toNodeId"), Capacity = routeValue.Float("capacity", 100f), RoadRequired = routeValue.Bool("roadRequired"), Bidirectional = routeValue.Bool("bidirectional", true)
                    };
                    ReadStrings(routeValue.Children("resources"), route.Resources);
                    ReadStrings(routeValue.Children("allowedFactions"), route.AllowedFactions);
                    ReadPoints(routeValue.Children("points"), route.NormalizedPoints);
                    preset.TradeRoutes.Add(route);
                }
                MapPresets[pair.Key] = preset;
            }
        }

        private void EnsureFallbacks()
        {
            if (!Objectives.ContainsKey("annihilation")) Objectives["annihilation"] = new BattleObjectiveDefinition { Id = "annihilation", Name = "Annihilation", Category = "elimination", Metric = "enemyElimination", Threshold = 1f };
            if (!FactionAi.ContainsKey("Space Marines")) FactionAi["Space Marines"] = new FactionAiDefinition { Id = "Space Marines" };
            if (!BuilderPolicies.ContainsKey("space_marines")) BuilderPolicies["space_marines"] = new BuilderPolicyDefinition { Id = "space_marines", BuilderName = "Servitor", StartingMinimum = 2, StartingMaximum = 4, GrowthMultiplier = 2, HardCap = 8, ReplaceDead = true };
        }

        private static void ReadFloatMap(Dictionary<string, object> source, Dictionary<string, float> target)
        {
            foreach (KeyValuePair<string, object> pair in source) target[pair.Key] = Convert.ToSingle(pair.Value);
        }

        private static void ReadStrings(List<object> source, List<string> target)
        {
            for (int i = 0; i < source.Count; i++) target.Add(Convert.ToString(source[i]));
        }

        private static void ReadPoints(List<object> source, List<Vector2> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                List<object> point = JsonData.Array(source[i]);
                if (point.Count >= 2) target.Add(new Vector2(Convert.ToSingle(point[0]), Convert.ToSingle(point[1])));
            }
        }

        private static string InferProjectile(string weapon)
        {
            string key = weapon.ToLowerInvariant();
            if (key.Contains("bolt")) return "BOLT";
            if (key.Contains("plasma")) return "PLASMA";
            if (key.Contains("melta")) return "MELTA";
            if (key.Contains("flame")) return "FLAME";
            if (key.Contains("rocket") || key.Contains("missile")) return "ROCKET";
            if (key.Contains("laser") || key.Contains("lascannon")) return "BEAM";
            return "BALLISTIC";
        }

        public static string NormalizeFactionKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return "space_marines";
            return value.Trim().ToLowerInvariant().Replace("'", "").Replace(" ", "_").Replace("-", "_");
        }
    }
}
