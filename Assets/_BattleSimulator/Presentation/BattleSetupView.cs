using System;
using System.Collections.Generic;
using BattleSimulator.Configuration;
using BattleSimulator.Core;
using BattleSimulator.Data;
using BattleSimulator.Simulation;
using UnityEngine;

namespace BattleSimulator.Presentation
{
    /// <summary>Unity-native setup screen backed by the same catalogs as the simulation.</summary>
    public sealed class BattleSetupView : MonoBehaviour
    {
        private static readonly Color[] Palette =
        {
            new Color(0.12f,0.35f,0.9f), new Color(0.2f,0.64f,0.17f), new Color(0.82f,0.18f,0.16f),
            new Color(0.62f,0.25f,0.8f), new Color(0.88f,0.62f,0.1f), new Color(0.1f,0.72f,0.72f),
            new Color(0.92f,0.32f,0.62f), new Color(0.78f,0.78f,0.82f), Color.white, Color.black
        };

        private BattleSimulationHost host;
        private SimulationClock clock;
        private BattleSetup draft;
        private readonly List<string> objectives = new List<string>();
        private Vector2 scroll;
        private bool visible;
        private string widthText = "1920", heightText = "1080", seedText = "742918";

        private void Awake()
        {
            host = GetComponent<BattleSimulationHost>();
            clock = GetComponent<SimulationClock>();
            draft = BattleSetup.CreateDefault(2);
            foreach (string key in BattleDataRepository.Instance.Objectives.Keys) objectives.Add(key);
            objectives.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void OnGUI()
        {
            if (!visible)
            {
                if (GUI.Button(new Rect(10f, Screen.height - 38f, 126f, 28f), "Battle setup")) Toggle();
                return;
            }
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            float panelWidth = Mathf.Min(1040f, Screen.width - 30f);
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, 18f, panelWidth, Screen.height - 36f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, 500f, 28f), "AUTONOMOUS WAR THEATER - BATTLE SETUP");
            float top = panel.y + 46f;
            GUI.Label(new Rect(panel.x + 18f, top, 70f, 24f), "Map");
            widthText = GUI.TextField(new Rect(panel.x + 88f, top, 74f, 24f), widthText);
            GUI.Label(new Rect(panel.x + 166f, top, 16f, 24f), "x");
            heightText = GUI.TextField(new Rect(panel.x + 184f, top, 74f, 24f), heightText);
            GUI.Label(new Rect(panel.x + 274f, top, 42f, 24f), "Seed");
            seedText = GUI.TextField(new Rect(panel.x + 318f, top, 92f, 24f), seedText);
            GUI.Label(new Rect(panel.x + 430f, top, 86f, 24f), $"Players {draft.Players.Count}");
            if (GUI.Button(new Rect(panel.x + 518f, top, 32f, 24f), "-")) SetPlayerCount(draft.Players.Count - 1);
            if (GUI.Button(new Rect(panel.x + 554f, top, 32f, 24f), "+")) SetPlayerCount(draft.Players.Count + 1);

            Rect content = new Rect(panel.x + 14f, top + 34f, panel.width - 28f, panel.height - 126f);
            float contentHeight = Mathf.Max(content.height, draft.Players.Count * 154f);
            scroll = GUI.BeginScrollView(content, scroll, new Rect(0f, 0f, content.width - 20f, contentHeight));
            for (int i = 0; i < draft.Players.Count; i++) DrawPlayer(draft.Players[i], i, new Rect(4f, i * 154f, content.width - 32f, 146f));
            GUI.EndScrollView();

            float bottom = panel.yMax - 42f;
            if (GUI.Button(new Rect(panel.x + 18f, bottom, 120f, 28f), "Cancel")) Toggle();
            if (GUI.Button(new Rect(panel.xMax - 178f, bottom, 160f, 28f), "Start / restart battle"))
            {
                if (float.TryParse(widthText, out float width)) draft.Width = Mathf.Clamp(width, 320f, 16384f);
                if (float.TryParse(heightText, out float height)) draft.Height = Mathf.Clamp(height, 180f, 16384f);
                if (int.TryParse(seedText, out int seed)) draft.Seed = seed;
                host.StartBattle(draft); visible = false;
            }
        }

