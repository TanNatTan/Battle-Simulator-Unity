using System;
using System.Collections.Generic;

namespace BattleSimulator.Data
{
    public sealed class FactionDefinition
    {
        public string Id;
        public string Race;
        public string Deployment;
        public readonly Dictionary<string, string> Buildings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string[]> Roster = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Subfactions = new List<string>();

        public string[] RosterFor(string role)
        {
            return Roster.TryGetValue(role, out string[] values) ? values : Array.Empty<string>();
        }

        public string BuildingLabel(string type)
        {
            return Buildings.TryGetValue(type, out string value) ? value : type;
        }
    }

    public static class FactionCatalog
    {
        public static readonly string[] FactionIds =
        {
            "Space Marines", "Imperial Guard", "Adeptus Mechanicus", "Chaos", "Orks", "Necrons", "Tau", "Tyranids"
        };

        private static readonly Dictionary<string, FactionDefinition> definitions = Build();

        public static FactionDefinition For(string id)
        {
            if (id != null && definitions.TryGetValue(id, out FactionDefinition definition)) return definition;
            return definitions["Space Marines"];
        }

        private static Dictionary<string, FactionDefinition> Build()
        {
            var result = new Dictionary<string, FactionDefinition>(StringComparer.OrdinalIgnoreCase);
            Add(result, "Space Marines", "Imperium", "Drop Pods, Thunderhawks, teleportation",
                new[] { "Ultramarines", "Blood Angels", "Imperial Fists", "Salamanders", "Emerald Suns", "White Scars", "Raven Guard", "Iron Hands", "Space Wolves", "Black Templars" },
                Buildings("Fortress Monastery", "Chapter Barracks", "Armoury", "Librarius", "Apothecarion", "Plasma Reactor", "Supply Depot", "Manufactorum", "Landing Pad", "Listening Post", "Fortress Wall", "Heavy Bolter Turret"),
                Roster(
                    ("builder", A("Servitor")),
                    ("supply", A("Chapter Supply Servitor", "Rhino Supply Carrier")),
                    ("trooper", A("Tactical Marine", "Intercessor", "Heavy Intercessor", "Assault Intercessor", "Assault Marine", "Jump Pack Intercessor", "Devastator", "Hellblaster", "Eradicator", "Inceptor", "Aggressor", "Reiver", "Incursor", "Infiltrator")),
                    ("scout", A("Scout Marine", "Skull Probe", "Eliminator", "Infiltrator")),
                    ("medic", A("Apothecary")), ("engineer", A("Techmarine")),
                    ("commander", A("Sergeant", "Lieutenant", "Captain", "Chapter Master", "Chaplain", "Librarian", "Judiciar")),
                    ("standard", A("Ancient", "Company Champion", "Bladeguard Veteran", "Sternguard Veteran", "Vanguard Veteran", "Terminator", "Assault Terminator")),
                    ("vehicle", A("Rhino", "Razorback", "Impulsor", "Repulsor", "Land Raider", "Predator", "Gladiator", "Vindicator", "Whirlwind", "Hunter", "Stalker", "Storm Speeder", "Invader ATV", "Dreadnought", "Redemptor Dreadnought", "Ballistus Dreadnought", "Brutalis Dreadnought", "Thunderhawk", "Stormraven", "Stormtalon", "Stormhawk"))));
            Add(result, "Imperial Guard", "Imperium", "Ground deployment, convoys, Valkyries",
                new[] { "Cadian 8th", "Steel Legion", "Tempestus Scions", "Death Korps of Krieg", "Catachan Jungle Fighters", "Tallarn Desert Raiders" },
                Buildings("Command Headquarters", "Barracks", "Manufactorum", "Tactica Command", "Field Hospital", "Generatorium", "Supply Warehouse", "Promethium Refinery", "Valkyrie Landing Pad", "Vox Relay", "Bunker Network", "Heavy Weapons Nest"),
                Roster(("builder", A("Combat Engineer")), ("supply", A("Munitorum Cargo Carrier", "Trojan Support Vehicle")),
                    ("trooper", A("Guardsman", "Shock Trooper", "Heavy Weapons Team", "Kasrkin", "Tempestus Scion", "Ogryn", "Bullgryn")),
                    ("scout", A("Ratling", "Sentinel Scout")), ("medic", A("Field Medic")), ("engineer", A("Combat Engineer")),
                    ("commander", A("Officer", "Commissar", "Priest")), ("standard", A("Regimental Standard")),
                    ("vehicle", A("Chimera", "Taurox", "Sentinel", "Hellhound", "Basilisk", "Manticore", "Hydra", "Leman Russ", "Rogal Dorn", "Baneblade"))));
            Add(result, "Adeptus Mechanicus", "Imperium", "Forge-world cohort, armored crawler columns, and noospheric relays",
                new[] { "Mars Forge", "Ryza Forge", "Lucius Forge", "Graia Forge", "Stygies VIII Forge" },
                Buildings("Forge Temple", "Skitarii Maniple Foundry", "Cybernetica Workshop", "Noosphere Archive", "Tech-Priest Reclamation Bay", "Plasma Generatorium", "Forge Vault", "Factorum Refinery", "Macro-Lander Pad", "Noospheric Relay", "Aegis Bulwark", "Onager Defense Battery"),
                Roster(("builder", A("Tech-Priest Enginseer", "Construction Servitor")), ("supply", A("Servitor Cargo Cohort", "Triaros Supply Crawler")),
                    ("trooper", A("Skitarii Ranger", "Skitarii Vanguard", "Kataphron Breacher", "Sicarian Infiltrator")),
                    ("scout", A("Sydonian Dragoon", "Pteraxii Skystalker")), ("medic", A("Tech-Priest Reclamation Adept")), ("engineer", A("Tech-Priest Enginseer")),
                    ("commander", A("Skitarii Alpha", "Tech-Priest Dominus", "Tech-Priest Manipulus")), ("standard", A("Data-Tether Bearer")),
                    ("vehicle", A("Onager Dunecrawler", "Skorpius Dunerider", "Skorpius Disintegrator", "Kastelan Robot"))));
            Add(result, "Chaos", "Chaos", "Warp beacons, corrupted drop pods, summoning",
                new[] { "Black Legion", "Word Bearers", "Iron Warriors", "Night Lords", "Alpha Legion", "Emperor's Children", "World Eaters", "Death Guard", "Thousand Sons", "Khorne Host", "Tzeentch Coven", "Nurgle Host", "Slaanesh Host" },
                Buildings("Dark Citadel", "Cult Mustering Hall", "Armoury of Damnation", "Forbidden Archive", "Sacrificial Shrine", "Warp Nexus", "Ammunition Cache", "Dark Forge", "Warp Beacon", "Corruption Spire", "Chaos Bastion", "Daemon Gun Platform"),
                Roster(("builder", A("Dark Servitor", "Cult Laborer")), ("supply", A("Traitor Cargo Hauler", "Daemon-bound Supply Carrier")),
                    ("trooper", A("Cultist", "Chaos Space Marine", "Havoc", "Chosen", "Possessed", "Noise Marine", "Plague Marine", "Rubric Marine", "Khorne Berzerker")),
                    ("scout", A("Skull Probe", "Raptor", "Warp Talon")), ("medic", A("Dark Apostle")), ("engineer", A("Warpsmith")),
                    ("commander", A("Chaos Lord", "Sorcerer", "Exalted Champion")), ("standard", A("Icon Bearer")),
                    ("vehicle", A("Chaos Rhino", "Predator", "Land Raider", "Defiler", "Maulerfiend", "Forgefiend", "Heldrake", "Venomcrawler"))));
            Add(result, "Orks", "Orks", "Spore patches, ramshackle camps, mobs and Trukks",
                new[] { "Ironjaw Mob", "Speed Freeks", "Freebooter Fleet", "Goff Mob", "Bad Moon Mob", "Deathskull Mob" },
                Buildings("Boss Hut", "Boyz Hut", "Mek Shop", "Big Mek's Workshop", "Painboy Hut", "Kustom Generator", "Dakka Dump", "Lootin' Yard", "Tellyporta Pad", "Watcha Tower", "Waaagh! Banner", "Big Gunz Nest"),
                Roster(("builder", A("Gretchin")), ("supply", A("Loot Trukk", "Grot Scrap Hauler")),
                    ("trooper", A("Gretchin", "Boy", "Shoota Boy", "Slugga Boy", "Burna Boy", "Tankbusta", "Loota", "Nob", "Flash Git", "Meganob")),
                    ("scout", A("Kommando")), ("medic", A("Painboy")), ("engineer", A("Mekboy", "Big Mek")),
                    ("commander", A("Boss Nob", "Warboss", "Weirdboy")), ("standard", A("Waaagh! Banner Nob")),
                    ("vehicle", A("Trukk", "Battlewagon", "Deff Dread", "Killa Kan", "Looted Wagon", "Scrapjet", "Rukkatrukk Squigbuggy", "Dakkajet"))));
            Add(result, "Necrons", "Necrons", "Reanimation, portals, teleportation",
                new[] { "Sautekh", "Mephrit", "Novokh", "Nihilakh", "Nephrekh", "Szarekhan", "Tomb Watch", "Repair Cohort", "Hunter Matrix" },
                Buildings("Tomb Core", "Summoning Core", "Canoptek Forge", "Cryptek Archive", "Resurrection Node", "Energy Conduit", "Gauss Repository", "Canoptek Foundry", "Monolith Gate", "Obelisk", "Quantum Bastion", "Gauss Pylon"),
                Roster(("builder", A("Canoptek Scarab")), ("supply", A("Canoptek Logistics Barge", "Canoptek Hauler")),
                    ("trooper", A("Warrior", "Immortal", "Lychguard", "Flayed One")), ("scout", A("Deathmark", "Triarch Praetorian")),
                    ("medic", A("Technomancer")), ("engineer", A("Cryptek")), ("commander", A("Royal Warden", "Lord", "Overlord", "Chronomancer", "Plasmancer")),
                    ("standard", A("Dynastic Herald")), ("vehicle", A("Ghost Ark", "Doomsday Ark", "Annihilation Barge", "Catacomb Command Barge", "Doom Scythe", "Night Scythe", "Monolith"))));
            Add(result, "Tau", "Tau", "Ground cadre, Devilfish, Orca and drone delivery",
                new[] { "T'au Sept", "Vior'la Sept", "Sa'cea Sept", "Bork'an Sept", "Dal'yth Sept", "Farsight Enclaves", "Marker Network", "Guardian Web", "Recon Swarm" },
                Buildings("Command Dome", "Fire Warrior Barracks", "Earth Caste Workshop", "Earth Caste Laboratory", "Medical Bay", "Power Core", "Supply Hub", "Vehicle Assembly Plant", "Orca Landing Zone", "Communications Relay", "Tidewall", "Drone Turret"),
                Roster(("builder", A("Earth Caste Engineer")), ("supply", A("Cargo Drone", "Tetra Supply Skimmer")),
                    ("trooper", A("Fire Warrior", "Breacher", "Crisis Battlesuit", "Broadside", "Ghostkeel")), ("scout", A("Pathfinder", "Stealth Suit")),
                    ("medic", A("Medical Drone", "Shield Drone")), ("engineer", A("Repair Drone")), ("commander", A("Cadre Fireblade", "Ethereal", "Commander", "Darkstrider")),
                    ("standard", A("Marker Drone")), ("vehicle", A("Devilfish", "Hammerhead", "Skyray", "Piranha", "Stormsurge", "Barracuda"))));
            Add(result, "Tyranids", "Tyranids", "Mycetic Spores, brood nests, tunnels and infestation zones",
                new[] { "Leviathan", "Kraken", "Behemoth", "Jormungandr", "Kronos", "Gorgon", "Hydra", "Lictor Brood", "Spore Web", "Genestealer Vanguard" },
                Buildings("Synaptic Hive Node", "Brood Nest", "Norn Gestation Chamber", "Evolutionary Chamber", "Synapse Spire", "Digestion Pool", "Feeder Organism Cluster", "Capillary Tower", "Aerial Brood Sac", "Sensory Tendril Cluster", "Spore Chimney", "Biovore Nest"),
                Roster(("builder", A("Ripper Tendril")), ("supply", A("Biomass Carrier Organism", "Feeder Transport Beast")),
                    ("trooper", A("Termagant", "Hormagaunt", "Genestealer", "Tyranid Warrior", "Venomthrope", "Zoanthrope")),
                    ("scout", A("Gargoyle", "Ravener")), ("medic", A("Feeder Organism")), ("engineer", A("Ripper Tendril")),
                    ("commander", A("Tyranid Prime", "Neurotyrant", "Broodlord", "Hive Tyrant", "Swarmlord")), ("standard", A("Synapse Organism")),
                    ("vehicle", A("Carnifex", "Screamer-Killer", "Trygon", "Mawloc", "Haruspex", "Exocrine", "Tyrannofex", "Tyrannocyte"))));
            return result;
        }

