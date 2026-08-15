using System;
using System.Collections.Generic;
using BattleSimulator.Core;
using BattleSimulator.Data;
using UnityEngine;

namespace BattleSimulator.Simulation.Systems
{
    /// <summary>Directional sight, auspex, camouflage and faction-wide vox sharing.</summary>
    public sealed class PerceptionIntelSystem : IBattleSystem, ICadencedBattleSystem
    {
        private readonly List<int> nearby = new List<int>(192);
        public int Order => 40;
        public float UpdatesPerSecond => 10f;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState observer = world.Units[i];
                if (!observer.Active || observer.IsDead || observer.Incapacitated || observer.EmbarkedInId > 0) continue;
                float range = Mathf.Max(observer.VisionRange, observer.AuspexRange);
                world.Spatial.Query(observer.Position, range, nearby);
                float nearestThreat = float.PositiveInfinity;
                for (int n = 0; n < nearby.Count; n++)
                {
                    BattleEntityState candidate = world.GetEntity<BattleEntityState>(nearby[n]);
                    if (candidate == null || !candidate.Active || candidate.Id == observer.Id || candidate.OwnerId <= 0 || world.AreAllies(observer.OwnerId, candidate.OwnerId)) continue;
                    Vector2 offset = candidate.Position - observer.Position;
                    float distance = offset.magnitude;
                    bool optical = OpticalContact(observer, candidate, offset, distance, world.Time);
                    bool sensor = AuspexContact(observer, candidate, distance, world.Time);
                    if (!optical && !sensor) continue;
                    nearestThreat = Mathf.Min(nearestThreat, distance);
                    PlayerState owner = world.GetPlayer(observer.OwnerId);
                    if (owner == null) continue;
                    float confidence = optical ? 1f : Mathf.Clamp01(0.9f - distance / Mathf.Max(1f, observer.AuspexRange) * 0.35f);
                    owner.IntelContacts[candidate.Id] = new IntelContactRecord
                    {
                        EntityId = candidate.Id, ObserverPlayerId = owner.Id,
                        ContactState = optical ? IntelContactState.Visible : IntelContactState.SensorConfirmed,
                        Position = candidate.Position, Classification = candidate.Kind.ToString(), Confidence = confidence,
                        UncertaintyRadius = optical ? 0f : Mathf.Lerp(4f, 34f, 1f - confidence), ObservedAt = world.Time,
                        ExpiresAt = world.Time + (candidate.Kind == EntityKind.Building ? 180d : 18d)
                    };
                    observer.EnemyContactId = candidate.Id;
                    observer.LastEnemyContactTime = world.Time;
                    if (candidate is BuildingState building && building.BuildingType == BuildingType.Headquarters)
                    {
                        owner.LastKnownEnemyBase = candidate.Position;
                        owner.EnemyBaseObservedAt = world.Time;
                    }
                }
                observer.Alertness = nearestThreat < float.PositiveInfinity ? Mathf.Clamp01(1f - nearestThreat / Mathf.Max(1f, range)) : Mathf.MoveTowards(observer.Alertness, 0f, step.DeltaTime * 0.15f);
                observer.Tension = Mathf.Max(observer.Alertness, observer.Suppression);
                // Only baseline humans route from fear; other factions express resolve, aggression or machine discipline.
                observer.Fear = world.GetPlayer(observer.OwnerId)?.Faction == "Imperial Guard"
                    ? Mathf.Clamp01(observer.Suppression * 0.7f + (1f - observer.Condition) * 0.3f)
                    : 0f;
            }
            ExpireContacts(world);
        }

        private static bool OpticalContact(UnitState observer, BattleEntityState candidate, Vector2 offset, float distance, double now)
        {
            if (distance > observer.VisionRange) return false;
            Vector2 facing = new Vector2(Mathf.Cos(observer.FacingRadians), Mathf.Sin(observer.FacingRadians));
            if (offset.sqrMagnitude > 0.01f && Vector2.Angle(facing, offset) > observer.VisionArcDegrees * 0.5f) return false;
            UnitState target = candidate as UnitState;
            if (target == null || !target.Camouflaged || target.RevealedUntil > now) return true;
            float detection = observer.Alertness * 0.35f + (1f - distance / Mathf.Max(1f, observer.VisionRange)) * 0.75f;
            return detection >= target.Camouflage;
        }

        private static bool AuspexContact(UnitState observer, BattleEntityState candidate, float distance, double now)
        {
            if (distance > observer.AuspexRange) return false;
            UnitState target = candidate as UnitState;
            float signature = target == null ? 0.9f : target.LastWeaponDischargeAt + 4d >= now ? 1f : target.Camouflaged ? 0.35f : 0.72f;
            return signature * (1f - distance / Mathf.Max(1f, observer.AuspexRange) * 0.55f) >= 0.28f;
        }

        private static void ExpireContacts(BattleWorld world)
        {
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                var expired = new List<int>();
                foreach (KeyValuePair<int, IntelContactRecord> pair in player.IntelContacts)
                {
                    if (pair.Value.ExpiresAt < world.Time) expired.Add(pair.Key);
                    else if (pair.Value.ObservedAt + 0.5d < world.Time) pair.Value.ContactState = IntelContactState.Remembered;
                }
                for (int i = 0; i < expired.Count; i++) player.IntelContacts.Remove(expired[i]);
            }
        }
    }

    /// <summary>Turns battle objectives, shortages and faction identity into physical squad missions.</summary>
    public sealed class StrategicCommandSystem : IBattleSystem, ICadencedBattleSystem
    {
        private readonly BattleDataRepository data;
        public StrategicCommandSystem(BattleDataRepository data) { this.data = data; }
        public int Order => 70;
        public float UpdatesPerSecond => 1f;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                if (player.Defeated) continue;
                UpdateDynamicBehavior(world, player);
                for (int s = 0; s < world.Squads.Count; s++)
                {
                    SquadState squad = world.Squads[s];
                    if (squad.OwnerId != player.Id || squad.MemberIds.Count == 0 || squad.RoleCommittedUntil > world.Time) continue;
                    AssignMission(world, player, squad);
                    squad.RoleCommittedUntil = world.Time + 5d + (squad.Id % 4);
                }
            }
        }

        private void UpdateDynamicBehavior(BattleWorld world, PlayerState player)
        {
            BattleObjectiveDefinition objective = data.Objective(player.BattleObjective);
            float enemyPressure = 0f, territoryRatio = 0f;
            int owned = 0;
            for (int c = 0; c < world.TerritoryCells.Count; c++) if (world.TerritoryCells[c].OwnerId == player.Id) owned++;
            territoryRatio = world.TerritoryCells.Count == 0 ? 0f : (float)owned / world.TerritoryCells.Count;
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (unit.OwnerId == player.Id && unit.HasRecentEnemyContact(world.Time, 5f)) enemyPressure += 0.01f;
            }
            player.Aggression = Mathf.Clamp(player.Aggression + objective.Signal("aggression") * 6f + enemyPressure * 4f - (1f - player.SupplyCondition) * 3f, 10f, 95f);
            player.Expansion = Mathf.Clamp(player.Expansion + objective.Signal("expansion") * 5f + (0.22f - territoryRatio) * 7f, 10f, 95f);
            player.Caution = Mathf.Clamp(player.Caution + objective.Signal("caution") * 4f + (1f - player.Morale) * 5f, 5f, 95f);
        }

        private static void AssignMission(BattleWorld world, PlayerState player, SquadState squad)
        {
            ResourceZoneState needed = MostNeededZone(world, player, SquadCenter(world, squad));
            bool recon = SquadHasRole(world, squad, UnitRole.Scout);
            if (needed != null && (recon || player.Economy + player.Expansion > 95f))
            {
                squad.PrimaryRole = recon ? SquadPrimaryRole.Reconnaissance : SquadPrimaryRole.Capture;
                squad.Objective = needed.Position; squad.ObjectiveEntityId = needed.Id;
            }
            else if (player.EnemyBaseObservedAt + 180d >= world.Time && player.Aggression >= player.Caution)
            {
                squad.PrimaryRole = SquadPrimaryRole.Offensive; squad.Objective = player.LastKnownEnemyBase;
            }
            else
            {
                TerritoryCellState target = BestFrontier(world, player, SquadCenter(world, squad));
                squad.PrimaryRole = recon ? SquadPrimaryRole.Capture : squad.Id % 5 == 0 ? SquadPrimaryRole.TerritoryDefense : SquadPrimaryRole.Offensive;
                squad.Objective = target != null ? target.Center : player.SpawnOrigin;
            }
            ApplyObjectiveToMembers(world, squad);
        }

        private static void ApplyObjectiveToMembers(BattleWorld world, SquadState squad)
        {
            for (int i = 0; i < squad.MemberIds.Count; i++)
            {
                UnitState unit = world.GetEntity<UnitState>(squad.MemberIds[i]);
                if (unit == null || !unit.Active || unit.IsDead || unit.Order == UnitOrder.Withdraw || unit.Role == UnitRole.Builder) continue;
                unit.Destination = squad.Objective;
                unit.Order = squad.PrimaryRole == SquadPrimaryRole.Capture || squad.PrimaryRole == SquadPrimaryRole.Reconnaissance ? UnitOrder.Capture : UnitOrder.Move;
            }
        }

        private static ResourceZoneState MostNeededZone(BattleWorld world, PlayerState player, Vector2 origin)
        {
            EconomyProfileDefinition profile = EconomyCatalog.For(player.Faction);
            ResourceZoneState best = null; float bestScore = 0f;
            for (int i = 0; i < world.ResourceZones.Count; i++)
            {
                ResourceZoneState zone = world.ResourceZones[i];
                if (!zone.Active || zone.OwnerId == player.Id || !profile.ActiveResources.Contains(zone.ResourceType)) continue;
                float stock = player.Resource(zone.ResourceType);
                float shortage = profile.Priority(zone.ResourceType) * (1f + 500f / Mathf.Max(80f, stock));
                float score = shortage * 500f / (80f + Vector2.Distance(origin, zone.Position));
                if (score > bestScore) { bestScore = score; best = zone; }
            }
            return best;
        }

        private static TerritoryCellState BestFrontier(BattleWorld world, PlayerState player, Vector2 origin)
        {
            TerritoryCellState best = null; float bestScore = float.NegativeInfinity;
            for (int i = 0; i < world.TerritoryCells.Count; i++)
            {
                TerritoryCellState cell = world.TerritoryCells[i];
                if (cell.OwnerId == player.Id) continue;
                float score = -Vector2.Distance(origin, cell.Center) * 0.01f;
                for (int z = 0; z < world.ResourceZones.Count; z++) if (cell.Contains(world.ResourceZones[z].Position)) score += EconomyCatalog.For(player.Faction).Priority(world.ResourceZones[z].ResourceType) * 5f;
                if (score > bestScore) { bestScore = score; best = cell; }
            }
            return best;
        }

        private static Vector2 SquadCenter(BattleWorld world, SquadState squad)
        {
            Vector2 center = Vector2.zero; int count = 0;
            for (int i = 0; i < squad.MemberIds.Count; i++) { UnitState u = world.GetEntity<UnitState>(squad.MemberIds[i]); if (u != null && u.Active && !u.IsDead) { center += u.Position; count++; } }
            return count > 0 ? center / count : squad.Objective;
        }
        private static bool SquadHasRole(BattleWorld world, SquadState squad, UnitRole role) { for (int i = 0; i < squad.MemberIds.Count; i++) { UnitState u = world.GetEntity<UnitState>(squad.MemberIds[i]); if (u != null && u.Role == role) return true; } return false; }
    }

    /// <summary>Keeps squads coherent without requiring Marines to remain glued together.</summary>
    public sealed class SquadFormationSystem : IBattleSystem, ICadencedBattleSystem
    {
        public int Order => 90;
        public float UpdatesPerSecond => 5f;
        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int s = 0; s < world.Squads.Count; s++)
            {
                SquadState squad = world.Squads[s];
                bool contact = false;
                for (int i = 0; i < squad.MemberIds.Count; i++) { UnitState u = world.GetEntity<UnitState>(squad.MemberIds[i]); if (u != null && u.HasRecentEnemyContact(world.Time, 4f)) { contact = true; break; } }
                squad.FormationActive = contact;
                squad.Formation = !contact ? FormationType.None : squad.PrimaryRole == SquadPrimaryRole.TerritoryDefense ? FormationType.DefensiveRing : squad.PrimaryRole == SquadPrimaryRole.Offensive ? FormationType.Wedge : FormationType.Staggered;
                if (!contact) continue;
                Vector2 center = SquadCenter(world, squad);
                Vector2 forward = (squad.Objective - center).normalized; if (forward.sqrMagnitude < 0.1f) forward = Vector2.right;
                Vector2 right = new Vector2(-forward.y, forward.x);
                for (int i = 0; i < squad.MemberIds.Count; i++)
                {
                    UnitState unit = world.GetEntity<UnitState>(squad.MemberIds[i]);
                    if (unit == null || !unit.Active || unit.IsDead || unit.Order == UnitOrder.Attack || unit.Order == UnitOrder.Withdraw) continue;
                    int rank = i / 5, file = i % 5 - 2;
                    float spacing = unit.Radius * 2.4f + 2f;
                    Vector2 offset = squad.Formation == FormationType.Wedge ? -forward * Mathf.Abs(file) * spacing + right * file * spacing : -forward * rank * spacing + right * file * spacing;
                    unit.Destination = world.ClampToWorld(center + offset, unit.Radius);
                    unit.Order = UnitOrder.Move;
                }
            }
        }
        private static Vector2 SquadCenter(BattleWorld world, SquadState squad) { Vector2 c = Vector2.zero; int n = 0; for (int i = 0; i < squad.MemberIds.Count; i++) { UnitState u = world.GetEntity<UnitState>(squad.MemberIds[i]); if (u != null && u.Active && !u.IsDead) { c += u.Position; n++; } } return n > 0 ? c / n : squad.Objective; }
    }

    /// <summary>Faction medical care, vehicle repairs, bleeding and recovery.</summary>
    public sealed class SustainmentSystem : IBattleSystem, ICadencedBattleSystem
    {
        public int Order => 560;
        public float UpdatesPerSecond => 3f;
        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (!unit.Active || unit.IsDead) continue;
                if (unit.Bleeding > 0f && !unit.Stabilized) unit.HitPoints -= unit.Bleeding * step.DeltaTime;
                if (unit.HitPoints <= 0f) { ResolveFatalState(world, unit); continue; }
                UnitState caretaker = NearestCaretaker(world, unit);
                BuildingState bay = NearestSustainment(world, unit);
                if (caretaker == null && bay == null) continue;
                float rate = bay != null ? 22f : caretaker.Role == UnitRole.Medic ? 17f : 28f;
                if (unit.Kind == EntityKind.Vehicle && caretaker != null && caretaker.Role != UnitRole.Engineer && caretaker.Role != UnitRole.Builder) continue;
                unit.HitPoints = Mathf.Min(unit.MaximumHitPoints, unit.HitPoints + rate * step.DeltaTime);
                unit.Bleeding = Mathf.Max(0f, unit.Bleeding - step.DeltaTime * 0.7f);
                unit.Stabilized |= unit.Bleeding <= 0.05f;
                if (unit.Condition > 0.25f) { unit.Incapacitated = false; unit.CombatCapable = unit.Role != UnitRole.Builder && unit.Role != UnitRole.SupplyCarrier; }
            }
        }
        private static void ResolveFatalState(BattleWorld world, UnitState unit)
        {
            PlayerState owner = world.GetPlayer(unit.OwnerId);
            if (owner != null && owner.Faction == "Necrons" && world.Random.Range(0f, 1f) < 0.55f) { unit.HitPoints = unit.MaximumHitPoints * 0.28f; unit.Incapacitated = false; return; }
            if (owner != null && owner.Faction == "Tyranids" && world.Random.Range(0f, 1f) < 0.18f) { unit.HitPoints = unit.MaximumHitPoints * 0.2f; return; }
            unit.IsDead = true; unit.CombatCapable = false; unit.DeathTime = world.Time; if (owner != null) owner.Casualties++;
        }
        private static UnitState NearestCaretaker(BattleWorld world, UnitState target) { UnitState best = null; float d = 70f * 70f; for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (!u.Active || u.IsDead || u.OwnerId != target.OwnerId || u.Id == target.Id || u.Role != UnitRole.Medic && u.Role != UnitRole.Engineer && u.Role != UnitRole.Builder) continue; float q = Vector2.SqrMagnitude(u.Position - target.Position); if (q < d) { d = q; best = u; } } return best; }
        private static BuildingState NearestSustainment(BattleWorld world, UnitState target) { for (int i = 0; i < world.Buildings.Count; i++) { BuildingState b = world.Buildings[i]; if (b.Active && b.Operational && b.OwnerId == target.OwnerId && (b.BuildingType == BuildingType.Hospital || b.BuildingType == BuildingType.Headquarters) && Vector2.Distance(b.Position, target.Position) < b.Radius + 45f) return b; } return null; }
    }

    /// <summary>Distinctive runtime rules that do not fork the shared technical foundation.</summary>
    public sealed class FactionIdentitySystem : IBattleSystem, ICadencedBattleSystem
    {
        private readonly Dictionary<int, int> processedKills = new Dictionary<int, int>();
        public int Order => 580;
        public float UpdatesPerSecond => 1f;
        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int p = 0; p < world.Players.Count; p++)
            {
                PlayerState player = world.Players[p];
                processedKills.TryGetValue(player.Id, out int knownKills);
                int newKills = Mathf.Max(0, player.UnitsKilled - knownKills); processedKills[player.Id] = player.UnitsKilled;
                if (newKills > 0)
                {
                    if (player.Faction == "Chaos") { player.AddResource(ResourceType.Influence, newKills * 5f); player.AddResource(ResourceType.Energy, newKills * 2f); }
                    else if (player.Faction == "Tyranids") player.AddResource(ResourceType.Biomass, newKills * 12f);
                    else if (player.Faction == "Orks") { player.AddResource(ResourceType.Scrap, newKills * 5f); player.WaaaghMomentum = Mathf.Clamp01(player.WaaaghMomentum + newKills * 0.015f); }
                }
                int nearbyOrks = 0, banners = 0;
                for (int b = 0; b < world.Buildings.Count; b++) if (world.Buildings[b].Active && world.Buildings[b].OwnerId == player.Id && world.Buildings[b].OperationalRole == "Signature") banners++;
                for (int i = 0; i < world.Units.Count; i++)
                {
                    UnitState unit = world.Units[i]; if (!unit.Active || unit.IsDead || unit.OwnerId != player.Id) continue;
                    if (player.Faction == "Orks") { nearbyOrks++; unit.Morale = Mathf.Clamp01(0.45f + player.WaaaghMomentum * 0.45f); unit.BuffedUntil = player.WaaaghMomentum > 0.5f ? world.Time + 1d : unit.BuffedUntil; }
                    if (player.Faction == "Space Marines" && unit.Specialty != null && unit.Specialty.IndexOf("Chaplain", StringComparison.OrdinalIgnoreCase) >= 0) BuffNearby(world, unit, 75f, world.Time + 1d);
                    if (player.Faction == "Space Marines")
                    {
                        if (unit.MaximumIronHalo > 0f && unit.IronHaloLastHitAt + 8d < world.Time) unit.IronHalo = Mathf.Min(unit.MaximumIronHalo, unit.IronHalo + step.DeltaTime * 8f);
                        if (unit.Specialty != null && unit.Specialty.IndexOf("Apothecary", StringComparison.OrdinalIgnoreCase) >= 0) RecoverGeneSeed(world, player, unit);
                        if (unit.Specialty != null && unit.Specialty.IndexOf("Assault", StringComparison.OrdinalIgnoreCase) >= 0) UseJumpPack(world, unit);
                    }
                    if (player.Faction == "Tau") unit.Confidence = Mathf.Max(unit.Confidence, 0.55f + Mathf.Min(0.25f, player.IntelContacts.Count * 0.004f));
                    if (player.Faction == "Tyranids") unit.Morale = HasCommanderNearby(world, unit) ? 1f : 0.45f;
                }
                if (player.Faction == "Orks") player.WaaaghMomentum = Mathf.Clamp01(player.WaaaghMomentum + step.DeltaTime * (nearbyOrks * 0.001f + banners * 0.03f) - (banners == 0 ? 0.012f : 0f));
            }
        }
        private static void BuffNearby(BattleWorld world, UnitState source, float radius, double until) { for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (u.Active && !u.IsDead && u.OwnerId == source.OwnerId && Vector2.Distance(u.Position, source.Position) <= radius) { u.BuffedUntil = until; u.Morale = Mathf.Min(1f, u.Morale + 0.05f); } } }
        private static bool HasCommanderNearby(BattleWorld world, UnitState unit) { for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (u.Active && !u.IsDead && u.OwnerId == unit.OwnerId && u.Role == UnitRole.Commander && Vector2.Distance(u.Position, unit.Position) <= 130f) return true; } return false; }
        private static void RecoverGeneSeed(BattleWorld world, PlayerState player, UnitState apothecary) { for (int i = 0; i < world.Units.Count; i++) { UnitState fallen = world.Units[i]; if (!fallen.IsDead || !fallen.GeneSeedBearing || fallen.GeneSeedRecovered || fallen.OwnerId != player.Id || Vector2.Distance(fallen.Position, apothecary.Position) > 55f) continue; fallen.GeneSeedRecovered = true; player.GeneSeedStock++; break; } }
        private static void UseJumpPack(BattleWorld world, UnitState unit) { if (unit.AbilityReadyAt > world.Time || !unit.HasRecentEnemyContact(world.Time, 3f)) return; BattleEntityState target = world.GetEntity<BattleEntityState>(unit.EnemyContactId); if (target == null) return; Vector2 offset = target.Position - unit.Position; if (offset.magnitude < 55f || offset.magnitude > 190f) return; unit.Position = world.ClampToWorld(unit.Position + offset.normalized * Mathf.Min(80f, offset.magnitude - 30f), unit.Radius); unit.AbilityReadyAt = world.Time + 12d; }
    }

    /// <summary>Capturable map-authored landmarks and route flow. Routes are never invented by AI.</summary>
    public sealed class LandmarkEconomySystem : IBattleSystem, ICadencedBattleSystem
    {
        public int Order => 540;
        public float UpdatesPerSecond => 1f;
        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int n = 0; n < world.EconomicNodes.Count; n++)
            {
                EconomicNodeState node = world.EconomicNodes[n];
                int claimant = PhysicalClaimant(world, node.Position, node.Radius + 22f);
                if (claimant > 0 && claimant != node.OwnerId)
                {
                    if (node.CapturingPlayerId != claimant) { node.CapturingPlayerId = claimant; node.CaptureProgress = 0f; }
                    node.CaptureProgress += step.DeltaTime / 12f;
                    if (node.CaptureProgress >= 1f) { node.OwnerId = claimant; node.CaptureProgress = 0f; node.CapturingPlayerId = 0; SeedLandmarkFlows(node); PlayerState owner = world.GetPlayer(claimant); if (owner != null) owner.CapturedNodes++; TransferCaptureStock(owner, node); }
                }
                if (node.OwnerId > 0) ApplyFlows(world.GetPlayer(node.OwnerId), node, step.DeltaTime);
            }
        }
        private static int PhysicalClaimant(BattleWorld world, Vector2 center, float radius) { int claimant = 0; for (int i = 0; i < world.Units.Count; i++) { UnitState u = world.Units[i]; if (!u.Active || u.IsDead || !u.CombatCapable || Vector2.Distance(u.Position, center) > radius) continue; if (claimant == 0) claimant = u.OwnerId; else if (!world.AreAllies(claimant, u.OwnerId)) return 0; } return claimant; }
        private static void SeedLandmarkFlows(EconomicNodeState node)
        {
            if (node.Exports.Count > 0 || node.Imports.Count > 0) return;
            string type = (node.NodeType ?? string.Empty).ToLowerInvariant();
            if (type.Contains("fuel") || type.Contains("promethium")) { node.Exports[ResourceType.Fuel] = 12f; node.Imports[ResourceType.Security] = 2f; }
            else if (type.Contains("agri") || type.Contains("farm")) { node.Exports[ResourceType.Food] = 16f; node.Imports[ResourceType.Fuel] = 2f; }
            else if (type.Contains("hive")) { node.Exports[ResourceType.Requisition] = 9f; node.Exports[ResourceType.Food] = 5f; node.Imports[ResourceType.Fuel] = 4f; node.Imports[ResourceType.Medical] = 3f; }
            else if (type.Contains("supply")) { node.Exports[ResourceType.Ammunition] = 10f; node.Exports[ResourceType.Materials] = 7f; node.Exports[ResourceType.Food] = 4f; }
            else if (type.Contains("mechan") || type.Contains("forge")) { node.Exports[ResourceType.Parts] = 10f; node.Exports[ResourceType.Energy] = 6f; node.Imports[ResourceType.Materials] = 5f; }
            else { node.Exports[ResourceType.Materials] = 5f; node.Exports[ResourceType.Requisition] = 4f; }
            foreach (KeyValuePair<ResourceType, float> pair in node.Exports) node.CaptureStock[pair.Key] = pair.Value * 20f;
        }
        private static void TransferCaptureStock(PlayerState player, EconomicNodeState node) { if (player == null || node.LastCaptureRecipient == player.Id) return; foreach (KeyValuePair<ResourceType, float> pair in node.CaptureStock) player.AddResource(pair.Key, pair.Value); node.LastCaptureRecipient = player.Id; }
        private static void ApplyFlows(PlayerState player, EconomicNodeState node, float dt) { if (player == null) return; float importFactor = 1f; foreach (KeyValuePair<ResourceType, float> pair in node.Imports) if (player.Resource(pair.Key) < pair.Value) importFactor = Mathf.Min(importFactor, player.Resource(pair.Key) / Mathf.Max(0.01f, pair.Value)); foreach (KeyValuePair<ResourceType, float> pair in node.Imports) player.AddResource(pair.Key, -pair.Value * importFactor * dt); foreach (KeyValuePair<ResourceType, float> pair in node.Exports) player.AddResource(pair.Key, pair.Value * importFactor * dt); }
    }

    /// <summary>Uses actual vehicle capacity to embark, move and disembark squads; aircraft remain on their own collision layer.</summary>
    public sealed class VehicleDeploymentSystem : IBattleSystem, ICadencedBattleSystem
    {
        public int Order => 180;
        public float UpdatesPerSecond => 4f;
        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState transport = world.Units[i];
                if (!transport.Active || transport.IsDead || transport.PassengerCapacity <= 0 || transport.EmbarkedInId > 0) continue;
                RemoveInvalidPassengers(world, transport);
                if (transport.PassengerIds.Count == 0) EmbarkNearbySquad(world, transport);
                if (transport.PassengerIds.Count == 0) continue;
                SquadState squad = PassengerSquad(world, transport);
                Vector2 target = squad != null ? squad.Objective : transport.Destination;
                transport.Destination = target; transport.Order = UnitOrder.Move;
                bool danger = transport.HasRecentEnemyContact(world.Time, 3f);
                if (danger || Vector2.Distance(transport.Position, target) <= 70f) Disembark(world, transport, target);
                else for (int p = 0; p < transport.PassengerIds.Count; p++) { UnitState passenger = world.GetEntity<UnitState>(transport.PassengerIds[p]); if (passenger != null) passenger.Position = transport.Position; }
            }
        }

        private static void EmbarkNearbySquad(BattleWorld world, UnitState transport)
        {
            SquadState best = null; float bestDistance = 35f * 35f;
            for (int s = 0; s < world.Squads.Count; s++)
            {
                SquadState squad = world.Squads[s]; if (squad.OwnerId != transport.OwnerId || squad.MemberIds.Count == 0) continue;
                UnitState first = FirstAvailable(world, squad); if (first == null || Vector2.Distance(first.Position, squad.Objective) < 180f) continue;
                float distance = Vector2.SqrMagnitude(first.Position - transport.Position); if (distance < bestDistance) { bestDistance = distance; best = squad; }
            }
            if (best == null) return;
            for (int m = 0; m < best.MemberIds.Count && transport.PassengerIds.Count < transport.PassengerCapacity; m++)
            {
                UnitState unit = world.GetEntity<UnitState>(best.MemberIds[m]);
                if (unit == null || !unit.Active || unit.IsDead || unit.EmbarkedInId > 0 || unit.Kind == EntityKind.Vehicle || Vector2.Distance(unit.Position, transport.Position) > 42f) continue;
                unit.EmbarkedInId = transport.Id; unit.Visible = false; unit.Velocity = Vector2.zero; transport.PassengerIds.Add(unit.Id);
            }
        }
        private static void Disembark(BattleWorld world, UnitState transport, Vector2 target)
        {
            for (int p = 0; p < transport.PassengerIds.Count; p++)
            {
                UnitState unit = world.GetEntity<UnitState>(transport.PassengerIds[p]); if (unit == null) continue;
                float angle = Mathf.PI * 2f * p / Mathf.Max(1, transport.PassengerIds.Count);
                unit.Position = world.ClampToWorld(transport.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (transport.Radius + unit.Radius + 4f), unit.Radius);
                unit.EmbarkedInId = 0; unit.Visible = true; unit.Destination = target; unit.Order = UnitOrder.Move;
            }
            transport.PassengerIds.Clear();
        }
        private static void RemoveInvalidPassengers(BattleWorld world, UnitState transport) { for (int i = transport.PassengerIds.Count - 1; i >= 0; i--) { UnitState u = world.GetEntity<UnitState>(transport.PassengerIds[i]); if (u == null || !u.Active || u.IsDead) transport.PassengerIds.RemoveAt(i); } }
        private static SquadState PassengerSquad(BattleWorld world, UnitState transport) { for (int i = 0; i < transport.PassengerIds.Count; i++) { UnitState u = world.GetEntity<UnitState>(transport.PassengerIds[i]); if (u != null && u.SquadId > 0) return world.GetSquad(u.SquadId); } return null; }
        private static UnitState FirstAvailable(BattleWorld world, SquadState squad) { for (int i = 0; i < squad.MemberIds.Count; i++) { UnitState u = world.GetEntity<UnitState>(squad.MemberIds[i]); if (u != null && u.Active && !u.IsDead && u.EmbarkedInId == 0) return u; } return null; }
    }
}