        private void DrawPlayer(PlayerSetup player, int index, Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, 72f, 22f), $"P{index + 1}");
            player.PlayerName = GUI.TextField(new Rect(rect.x + 78f, rect.y + 6f, 160f, 24f), player.PlayerName);
            GUI.Label(new Rect(rect.x + 250f, rect.y + 8f, 44f, 22f), "Team");
            if (GUI.Button(new Rect(rect.x + 294f, rect.y + 6f, 32f, 24f), "-")) player.TeamId = Mathf.Max(1, player.TeamId - 1);
            GUI.Label(new Rect(rect.x + 330f, rect.y + 8f, 30f, 22f), player.TeamId.ToString());
            if (GUI.Button(new Rect(rect.x + 358f, rect.y + 6f, 32f, 24f), "+")) player.TeamId = Mathf.Min(12, player.TeamId + 1);
            GUI.Label(new Rect(rect.x + 410f, rect.y + 8f, 86f, 22f), "Spawn radius");
            player.SpawnRadius = GUI.HorizontalSlider(new Rect(rect.x + 500f, rect.y + 12f, 130f, 18f), player.SpawnRadius, 80f, 320f);
            GUI.Label(new Rect(rect.x + 636f, rect.y + 8f, 50f, 22f), $"{player.SpawnRadius:0}px");

            GUI.Label(new Rect(rect.x + 10f, rect.y + 42f, 54f, 22f), "Faction");
            if (GUI.Button(new Rect(rect.x + 64f, rect.y + 38f, 186f, 26f), player.FactionId)) CycleFaction(player);
            GUI.Label(new Rect(rect.x + 266f, rect.y + 42f, 72f, 22f), "Subfaction");
            if (GUI.Button(new Rect(rect.x + 340f, rect.y + 38f, 205f, 26f), player.Subfaction)) CycleSubfaction(player);
            GUI.Label(new Rect(rect.x + 560f, rect.y + 42f, 68f, 22f), "Objective");
            if (GUI.Button(new Rect(rect.x + 628f, rect.y + 38f, 210f, 26f), BattleDataRepository.Instance.Objective(player.BattleObjective).Name)) CycleObjective(player);

            Color[] colors = { player.PrimaryColor, player.SecondaryColor, player.AccentColor, player.BodyColor };
            string[] labels = { "Primary", "Secondary", "Accent", "Body" };
            for (int c = 0; c < 4; c++)
            {
                float x = rect.x + 10f + c * 168f;
                GUI.Label(new Rect(x, rect.y + 78f, 76f, 22f), labels[c]);
                Color before = GUI.backgroundColor; GUI.backgroundColor = colors[c];
                if (GUI.Button(new Rect(x + 78f, rect.y + 74f, 70f, 28f), Hex(colors[c]))) SetColor(player, c, NextColor(colors[c]));
                GUI.backgroundColor = before;
            }
            GUI.Label(new Rect(rect.x + 10f, rect.y + 112f, rect.width - 20f, 22f), $"Battle method: {ObjectiveMethod(player.FactionId)}");
        }

        private void Toggle() { visible = !visible; clock?.SetPaused(visible); }
        private void SetPlayerCount(int count) { count = Mathf.Clamp(count, 2, 12); while (draft.Players.Count > count) draft.Players.RemoveAt(draft.Players.Count - 1); while (draft.Players.Count < count) draft.Players.Add(BattleSetup.CreateDefault(count).Players[draft.Players.Count]); }
        private static void CycleFaction(PlayerSetup player) { int i = Array.IndexOf(FactionCatalog.FactionIds, player.FactionId); player.FactionId = FactionCatalog.FactionIds[(i + 1 + FactionCatalog.FactionIds.Length) % FactionCatalog.FactionIds.Length]; FactionDefinition f = FactionCatalog.For(player.FactionId); player.Subfaction = f.Subfactions[0]; }
        private static void CycleSubfaction(PlayerSetup player) { FactionDefinition f = FactionCatalog.For(player.FactionId); int i = f.Subfactions.IndexOf(player.Subfaction); player.Subfaction = f.Subfactions[(i + 1 + f.Subfactions.Count) % f.Subfactions.Count]; }
        private void CycleObjective(PlayerSetup player) { int i = objectives.IndexOf(player.BattleObjective); player.BattleObjective = objectives[(i + 1 + objectives.Count) % objectives.Count]; }
        private static Color NextColor(Color current) { int best = 0; float d = float.PositiveInfinity; for (int i = 0; i < Palette.Length; i++) { Color delta = Palette[i] - current; float q = delta.r * delta.r + delta.g * delta.g + delta.b * delta.b + delta.a * delta.a; if (q < d) { d = q; best = i; } } return Palette[(best + 1) % Palette.Length]; }
        private static void SetColor(PlayerSetup p, int slot, Color color) { if (slot == 0) p.PrimaryColor = color; else if (slot == 1) p.SecondaryColor = color; else if (slot == 2) p.AccentColor = color; else p.BodyColor = color; }
        private static string Hex(Color color) { Color32 c = color; return $"#{c.r:X2}{c.g:X2}{c.b:X2}"; }
        private static string ObjectiveMethod(string faction) { switch (faction) { case "Space Marines": return "elite strike and rapid reinforcement"; case "Imperial Guard": return "fortified combined-arms front"; case "Chaos": return "multi-front corruption then concentration"; case "Orks": return "mob expansion and Waaagh escalation"; case "Necrons": return "persistent secured advance"; case "Tau": return "reconnaissance and overlapping fire"; case "Tyranids": return "synapse and biomass encirclement"; default: return "adaptive objective operation"; } }
    }
}
