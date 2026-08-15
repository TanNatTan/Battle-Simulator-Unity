using System;
using BattleSimulator.Simulation;
using UnityEngine;

namespace BattleSimulator.Data
{
    public static class UnitArchetypeCatalog
    {
        public static void Configure(UnitState unit, PlayerState player, string specialty, BattleDataRepository data)
        {
            unit.Specialty = string.IsNullOrWhiteSpace(specialty) ? unit.Role.ToString() : specialty;
            unit.Subfaction = player.Subfaction;
            unit.Species = player.Race;
            ApplyFactionBaseline(unit, player.Faction);
            ApplySpecialty(unit, player);
            ApplyWeapon(unit, data);
            ApplyPostWeaponSpecialty(unit);
            ConfigureVehicle(unit);
            unit.HitPoints = unit.MaximumHitPoints;
            unit.Ammunition = unit.MaximumAmmunition;
            unit.Magazine = unit.MagazineSize;
            unit.LastProgressPosition = unit.Position;
        }

        private static void ApplyFactionBaseline(UnitState unit, string faction)
        {
            switch (faction)
            {
                case "Space Marines":
                    unit.MaximumHitPoints = 130f; unit.ArmorProtection = 15f; unit.Speed = 30f; unit.Accuracy = 0.82f; unit.Precision = 0.8f;
                    unit.Morale = 0.92f; unit.Aggression = 0.58f; unit.Confidence = 0.82f; unit.VisionRange = 210f; unit.VisionArcDegrees = 215f; unit.AuspexRange = 190f; unit.WeaponId = "bolter"; unit.GeneSeedBearing = true; break;
                case "Imperial Guard":
                    unit.MaximumHitPoints = 82f; unit.ArmorProtection = 5f; unit.Speed = 27f; unit.Accuracy = 0.68f; unit.Precision = 0.65f;
                    unit.Morale = 0.62f; unit.Aggression = 0.46f; unit.Confidence = 0.56f; unit.VisionRange = 170f; unit.VisionArcDegrees = 190f; unit.AuspexRange = 0f; unit.WeaponId = "rifle"; break;
                case "Adeptus Mechanicus":
                    unit.MaximumHitPoints = 96f; unit.ArmorProtection = 9f; unit.Speed = 26f; unit.Accuracy = 0.76f; unit.Precision = 0.78f;
                    unit.Morale = 0.82f; unit.Aggression = 0.48f; unit.Confidence = 0.72f; unit.VisionRange = 185f; unit.AuspexRange = 210f; unit.WeaponId = "plasma-gun"; break;
                case "Chaos":
                    unit.MaximumHitPoints = 112f; unit.ArmorProtection = 12f; unit.Speed = 29f; unit.Accuracy = 0.72f; unit.Precision = 0.68f;
                    unit.Morale = 0.78f; unit.Aggression = 0.72f; unit.Confidence = 0.72f; unit.VisionRange = 180f; unit.AuspexRange = 90f; unit.WeaponId = "bolter"; unit.GeneSeedBearing = true; break;
                case "Orks":
                    unit.MaximumHitPoints = 105f; unit.ArmorProtection = 6f; unit.Speed = 29f; unit.Accuracy = 0.56f; unit.Precision = 0.5f;
                    unit.Morale = 0.74f; unit.Aggression = 0.88f; unit.Confidence = 0.7f; unit.VisionRange = 165f; unit.AuspexRange = 0f; unit.WeaponId = "rifle"; break;
                case "Necrons":
                    unit.MaximumHitPoints = 125f; unit.ArmorProtection = 14f; unit.Speed = 23f; unit.Accuracy = 0.78f; unit.Precision = 0.76f;
                    unit.Morale = 1f; unit.Aggression = 0.52f; unit.Confidence = 0.9f; unit.VisionRange = 190f; unit.AuspexRange = 180f; unit.WeaponId = "plasma-gun"; break;
                case "Tau":
                    unit.MaximumHitPoints = 88f; unit.ArmorProtection = 7f; unit.Speed = 29f; unit.Accuracy = 0.82f; unit.Precision = 0.86f;
                    unit.Morale = 0.7f; unit.Aggression = 0.42f; unit.Confidence = 0.68f; unit.VisionRange = 205f; unit.AuspexRange = 220f; unit.WeaponId = "carbine"; break;
                case "Tyranids":
                    unit.MaximumHitPoints = 92f; unit.ArmorProtection = 7f; unit.Speed = 32f; unit.Accuracy = 0.62f; unit.Precision = 0.55f;
                    unit.Morale = 0.86f; unit.Aggression = 0.78f; unit.Confidence = 0.78f; unit.VisionRange = 175f; unit.AuspexRange = 100f; unit.WeaponId = "rifle"; break;
            }
        }

