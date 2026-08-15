using System;
using System.Collections.Generic;
using BattleSimulator.Data;
using UnityEngine;

namespace BattleSimulator.Configuration
{
    [Serializable]
    public sealed class BattleSetup
    {
        public string Name = "Autonomous War Theater";
        public float Width = 1920f;
        public float Height = 1080f;
        public int Seed = 742918;
        public string EconomicPreset = "iron";
        public string ScalePreset = "battle";
        public readonly List<PlayerSetup> Players = new List<PlayerSetup>();

        public static BattleSetup CreateDefault(int playerCount = 2)
        {
            var setup = new BattleSetup();
            string[] factions = FactionCatalog.FactionIds;
            int count = Mathf.Clamp(playerCount, 2, 12);
            for (int i = 0; i < count; i++)
            {
                string faction = factions[i % factions.Length];
                FactionDefinition definition = FactionCatalog.For(faction);
                setup.Players.Add(new PlayerSetup
                {
                    Id = i + 1,
                    TeamId = i + 1,
                    PlayerName = $"Player {i + 1}",
                    FactionId = faction,
                    Subfaction = definition.Subfactions.Count > 0 ? definition.Subfactions[i % definition.Subfactions.Count] : faction,
                    BattleObjective = "annihilation",
                    PrimaryColor = DefaultColor(i),
                    SecondaryColor = Color.Lerp(DefaultColor(i), Color.white, 0.42f),
                    AccentColor = Color.Lerp(DefaultColor(i), Color.yellow, 0.55f),
                    BodyColor = Color.Lerp(DefaultColor(i), Color.black, 0.18f),
                    SpawnRadius = 160f
                });
            }
            return setup;
        }

        private static Color DefaultColor(int index)
        {
            Color[] colors =
            {
                new Color(0.12f, 0.35f, 0.9f), new Color(0.2f, 0.64f, 0.17f),
                new Color(0.82f, 0.18f, 0.16f), new Color(0.62f, 0.25f, 0.8f),
                new Color(0.88f, 0.62f, 0.1f), new Color(0.1f, 0.72f, 0.72f),
                new Color(0.92f, 0.32f, 0.62f), new Color(0.42f, 0.52f, 0.92f),
                new Color(0.68f, 0.46f, 0.22f), new Color(0.28f, 0.8f, 0.48f),
                new Color(0.78f, 0.78f, 0.82f), new Color(0.93f, 0.43f, 0.12f)
            };
            return colors[index % colors.Length];
        }
    }

    [Serializable]
    public sealed class PlayerSetup
    {
        public int Id;
        public int TeamId;
        public string PlayerName;
        public string FactionId;
        public string Subfaction;
        public string BattleObjective;
        public Color PrimaryColor;
        public Color SecondaryColor;
        public Color AccentColor;
        public Color BodyColor;
        public float SpawnRadius = 160f;
    }
}
