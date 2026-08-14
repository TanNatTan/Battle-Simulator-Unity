using BattleSimulator.Core;
using BattleSimulator.Simulation;
using UnityEngine;

namespace BattleSimulator.Presentation
{
    public sealed class BattleObserverView : MonoBehaviour
    {
        private BattleSimulationHost host;
        private SimulationClock clock;
        private Vector2 viewCenter;
        private float zoom = 1f;
        private bool showTerrain = true;
        private bool showMinimap = true;
        private float smoothedFps = 60f;
        private GUIStyle smallStyle;
        private GUIStyle titleStyle;

        private void Awake()
        {
            host = GetComponent<BattleSimulationHost>();
            clock = GetComponent<SimulationClock>();
        }

        private void Update()
        {
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            smoothedFps = Mathf.Lerp(smoothedFps, fps, 0.08f);
        }

        private void OnGUI()
        {
            if (host?.World == null) return;
            EnsureStyles();
            BattleWorld world = host.World;
            if (viewCenter == Vector2.zero) viewCenter = new Vector2(world.Width * 0.5f, world.Height * 0.5f);
            Rect map = new Rect(10f, 48f, Mathf.Max(400f, Screen.width - 280f), Mathf.Max(280f, Screen.height - 60f));
            DrawTopBar(world);
            DrawBattlefield(world, map);
            DrawSidebar(world, new Rect(Screen.width - 260f, 48f, 250f, Screen.height - 60f));
            HandleMapInput(world, map);
        }

        private void DrawTopBar(BattleWorld world)
        {
            GUI.Box(new Rect(0f, 0f, Screen.width, 42f), GUIContent.none);
            GUI.Label(new Rect(12f, 8f, 260f, 28f), "W40K AUTONOMOUS WAR THEATER — UNITY", titleStyle);
            float x = 310f;
            string pauseLabel = clock.IsPaused ? "Resume" : "Pause";
            if (GUI.Button(new Rect(x, 8f, 72f, 26f), pauseLabel)) clock.SetPaused(!clock.IsPaused);
            x += 78f;
            float[] speeds = SimulationConstants.SupportedSpeeds;
            for (int i = 0; i < speeds.Length; i++)
            {
                if (GUI.Button(new Rect(x, 8f, 42f, 26f), $"{speeds[i]:0}x")) { clock.SetSimulationSpeed(speeds[i]); clock.SetPaused(false); }
                x += 46f;
            }
            if (GUI.Button(new Rect(x, 8f, 104f, 26f), showTerrain ? "Hide terrain" : "Show terrain")) showTerrain = !showTerrain;
            x += 110f;
            if (GUI.Button(new Rect(x, 8f, 104f, 26f), showMinimap ? "Hide minimap" : "Show minimap")) showMinimap = !showMinimap;
            GUI.Label(new Rect(Screen.width - 155f, 11f, 145f, 24f), $"FPS {smoothedFps:0}  Tick {world.Tick}", smallStyle);
        }

