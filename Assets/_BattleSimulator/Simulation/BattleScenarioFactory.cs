using UnityEngine;
using BattleSimulator.Configuration;
using BattleSimulator.Data;

namespace BattleSimulator.Simulation
{
    public static class BattleScenarioFactory
    {
        public static BattleWorld Create(BattleSetup setup, BattleDataRepository data)
        {
            var world = new BattleWorld(setup.Width, setup.Height, setup.Seed);
            for (int i = 0; i < setup.Players.Count; i++)
            {
                PlayerSetup source = setup.Players[i];
                FactionDefinition faction = FactionCatalog.For(source.FactionId);
                Vector2 spawn = PerimeterSpawn(setup.Width, setup.Height, i, setup.Players.Count);
                var player = new PlayerState
                {
                    Id = source.Id > 0 ? source.Id : i + 1, TeamId = source.TeamId, Name = source.PlayerName,
                    Race = faction.Race, Faction = faction.Id, Subfaction = source.Subfaction,
                    BattleObjective = data.Objective(source.BattleObjective).Id, SpawnOrigin = spawn, SpawnRadius = source.SpawnRadius,
                    PrimaryColor = source.PrimaryColor, SecondaryColor = source.SecondaryColor, AccentColor = source.AccentColor, BodyColor = source.BodyColor
                };
                ApplyPlayerData(player, data);
                world.AddPlayer(player);
            }
            TerritoryGenerator.Generate(world, 8, 6);
            AssignStartingTerritory(world);
            EconomicMapPresetDefinition preset = data.MapPresets.TryGetValue(setup.EconomicPreset, out EconomicMapPresetDefinition selected) ? selected : null;
            if (preset != null) AddEconomicPreset(world, preset);
            else AddFallbackResources(world);
            for (int i = 0; i < world.Players.Count; i++) AddConfiguredStartingForce(world, world.Players[i], data);
            world.RebuildSpatialIndex();
            return world;
        }

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

        private static void ApplyPlayerData(PlayerState player, BattleDataRepository data)
        {
            EconomyProfileDefinition economy = EconomyCatalog.For(player.Faction);
            foreach (var pair in economy.StartingStockpile) player.Resources[pair.Key] = pair.Value;
            FactionAiDefinition ai = data.FactionProfile(player.Faction);
            FactionAiDefinition subfaction = data.SubfactionAi.TryGetValue(player.Subfaction, out FactionAiDefinition sub) ? sub : null;
            player.Aggression = ai.BehaviorValue("aggression"); player.Caution = ai.BehaviorValue("caution");
            player.Expansion = ai.BehaviorValue("expansion"); player.Economy = ai.BehaviorValue("economy");
            foreach (var pair in ai.Weights) player.StrategicWeights[pair.Key] = pair.Value;
            foreach (var pair in ai.Identity) player.IdentityWeights[pair.Key] = pair.Value;
            if (subfaction != null)
            {
                foreach (var pair in subfaction.Weights) player.StrategicWeights[pair.Key] = player.StrategicWeights.TryGetValue(pair.Key, out float value) ? value * pair.Value : pair.Value;
                foreach (var pair in subfaction.Identity) player.IdentityWeights[pair.Key] = pair.Value;
                if (subfaction.Behavior.Count > 0)
                {
                    player.Aggression = subfaction.BehaviorValue("aggression", player.Aggression); player.Caution = subfaction.BehaviorValue("caution", player.Caution);
                    player.Expansion = subfaction.BehaviorValue("expansion", player.Expansion); player.Economy = subfaction.BehaviorValue("economy", player.Economy);
                }
            }
            player.ForceCap = player.Faction == "Imperial Guard" ? 360 : player.Faction == "Orks" || player.Faction == "Tyranids" ? 400 : player.Faction == "Space Marines" ? 220 : 280;
            player.ObjectiveMethod = ObjectiveMethod(player);
        }

        private static string ObjectiveMethod(PlayerState player)
        {
            switch (player.Faction)
            {
                case "Space Marines": return "elite strike and rapid reinforcement";
                case "Imperial Guard": return "broad fortified front behind armor and artillery";
                case "Adeptus Mechanicus": return "secure valuable nodes then escalate machine strength";
                case "Chaos": return "shape several fronts, exploit losses, then concentrate";
                case "Orks": return "expand in several mobs and pile into the largest fight";
                case "Necrons": return "deliberate advance with persistent secured cells";
                case "Tau": return "reconnaissance and overlapping fire across secure corridors";
                case "Tyranids": return "spread synapse and biomass before massing at weak resistance";
                default: return "adaptive operation";
            }
        }

