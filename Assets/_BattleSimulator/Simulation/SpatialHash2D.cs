using System.Collections.Generic;
using UnityEngine;

namespace BattleSimulator.Simulation
{
    public sealed class SpatialHash2D
    {
        private readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
        private readonly Stack<List<int>> listPool = new Stack<List<int>>();
        private readonly float cellSize;

        public SpatialHash2D(float cellSize)
        {
            this.cellSize = Mathf.Max(8f, cellSize);
        }

        public void Rebuild(IEnumerable<BattleEntityState> entities)
        {
            foreach (List<int> list in cells.Values)
            {
                list.Clear();
                listPool.Push(list);
            }
            cells.Clear();

            foreach (BattleEntityState entity in entities)
            {
                if (!entity.Active) continue;
                Vector2Int key = Key(entity.Position);
                if (!cells.TryGetValue(key, out List<int> list))
                {
                    list = listPool.Count > 0 ? listPool.Pop() : new List<int>(16);
                    cells.Add(key, list);
                }
                list.Add(entity.Id);
            }
        }

        public void Query(Vector2 center, float radius, List<int> results)
        {
            results.Clear();
            Vector2Int minimum = Key(center - Vector2.one * radius);
            Vector2Int maximum = Key(center + Vector2.one * radius);
            for (int y = minimum.y; y <= maximum.y; y++)
            {
                for (int x = minimum.x; x <= maximum.x; x++)
                {
                    if (cells.TryGetValue(new Vector2Int(x, y), out List<int> list))
                    {
                        results.AddRange(list);
                    }
                }
            }
        }

        private Vector2Int Key(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.y / cellSize));
        }
    }
}
