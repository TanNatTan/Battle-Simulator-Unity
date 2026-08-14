using UnityEngine;

namespace BattleSimulator.Simulation
{
    public static class BattleScenarioFactory
    {
        public static BattleWorld CreateAutonomousBattle(int seed = 742918)
        {
            var world = new BattleWorld(1920f, 1080f, seed);
            PlayerState marines = CreatePlayer(1, 1, "Aurelius", "Imperium", "Space Marines", "Ultramarines", new Vector2(230f, 540f), new Color(0.12f, 0.35f, 0.9f), new Color(0.95f, 0.78f, 0.16f));
            PlayerState orks = CreatePlayer(2, 2, "Gorzag", "Orks", "Orks", "Goffs", new Vector2(1690f, 540f), new Color(0.2f, 0.64f, 0.17f), new Color(0.8f, 0.15f, 0.08f));
            world.AddPlayer(marines);
            world.AddPlayer(orks);
            AddStartingForce(world, marines, 3, 12);
            AddStartingForce(world, orks, 6, 16);
            AddTerritoryGrid(world, 8, 6);
            AddResourceZone(world, ResourceType.Food, new Vector2(720f, 300f), 72f);
            AddResourceZone(world, ResourceType.Fuel, new Vector2(960f, 540f), 72f);
            AddResourceZone(world, ResourceType.Scrap, new Vector2(1200f, 780f), 72f);
            AddResourceZone(world, ResourceType.Requisition, new Vector2(960f, 180f), 64f);
            world.RebuildSpatialIndex();
            return world;
        }

        private static PlayerState CreatePlayer(int id, int team, string name, string race, string faction, string subfaction, Vector2 spawn, Color primary, Color secondary)
        {
            var player = new PlayerState
            {
                Id = id,
                TeamId = team,
                Name = name,
                Race = race,
                Faction = faction,
                Subfaction = subfaction,
                SpawnOrigin = spawn,
                SpawnRadius = 160f,
                PrimaryColor = primary,
                SecondaryColor = secondary,
                AccentColor = Color.white,
                BodyColor = primary
            };
            player.AddResource(ResourceType.Requisition, 500f);
            player.AddResource(ResourceType.Materials, 500f);
            player.AddResource(ResourceType.Food, 200f);
            player.AddResource(ResourceType.Ammunition, 300f);
            return player;
        }

        private static void AddStartingForce(BattleWorld world, PlayerState player, int builders, int troopers)
        {
            BuildingState headquarters = world.AddEntity(BattleEntityFactory.CreateBuilding(player, BuildingType.Headquarters, player.SpawnOrigin));
            headquarters.Name = player.Race == "Orks" ? "Big Hut" : "Fortress Monastery";
            world.AddEntity(BattleEntityFactory.CreateBuilding(player, BuildingType.Warehouse, player.SpawnOrigin + new Vector2(0f, 55f)));
            for (int i = 0; i < builders; i++) AddUnit(world, player, UnitRole.Builder, i, builders, 55f);
            for (int i = 0; i < 2; i++) AddUnit(world, player, UnitRole.SupplyCarrier, i, 2, 78f);
            for (int i = 0; i < troopers; i++) AddUnit(world, player, i < 2 ? UnitRole.Scout : UnitRole.Trooper, i, troopers, 105f);
            AddUnit(world, player, UnitRole.Commander, 0, 1, 38f);
        }

        private static void AddUnit(BattleWorld world, PlayerState player, UnitRole role, int index, int total, float radius)
        {
            float angle = Mathf.PI * 2f * index / Mathf.Max(1, total);
            Vector2 position = player.SpawnOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            UnitState unit = world.AddEntity(BattleEntityFactory.CreateUnit(player, role, position));
            if (player.Race == "Orks") unit.Name = role == UnitRole.Builder ? "Gretchin" : role == UnitRole.Trooper ? "Slugga Boy" : unit.Name;
            else if (role == UnitRole.Builder) unit.Name = "Servitor";
            else if (role == UnitRole.Trooper) unit.Name = "Tactical Marine";
            else if (role == UnitRole.Scout) unit.Name = "Scout Marine";
        }

        private static void AddTerritoryGrid(BattleWorld world, int columns, int rows)
        {
            float width = world.Width / columns;
            float height = world.Height / rows;
            int id = 1;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                world.AddTerritory(new TerritoryCellState
                {
                    Id = id++,
                    Center = new Vector2((x + 0.5f) * width, (y + 0.5f) * height),
                    Size = new Vector2(width, height),
                    OwnerId = x == 0 ? 1 : x == columns - 1 ? 2 : 0
                });
            }
        }

        private static void AddResourceZone(BattleWorld world, ResourceType type, Vector2 center, float radius)
        {
            var zone = new ResourceZoneState
            {
                Name = $"{type} Zone",
                Kind = EntityKind.ResourceZone,
                Position = center,
                Radius = radius,
                ResourceType = type,
                Capacity = type == ResourceType.Food ? 50000f : 25000f,
                Remaining = type == ResourceType.Food ? 50000f : 25000f,
                GatherRate = type == ResourceType.Food ? 12f : 20f
            };
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 2f * i / 10f;
                zone.Polygon.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            world.AddEntity(zone);
        }
    }
}