        private static void AddConfiguredStartingForce(BattleWorld world, PlayerState player, BattleDataRepository data)
        {
            float hqAngle = world.Random.Range(0f, Mathf.PI * 2f);
            Vector2 headquartersPosition = world.ClampToWorld(player.SpawnOrigin + new Vector2(Mathf.Cos(hqAngle), Mathf.Sin(hqAngle)) * world.Random.Range(20f, player.SpawnRadius * 0.55f), 24f);
            BuildingState headquarters = world.AddEntity(BattleEntityFactory.CreateBuilding(player, BuildingType.Headquarters, headquartersPosition, 1f, "HQ"));
            headquarters.TerritoryCellId = CellAt(world, headquarters.Position)?.Id ?? 0;
            BuilderPolicyDefinition policy = data.BuilderPolicy(player.Faction);
            int builderCount = world.Random.Range(policy.StartingMinimum, policy.StartingMaximum + 1);
            int troopers = player.Faction == "Space Marines" ? 20 : player.Faction == "Imperial Guard" ? 32 : player.Faction == "Orks" || player.Faction == "Tyranids" ? 36 : 24;
            FactionDefinition faction = FactionCatalog.For(player.Faction);
            for (int i = 0; i < builderCount; i++) AddConfiguredUnit(world, player, data, UnitRole.Builder, faction.RosterFor("builder"), i, builderCount, 54f, null);
            for (int i = 0; i < 2; i++) AddConfiguredUnit(world, player, data, UnitRole.SupplyCarrier, faction.RosterFor("supply"), i, 2, 72f, null);
            int squads = Mathf.CeilToInt(troopers / (player.Faction == "Space Marines" ? 10f : 8f));
            for (int s = 0; s < squads; s++)
            {
                var squad = world.AddSquad(new SquadState { OwnerId = player.Id, TeamId = player.TeamId, Name = $"{player.Subfaction} Squad {s + 1}", NominalSize = player.Faction == "Space Marines" ? 10 : 8, PrimaryRole = s == 0 ? SquadPrimaryRole.Capture : SquadPrimaryRole.Offensive });
                int members = Mathf.Min(squad.NominalSize, troopers - s * squad.NominalSize);
                for (int m = 0; m < members; m++) AddConfiguredUnit(world, player, data, m == 0 && s == 0 ? UnitRole.Scout : UnitRole.Trooper, m == 0 && s == 0 ? faction.RosterFor("scout") : faction.RosterFor("trooper"), s * squad.NominalSize + m, troopers, 95f + s * 10f, squad);
            }
            AddConfiguredUnit(world, player, data, UnitRole.Commander, faction.RosterFor("commander"), 0, 1, 36f, world.Squads.Count > 0 ? world.Squads[world.Squads.Count - squads] : null);
        }

        private static UnitState AddConfiguredUnit(BattleWorld world, PlayerState player, BattleDataRepository data, UnitRole role, string[] roster, int index, int total, float radius, SquadState squad)
        {
            float angle = Mathf.PI * 2f * index / Mathf.Max(1, total);
            Vector2 position = world.ClampToWorld(player.SpawnOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, 5f);
            string specialty = roster.Length > 0 ? roster[index % roster.Length] : role.ToString();
            UnitState unit = world.AddEntity(BattleEntityFactory.CreateUnit(player, role, position, specialty, data));
            unit.Name = specialty;
            if (squad != null)
            {
                if (role == UnitRole.Commander) { unit.AttachedSquadId = squad.Id; squad.AttachedCharacterIds.Add(unit.Id); }
                else { unit.SquadId = squad.Id; squad.MemberIds.Add(unit.Id); }
            }
            return unit;
        }