        private static void ApplySpecialty(UnitState unit, PlayerState player)
        {
            string text = unit.Specialty.ToLowerInvariant();
            if (unit.Role == UnitRole.Builder || unit.Role == UnitRole.SupplyCarrier)
            {
                unit.CombatCapable = unit.Role == UnitRole.Builder && (text.Contains("engineer") || text.Contains("mek") || text.Contains("tech-priest"));
                unit.WeaponId = unit.CombatCapable ? "engineer-tools" : "unarmed";
                unit.GeneSeedBearing = false;
            }
            if (unit.Role == UnitRole.Scout) { unit.Speed *= 1.15f; unit.VisionRange *= 1.22f; unit.AuspexRange *= 1.22f; unit.Camouflage += 0.16f; unit.WeaponId = "carbine"; }
            if (unit.Role == UnitRole.Medic) { unit.CombatCapable = true; unit.WeaponId = player.Faction == "Space Marines" ? "bolter" : "rifle"; unit.GeneSeedBearing = false; }
            if (unit.Role == UnitRole.Commander) { unit.MaximumHitPoints *= 1.35f; unit.ArmorProtection += 2f; unit.Morale = 1f; unit.Confidence = 0.95f; unit.IronHalo = text.Contains("captain") || text.Contains("chapter master") ? 70f : 0f; unit.MaximumIronHalo = unit.IronHalo; unit.GeneSeedBearing = false; }
            if (text.Contains("devastator")) { unit.WeaponId = "heavy-bolter"; unit.Speed *= 0.75f; unit.WeaponRange *= 1.5f; unit.FireInterval /= 1.75f; }
            else if (text.Contains("hellblaster")) unit.WeaponId = "plasma-gun";
            else if (text.Contains("eradicator") || text.Contains("melta")) unit.WeaponId = "multi-melta";
            else if (text.Contains("eliminator") || text.Contains("sniper") || text.Contains("deathmark") || text.Contains("ratling")) { unit.WeaponId = "stalker-bolt-rifle"; unit.Camouflage += 0.28f; unit.Precision += 0.12f; }
            else if (text.Contains("assault") || text.Contains("berzerker") || text.Contains("slugga") || text.Contains("hormagaunt")) { unit.WeaponId = "bolter"; unit.Aggression = Mathf.Max(unit.Aggression, 0.82f); unit.Speed *= 1.08f; }
            if (text.Contains("terminator")) { unit.MaximumHitPoints *= 1.65f; unit.ArmorProtection += 7f; unit.Speed *= 0.72f; unit.SuppressionResistance += 0.3f; }
            else if (text.Contains("heavy intercessor")) { unit.MaximumHitPoints *= 1.22f; unit.ArmorProtection += 2f; unit.Speed *= 0.9f; }
            if (text.Contains("veteran") || text.Contains("bladeguard")) { unit.MaximumHitPoints *= 1.16f; unit.Accuracy = Mathf.Min(0.98f, unit.Accuracy + 0.07f); unit.Precision = Mathf.Min(0.98f, unit.Precision + 0.07f); }
            if (text.Contains("jump") || text.Contains("assault marine") || text.Contains("gargoyle") || text.Contains("raptor")) unit.Speed *= 1.22f;
            if (text.Contains("skull probe") || text.Contains("servo-skull")) { unit.MaximumHitPoints = 32f; unit.Radius = 2.5f; unit.Speed = 45f; unit.MovementLayer = MovementLayer.Air; unit.CombatCapable = false; unit.GeneSeedBearing = false; }
            unit.Camouflaged = unit.Camouflage >= 0.5f;
        }