        private static void Add(Dictionary<string, FactionDefinition> target, string id, string race, string deployment, string[] subfactions,
            Dictionary<string, string> buildings, Dictionary<string, string[]> roster)
        {
            var definition = new FactionDefinition { Id = id, Race = race, Deployment = deployment };
            foreach (KeyValuePair<string, string> pair in buildings) definition.Buildings[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, string[]> pair in roster) definition.Roster[pair.Key] = pair.Value;
            definition.Subfactions.AddRange(subfactions);
            target[id] = definition;
        }

        private static Dictionary<string, string> Buildings(string hq, string muster, string forge, string doctrine, string sustainment, string power,
            string logistics, string industry, string deployment, string intel, string fortification, string emplacement)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["HQ"] = hq, ["Muster"] = muster, ["War Forge"] = forge, ["Doctrine"] = doctrine,
                ["Sustainment"] = sustainment, ["Power"] = power, ["Logistics"] = logistics, ["Industry"] = industry,
                ["Deployment"] = deployment, ["Intel"] = intel, ["Fortification"] = fortification,
                ["Emplacement"] = emplacement, ["Signature"] = emplacement
            };
        }

        private static Dictionary<string, string[]> Roster(params (string role, string[] units)[] entries)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Length; i++) result[entries[i].role] = entries[i].units;
            return result;
        }

        private static string[] A(params string[] values) => values;
    }
}
