using UnityEngine;

namespace BattleSimulator.Data
{
    /// <summary>
    /// Loads the original browser simulator's JSON balance catalogs from Resources.
    /// Keeping JSON authoritative lets Unity and the web build share the same values.
    /// </summary>
    public static class BattleDataCatalog
    {
        private const string Root = "BattleSimulatorData/data/";

        public static string LoadJson(string relativePathWithoutExtension)
        {
            string normalized = relativePathWithoutExtension.Replace('\\', '/').TrimStart('/');
            TextAsset asset = Resources.Load<TextAsset>(Root + normalized);
            if (asset == null)
            {
                Debug.LogWarning($"Battle data catalog not found: {normalized}.json");
                return string.Empty;
            }
            return asset.text;
        }

        public static string Weapons => LoadJson("weapons");
        public static string Projectiles => LoadJson("projectiles");
        public static string BattleObjectives => LoadJson("ai/battle-objectives");
        public static string FactionBranches => LoadJson("ai/faction-branches");
        public static string ProductionPlans => LoadJson("ai/subfaction-production-plans");
        public static string BuilderWorkforcePolicy => LoadJson("ai/builder-workforce-policy");
        public static string WarfareDoctrines => LoadJson("ai/warfare-doctrines");
        public static string EconomyCosts => LoadJson("economy/costs");
        public static string EconomyResources => LoadJson("economy/resources");
        public static string EconomicMapPresets => LoadJson("maps/economic-presets");
        public static string ArmyCompositions => LoadJson("ai/army-compositions");
        public static string BuildingDiversityPolicy => LoadJson("ai/building-diversity-policy");
        public static string WargearDoctrines => LoadJson("ai/wargear-doctrines");
    }
}