        private void DrawBattlefield(BattleWorld world, Rect map)
        {
            DrawRect(map, showTerrain ? new Color(0.09f, 0.14f, 0.1f) : new Color(0.025f, 0.025f, 0.025f));
            for (int i = 0; i < world.TerritoryCells.Count; i++)
            {
                TerritoryCellState cell = world.TerritoryCells[i];
                Color color = cell.OwnerId == 0 ? new Color(1f, 1f, 1f, 0.025f) : PlayerColor(world, cell.OwnerId, 0.1f);
                Rect rect = WorldRect(world, map, cell.Center, cell.Size);
                DrawRect(rect, color);
                DrawOutline(rect, new Color(1f, 1f, 1f, 0.08f));
            }
            for (int i = 0; i < world.ResourceZones.Count; i++)
            {
                ResourceZoneState zone = world.ResourceZones[i];
                Rect rect = WorldRect(world, map, zone.Position, Vector2.one * zone.Radius * 1.5f);
                DrawRect(rect, ResourceColor(zone.ResourceType));
                GUI.Label(rect, zone.ResourceType.ToString(), smallStyle);
            }
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                BuildingState building = world.Buildings[i];
                Rect rect = WorldRect(world, map, building.Position, Vector2.one * building.Radius * 2f);
                DrawRect(rect, PlayerColor(world, building.OwnerId, building.Operational ? 0.9f : 0.42f));
                GUI.Label(new Rect(rect.x, rect.y - 14f, Mathf.Max(60f, rect.width), 16f), building.Name, smallStyle);
            }
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (!unit.Active) continue;
                float size = unit.Role == UnitRole.Vehicle ? 10f : unit.Role == UnitRole.Aircraft ? 9f : 6f;
                Vector2 screen = WorldToScreen(world, map, unit.Position);
                Rect rect = new Rect(screen.x - size * 0.5f, screen.y - size * 0.5f, size, size);
                DrawRect(rect, unit.IsDead ? new Color(0.2f, 0.2f, 0.2f) : PlayerColor(world, unit.OwnerId, 1f));
            }
            for (int i = 0; i < world.Projectiles.Count; i++)
            {
                Vector2 screen = WorldToScreen(world, map, world.Projectiles[i].Position);
                DrawRect(new Rect(screen.x - 1f, screen.y - 1f, 3f, 3f), new Color(1f, 0.75f, 0.2f));
            }
        }

        private void DrawSidebar(BattleWorld world, Rect sidebar)
        {
            GUI.Box(sidebar, GUIContent.none);
            float y = sidebar.y + 10f;
            GUI.Label(new Rect(sidebar.x + 10f, y, 230f, 24f), $"TIME {world.Time:0.0}s   {clock.SimulationSpeed:0}x", titleStyle); y += 30f;
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                int units = 0, buildings = 0, territories = 0;
                for (int i = 0; i < world.Units.Count; i++) if (world.Units[i].Active && !world.Units[i].IsDead && world.Units[i].OwnerId == player.Id) units++;
                for (int i = 0; i < world.Buildings.Count; i++) if (world.Buildings[i].Active && world.Buildings[i].OwnerId == player.Id) buildings++;
                for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId == player.Id) territories++;
                GUI.color = player.PrimaryColor;
                GUI.Label(new Rect(sidebar.x + 10f, y, 230f, 20f), $"{player.Name} — {player.Subfaction}", titleStyle); GUI.color = Color.white; y += 22f;
                GUI.Label(new Rect(sidebar.x + 10f, y, 230f, 58f), $"Units {units}   Buildings {buildings}\nTerritories {territories}\nReq {player.Resource(ResourceType.Requisition):0}  Food {player.Resource(ResourceType.Food):0}", smallStyle); y += 66f;
            }
            if (showMinimap)
            {
                Rect mini = new Rect(sidebar.x + 10f, sidebar.yMax - 170f, 230f, 130f);
                DrawRect(mini, new Color(0.02f, 0.035f, 0.025f));
                for (int i = 0; i < world.Units.Count; i++)
                {
                    UnitState unit = world.Units[i];
                    if (!unit.Active || unit.IsDead) continue;
                    Vector2 point = new Vector2(mini.x + unit.Position.x / world.Width * mini.width, mini.yMax - unit.Position.y / world.Height * mini.height);
                    DrawRect(new Rect(point.x - 1f, point.y - 1f, 3f, 3f), PlayerColor(world, unit.OwnerId, 1f));
                }
                Rect camera = MinimapCameraRect(world, mini);
                DrawOutline(camera, Color.white);
                GUI.Label(new Rect(mini.x, mini.y - 22f, mini.width, 20f), $"MINIMAP  •  FPS {smoothedFps:0}", smallStyle);
                Event current = Event.current;
                if (current.type == EventType.MouseDown && current.button == 0 && mini.Contains(current.mousePosition))
                {
                    viewCenter = new Vector2((current.mousePosition.x - mini.x) / mini.width * world.Width, (mini.yMax - current.mousePosition.y) / mini.height * world.Height);
                    ClampView(world);
                    current.Use();
                }
            }
            if (world.BattleEnded) GUI.Label(new Rect(sidebar.x + 10f, sidebar.center.y - 20f, 230f, 40f), world.WinningTeamId == 0 ? "MUTUAL ANNIHILATION" : $"TEAM {world.WinningTeamId} VICTORY", titleStyle);
        }

        private void HandleMapInput(BattleWorld world, Rect map)
        {
            Event current = Event.current;
            if (!map.Contains(current.mousePosition)) return;
            if (current.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * (current.delta.y > 0f ? 0.88f : 1.14f), 1f, 5f);
                ClampView(world); current.Use();
            }
            else if (current.type == EventType.MouseDrag && (current.button == 1 || current.button == 2))
            {
                viewCenter += new Vector2(-current.delta.x / map.width * world.Width / zoom, current.delta.y / map.height * world.Height / zoom);
                ClampView(world); current.Use();
            }
        }

        private Vector2 WorldToScreen(BattleWorld world, Rect map, Vector2 position)
        {
            float visibleWidth = world.Width / zoom, visibleHeight = world.Height / zoom;
            float left = viewCenter.x - visibleWidth * 0.5f, bottom = viewCenter.y - visibleHeight * 0.5f;
            return new Vector2(map.x + (position.x - left) / visibleWidth * map.width, map.yMax - (position.y - bottom) / visibleHeight * map.height);
        }

        private Rect WorldRect(BattleWorld world, Rect map, Vector2 center, Vector2 size)
        {
            Vector2 screen = WorldToScreen(world, map, center);
            return new Rect(screen.x - size.x * map.width / (world.Width / zoom) * 0.5f, screen.y - size.y * map.height / (world.Height / zoom) * 0.5f, size.x * map.width / (world.Width / zoom), size.y * map.height / (world.Height / zoom));
        }

        private Rect MinimapCameraRect(BattleWorld world, Rect mini)
        {
            float width = mini.width / zoom, height = mini.height / zoom;
            return new Rect(mini.x + viewCenter.x / world.Width * mini.width - width * 0.5f, mini.yMax - viewCenter.y / world.Height * mini.height - height * 0.5f, width, height);
        }

        private void ClampView(BattleWorld world)
        {
            float halfWidth = world.Width / zoom * 0.5f, halfHeight = world.Height / zoom * 0.5f;
            viewCenter.x = Mathf.Clamp(viewCenter.x, halfWidth, world.Width - halfWidth);
            viewCenter.y = Mathf.Clamp(viewCenter.y, halfHeight, world.Height - halfHeight);
        }

        private Color PlayerColor(BattleWorld world, int ownerId, float alpha)
        {
            PlayerState player = world.GetPlayer(ownerId); Color color = player?.PrimaryColor ?? Color.gray; color.a = alpha; return color;
        }

        private static Color ResourceColor(ResourceType type)
        {
            switch (type) { case ResourceType.Food: return new Color(0.2f, 0.65f, 0.25f, 0.5f); case ResourceType.Fuel: return new Color(0.95f, 0.55f, 0.12f, 0.5f); case ResourceType.Scrap: return new Color(0.55f, 0.55f, 0.62f, 0.5f); default: return new Color(0.35f, 0.7f, 0.9f, 0.5f); }
        }

        private void EnsureStyles()
        {
            if (smallStyle != null) return;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.82f, 0.9f, 0.84f) } };
            titleStyle = new GUIStyle(smallStyle) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private static void DrawRect(Rect rect, Color color) { Color before = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before; }
        private static void DrawOutline(Rect rect, Color color) { DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color); DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color); DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color); DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color); }
    }
}
