using System;
using System.Collections.Generic;
using BattleSimulator.Simulation;

namespace BattleSimulator.Data
{
    public sealed class EconomyProfileDefinition
    {
        public string Id;
        public readonly Dictionary<ResourceType, float> StartingStockpile = new Dictionary<ResourceType, float>();
        public readonly Dictionary<ResourceType, float> Priorities = new Dictionary<ResourceType, float>();
        public readonly HashSet<ResourceType> ActiveResources = new HashSet<ResourceType>();
        public readonly HashSet<ResourceType> ProducibleResources = new HashSet<ResourceType>();
        public float Priority(ResourceType type) => Priorities.TryGetValue(type, out float value) ? value : ActiveResources.Contains(type) ? 1f : 0f;
    }

    public static class EconomyCatalog
    {
        private static readonly Dictionary<string, EconomyProfileDefinition> profiles = Build();
        public static EconomyProfileDefinition For(string faction)
        {
            if (faction != null && profiles.TryGetValue(faction, out EconomyProfileDefinition profile)) return profile;
            return profiles["Space Marines"];
        }

        public static bool TryResource(string value, out ResourceType resource)
        {
            string normalized = (value ?? string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
            foreach (ResourceType candidate in Enum.GetValues(typeof(ResourceType)))
            {
                if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase)) { resource = candidate; return true; }
            }
            resource = ResourceType.Materials;
            return false;
        }

        private static Dictionary<string, EconomyProfileDefinition> Build()
        {
            var result = new Dictionary<string, EconomyProfileDefinition>(StringComparer.OrdinalIgnoreCase);
            result["Space Marines"] = Profile("space-marines",
                Stock((ResourceType.Requisition,1100),(ResourceType.Materials,750),(ResourceType.Fuel,520),(ResourceType.Energy,580),(ResourceType.Influence,320),(ResourceType.Parts,420),(ResourceType.Ammunition,900),(ResourceType.Medical,280),(ResourceType.Food,260)),
                new[] { ResourceType.Materials, ResourceType.Fuel, ResourceType.Energy, ResourceType.Food }, Priority((ResourceType.Food,1.3f),(ResourceType.Fuel,1.15f),(ResourceType.Materials,1.1f)));
            result["Imperial Guard"] = Profile("imperial-guard",
                Stock((ResourceType.Requisition,1600),(ResourceType.Materials,1100),(ResourceType.Fuel,1000),(ResourceType.Energy,700),(ResourceType.Food,1300),(ResourceType.Parts,650),(ResourceType.Ammunition,1900),(ResourceType.Medical,700)),
                new[] { ResourceType.Materials, ResourceType.Fuel, ResourceType.Energy, ResourceType.Food }, Priority((ResourceType.Food,1.45f),(ResourceType.Fuel,1.3f),(ResourceType.Ammunition,1.2f)));
            result["Adeptus Mechanicus"] = Profile("adeptus-mechanicus",
                Stock((ResourceType.Requisition,1300),(ResourceType.Materials,1150),(ResourceType.Fuel,700),(ResourceType.Energy,1450),(ResourceType.Influence,360),(ResourceType.Parts,1050),(ResourceType.Ammunition,1200),(ResourceType.Medical,240),(ResourceType.Food,380)),
                new[] { ResourceType.Materials, ResourceType.Fuel, ResourceType.Energy, ResourceType.Food }, Priority((ResourceType.Energy,1.55f),(ResourceType.Parts,1.4f),(ResourceType.Materials,1.25f)));
            result["Chaos"] = Profile("chaos",
                Stock((ResourceType.Requisition,1100),(ResourceType.Materials,800),(ResourceType.Fuel,600),(ResourceType.Energy,700),(ResourceType.Parts,450),(ResourceType.Ammunition,1100),(ResourceType.Food,420)),
                new[] { ResourceType.Materials, ResourceType.Fuel, ResourceType.Energy, ResourceType.Food }, Priority((ResourceType.Food,1.3f),(ResourceType.Fuel,1.2f),(ResourceType.Materials,1.1f)));
            result["Orks"] = Profile("orks", Stock((ResourceType.Scrap,1600),(ResourceType.Fuel,850),(ResourceType.Food,750),(ResourceType.Ammunition,1200)),
                new[] { ResourceType.Scrap, ResourceType.Fuel, ResourceType.Food }, Priority((ResourceType.Scrap,2f),(ResourceType.Food,1.35f),(ResourceType.Fuel,1.15f)));
            result["Necrons"] = Profile("necrons", Stock((ResourceType.Energy,1800),(ResourceType.Materials,900),(ResourceType.Food,260)),
                new[] { ResourceType.Energy, ResourceType.Materials, ResourceType.Food }, Priority((ResourceType.Energy,1.8f),(ResourceType.Materials,1.25f),(ResourceType.Food,1.1f)));
            result["Tau"] = Profile("tau", Stock((ResourceType.Requisition,1300),(ResourceType.Materials,1000),(ResourceType.Fuel,750),(ResourceType.Energy,1200),(ResourceType.Influence,350),(ResourceType.Parts,800),(ResourceType.Ammunition,1300),(ResourceType.Medical,380),(ResourceType.Food,520)),
                new[] { ResourceType.Materials, ResourceType.Fuel, ResourceType.Energy, ResourceType.Food }, Priority((ResourceType.Food,1.3f),(ResourceType.Energy,1.2f),(ResourceType.Fuel,1.15f)));
            result["Tyranids"] = Profile("tyranids", Stock((ResourceType.Biomass,2400),(ResourceType.Food,420)),
                new[] { ResourceType.Biomass, ResourceType.Food }, Priority((ResourceType.Biomass,2.1f),(ResourceType.Food,1.25f)));
            return result;
        }

        private static EconomyProfileDefinition Profile(string id, Dictionary<ResourceType,float> stock, ResourceType[] producible, Dictionary<ResourceType,float> priorities)
        {
            var result = new EconomyProfileDefinition { Id = id };
            foreach (KeyValuePair<ResourceType,float> pair in stock) { result.StartingStockpile[pair.Key] = pair.Value; result.ActiveResources.Add(pair.Key); }
            for (int i = 0; i < producible.Length; i++) result.ProducibleResources.Add(producible[i]);
            foreach (ResourceType type in result.ActiveResources) result.Priorities[type] = priorities.TryGetValue(type, out float value) ? value : 1f;
            return result;
        }
        private static Dictionary<ResourceType,float> Stock(params (ResourceType type,float amount)[] values) { var d=new Dictionary<ResourceType,float>(); for(int i=0;i<values.Length;i++) d[values[i].type]=values[i].amount; return d; }
        private static Dictionary<ResourceType,float> Priority(params (ResourceType type,float amount)[] values) => Stock(values);
    }
}