        private static void AddEconomicPreset(BattleWorld world, EconomicMapPresetDefinition preset)
        {
            for (int i = 0; i < preset.ResourceZones.Count; i++)
            {
                MapResourceZoneDefinition source = preset.ResourceZones[i];
                if (!EconomyCatalog.TryResource(source.ResourceType, out ResourceType resource)) continue;
                var zone = new ResourceZoneState { Name = source.Name, Kind = EntityKind.ResourceZone, ResourceType = resource, Capacity = source.Infinite ? float.PositiveInfinity : source.Capacity, Remaining = source.Infinite ? float.PositiveInfinity : source.Capacity, GatherRate = source.GatherRate, RegenerationRate = source.Regeneration };
                for (int p = 0; p < source.NormalizedPoints.Count; p++) zone.Polygon.Add(new Vector2(source.NormalizedPoints[p].x * world.Width, source.NormalizedPoints[p].y * world.Height));
                zone.Position = PolygonCenter(zone.Polygon); zone.Radius = 64f; world.AddEntity(zone);
            }
            for (int i = 0; i < preset.EconomicNodes.Count; i++)
            {
                MapEconomicNodeDefinition source = preset.EconomicNodes[i];
                world.AddEntity(new EconomicNodeState { Name = source.Name, Specialty = source.Id, NodeType = source.Type, Kind = EntityKind.EconomicNode, Position = new Vector2(source.NormalizedPosition.x * world.Width, source.NormalizedPosition.y * world.Height), Radius = 26f, HitPoints = 800f, MaximumHitPoints = 800f, Capacity = 2000f, StrategicValue = 80f });
            }
            for (int i = 0; i < preset.TradeRoutes.Count; i++)
            {
                MapTradeRouteDefinition source = preset.TradeRoutes[i];
                var route = new TradeRouteState { Id = source.Id, Name = source.Name, Type = source.Type, FromNodeId = source.FromNodeId, ToNodeId = source.ToNodeId, Capacity = source.Capacity, RoadRequired = source.RoadRequired, Bidirectional = source.Bidirectional };
                for (int r = 0; r < source.Resources.Count; r++) if (EconomyCatalog.TryResource(source.Resources[r], out ResourceType resource)) route.Resources.Add(resource);
                route.AllowedFactions.AddRange(source.AllowedFactions);
                for (int p = 0; p < source.NormalizedPoints.Count; p++) route.Points.Add(new Vector2(source.NormalizedPoints[p].x * world.Width, source.NormalizedPoints[p].y * world.Height));
                world.AddTradeRoute(route);
            }
        }

        private static void AddFallbackResources(BattleWorld world)
        {
            AddResourceZone(world, ResourceType.Food, new Vector2(world.Width * 0.375f, world.Height * 0.28f), 72f);
            AddResourceZone(world, ResourceType.Fuel, new Vector2(world.Width * 0.5f, world.Height * 0.5f), 72f);
            AddResourceZone(world, ResourceType.Scrap, new Vector2(world.Width * 0.625f, world.Height * 0.72f), 72f);
            AddResourceZone(world, ResourceType.Biomass, new Vector2(world.Width * 0.5f, world.Height * 0.78f), 72f);
        }

        private static void AssignStartingTerritory(BattleWorld world)
        {
            for (int c = 0; c < world.TerritoryCells.Count; c++)
            {
                TerritoryCellState cell = world.TerritoryCells[c];
                float best = float.PositiveInfinity; int owner = 0;
                for (int p = 0; p < world.Players.Count; p++)
                {
                    float distance = Vector2.Distance(cell.Center, world.Players[p].SpawnOrigin);
                    if (distance < best && distance <= world.Players[p].SpawnRadius * 1.35f) { best = distance; owner = world.Players[p].Id; }
                }
                cell.OwnerId = owner;
            }
        }

        private static Vector2 PerimeterSpawn(float width, float height, int index, int count)
        {
            float perimeter = 2f * (width + height), distance = (index + 0.5f) / count * perimeter;
            const float margin = 95f;
            if (distance < width) return new Vector2(Mathf.Clamp(distance, margin, width - margin), margin);
            distance -= width; if (distance < height) return new Vector2(width - margin, Mathf.Clamp(distance, margin, height - margin));
            distance -= height; if (distance < width) return new Vector2(width - Mathf.Clamp(distance, margin, width - margin), height - margin);
            distance -= width; return new Vector2(margin, height - Mathf.Clamp(distance, margin, height - margin));
        }

        private static TerritoryCellState CellAt(BattleWorld world, Vector2 point) { for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].Contains(point)) return world.TerritoryCells[i]; return null; }
        private static Vector2 PolygonCenter(System.Collections.Generic.List<Vector2> polygon) { Vector2 total = Vector2.zero; for (int i = 0; i < polygon.Count; i++) total += polygon[i]; return polygon.Count > 0 ? total / polygon.Count : Vector2.zero; }

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