        private static void ApplyWeapon(UnitState unit, BattleDataRepository data)
        {
            if (!data.Weapons.TryGetValue(unit.WeaponId, out WeaponDefinition weapon)) return;
            unit.Damage = weapon.Damage; unit.Penetration = weapon.Penetration; unit.WeaponRange = weapon.Range;
            unit.FireInterval = weapon.RateOfFire <= 0f ? 1f : weapon.RateOfFire; unit.MagazineSize = weapon.MagazineSize;
            unit.ReloadDuration = weapon.ReloadTime; unit.Accuracy = Mathf.Clamp01(unit.Accuracy * weapon.Accuracy); unit.Precision = Mathf.Clamp01(unit.Precision * weapon.Precision);
            unit.HeatPerShot = weapon.HeatPerShot; unit.CoolRate = weapon.CoolRate; unit.MaximumHeat = weapon.MaximumHeat;
            if (TryProjectile(weapon.ProjectileClass, out ProjectileClass projectileClass)) unit.ProjectileClass = projectileClass;
            if (data.Projectiles.TryGetValue(weapon.ProjectileClass, out ProjectileDefinition projectile)) unit.ProjectileBehavior = Flags(projectile);
            unit.MaximumAmmunition = Mathf.Max(0, weapon.MagazineSize * 8 * 4);
        }

        private static void ApplyPostWeaponSpecialty(UnitState unit)
        {
            string text = unit.Specialty.ToLowerInvariant();
            if (text.Contains("devastator")) { unit.WeaponRange *= 1.5f; unit.FireInterval /= 1.75f; }
            if (text.Contains("captain") || text.Contains("chapter master")) unit.MaximumIronHalo = unit.IronHalo = Mathf.Max(70f, unit.MaximumIronHalo);
        }

        private static void ConfigureVehicle(UnitState unit)
        {
            if (unit.Role != UnitRole.Vehicle && unit.Role != UnitRole.Aircraft) return;
            string text = unit.Specialty.ToLowerInvariant();
            unit.Kind = EntityKind.Vehicle; unit.Radius = 9f; unit.MaximumHitPoints *= 2.6f; unit.ArmorProtection += 12f; unit.GeneSeedBearing = false;
            unit.Speed = 30f; unit.PassengerCapacity = 0; unit.MaximumFuel = unit.Fuel = 100f;
            if (ContainsAny(text, "thunderhawk", "stormraven", "stormtalon", "stormhawk", "heldrake", "dakkajet", "barracuda", "doom scythe", "night scythe"))
            { unit.Role = UnitRole.Aircraft; unit.Kind = EntityKind.Aircraft; unit.MovementLayer = MovementLayer.Air; unit.Speed = 68f; unit.AircraftPhase = AircraftPhase.Flying; unit.Altitude = 100f; }
            if (ContainsAny(text, "impulsor")) { unit.PassengerCapacity = 6; unit.Speed = 40f; }
            else if (ContainsAny(text, "trukk")) { unit.PassengerCapacity = 12; unit.Speed = 38f; }
            else if (ContainsAny(text, "rhino", "razorback", "repulsor", "chimera", "devilfish", "ghost ark", "land raider", "battlewagon")) { unit.PassengerCapacity = text.Contains("land raider") || text.Contains("battlewagon") ? 8 : 10; unit.Speed = text.Contains("land raider") ? 26f : 34f; }
            if (ContainsAny(text, "storm speeder", "invader atv", "piranha", "scrapjet")) unit.Speed = 44f;
            if (ContainsAny(text, "dreadnought", "deff dread", "killa kan", "walker", "carnifex", "trygon", "defiler")) unit.Speed = 24f;
            if (ContainsAny(text, "whirlwind", "basilisk", "manticore", "exocrine", "doomsday")) { unit.Speed = 27f; unit.ProjectileClass = ProjectileClass.Artillery; unit.ProjectileBehavior |= ProjectileBehavior.Indirect | ProjectileBehavior.Explosive | ProjectileBehavior.Suppression; unit.WeaponRange = 260f; }
        }

        private static bool TryProjectile(string value, out ProjectileClass projectileClass)
        {
            string normalized = (value ?? "BALLISTIC").Replace("_", string.Empty);
            foreach (ProjectileClass candidate in Enum.GetValues(typeof(ProjectileClass))) if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase)) { projectileClass = candidate; return true; }
            projectileClass = ProjectileClass.Ballistic; return false;
        }
        private static ProjectileBehavior Flags(ProjectileDefinition definition)
        {
            ProjectileBehavior result = ProjectileBehavior.None;
            foreach (string flag in definition.Flags)
            {
                string normalized = flag.Replace("_", string.Empty);
                foreach (ProjectileBehavior candidate in Enum.GetValues(typeof(ProjectileBehavior))) if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase)) result |= candidate;
            }
            return result;
        }
        private static bool ContainsAny(string source, params string[] values) { for (int i = 0; i < values.Length; i++) if (source.Contains(values[i])) return true; return false; }
    }
}
