using System.Collections.Generic;
using BattleSimulator.Core;
using UnityEngine;

namespace BattleSimulator.Simulation
{
    public sealed class BattleWorld
    {
        private readonly Dictionary<int, BattleEntityState> entities = new Dictionary<int, BattleEntityState>();
        private readonly List<UnitState> units = new List<UnitState>();
        private readonly List<BuildingState> buildings = new List<BuildingState>();
        private readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        private readonly List<ResourceZoneState> resourceZones = new List<ResourceZoneState>();
        private readonly List<TerritoryCellState> territoryCells = new List<TerritoryCellState>();
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<int> removalBuffer = new List<int>();
        private readonly Stack<ProjectileState> projectilePool = new Stack<ProjectileState>();
        private int nextEntityId = 1;

        public BattleWorld(float width, float height, int seed = 742918)
        {
            Width = Mathf.Max(320f, width);
            Height = Mathf.Max(180f, height);
            Random = new DeterministicRandom(seed);
            Events = new BattleEventBus();
            Spatial = new SpatialHash2D(SimulationConstants.SpatialCellSize);
        }

        public float Width { get; }
        public float Height { get; }
        public double Time { get; internal set; }
        public ulong Tick { get; internal set; }
        public bool BattleEnded { get; internal set; }
        public int WinningTeamId { get; internal set; }
        public DeterministicRandom Random { get; }
        public BattleEventBus Events { get; }
        public SpatialHash2D Spatial { get; }
        public IReadOnlyDictionary<int, BattleEntityState> Entities => entities;
        public IReadOnlyList<UnitState> Units => units;
        public IReadOnlyList<BuildingState> Buildings => buildings;
        public IReadOnlyList<ProjectileState> Projectiles => projectiles;
        public IReadOnlyList<ResourceZoneState> ResourceZones => resourceZones;
        public IReadOnlyList<TerritoryCellState> TerritoryCells => territoryCells;
        public IReadOnlyList<PlayerState> Players => players;

        public void AddPlayer(PlayerState player)
        {
            players.Add(player);
        }

        public T AddEntity<T>(T entity) where T : BattleEntityState
        {
            if (entity.Id <= 0) entity.Id = nextEntityId++;
            else nextEntityId = Mathf.Max(nextEntityId, entity.Id + 1);
            entities[entity.Id] = entity;
            if (entity is UnitState unit) units.Add(unit);
            else if (entity is BuildingState building) buildings.Add(building);
            else if (entity is ProjectileState projectile) projectiles.Add(projectile);
            else if (entity is ResourceZoneState resourceZone) resourceZones.Add(resourceZone);
            return entity;
        }

        public ProjectileState AcquireProjectile()
        {
            ProjectileState projectile = projectilePool.Count > 0 ? projectilePool.Pop() : new ProjectileState();
            projectile.Id = 0;
            projectile.Active = true;
            projectile.Visible = true;
            projectile.Velocity = Vector2.zero;
            projectile.RemainingRange = 0f;
            projectile.SplashRadius = 0f;
            return projectile;
        }

        public void AddTerritory(TerritoryCellState territory)
        {
            territoryCells.Add(territory);
        }

        public bool TryGetEntity(int id, out BattleEntityState entity)
        {
            return entities.TryGetValue(id, out entity);
        }

        public T GetEntity<T>(int id) where T : BattleEntityState
        {
            return entities.TryGetValue(id, out BattleEntityState entity) ? entity as T : null;
        }

        public PlayerState GetPlayer(int playerId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == playerId) return players[i];
            }
            return null;
        }

        public bool AreAllies(int leftPlayerId, int rightPlayerId)
        {
            if (leftPlayerId == rightPlayerId) return true;
            PlayerState left = GetPlayer(leftPlayerId);
            PlayerState right = GetPlayer(rightPlayerId);
            return left != null && right != null && left.TeamId == right.TeamId;
        }

        public Vector2 ClampToWorld(Vector2 point, float padding = 2f)
        {
            return new Vector2(Mathf.Clamp(point.x, padding, Width - padding), Mathf.Clamp(point.y, padding, Height - padding));
        }

        public void RemoveInactive()
        {
            removalBuffer.Clear();
            foreach (KeyValuePair<int, BattleEntityState> pair in entities)
            {
                if (!pair.Value.Active) removalBuffer.Add(pair.Key);
            }

            foreach (int id in removalBuffer)
            {
                BattleEntityState entity = entities[id];
                entities.Remove(id);
                if (entity is UnitState unit) units.Remove(unit);
                else if (entity is BuildingState building) buildings.Remove(building);
                else if (entity is ProjectileState projectile)
                {
                    projectiles.Remove(projectile);
                    projectilePool.Push(projectile);
                }
                else if (entity is ResourceZoneState resourceZone) resourceZones.Remove(resourceZone);
            }
        }

        public void RebuildSpatialIndex()
        {
            Spatial.Rebuild(entities.Values);
        }
    }
}
