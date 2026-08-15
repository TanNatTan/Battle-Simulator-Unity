using System.Collections.Generic;
using UnityEngine;

namespace BattleSimulator.Simulation
{
    public static class TerritoryGenerator
    {
        public static void Generate(BattleWorld world, int columns, int rows)
        {
            var sites = new List<Vector2>(columns * rows);
            float cellWidth = world.Width / columns, cellHeight = world.Height / rows;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                float jitterX = world.Random.Range(-cellWidth * 0.22f, cellWidth * 0.22f);
                float jitterY = world.Random.Range(-cellHeight * 0.22f, cellHeight * 0.22f);
                sites.Add(new Vector2(Mathf.Clamp((x + 0.5f) * cellWidth + jitterX, 1f, world.Width - 1f), Mathf.Clamp((y + 0.5f) * cellHeight + jitterY, 1f, world.Height - 1f)));
            }
            for (int i = 0; i < sites.Count; i++)
            {
                var polygon = new List<Vector2>
                {
                    Vector2.zero, new Vector2(world.Width, 0f), new Vector2(world.Width, world.Height), new Vector2(0f, world.Height)
                };
                for (int other = 0; other < sites.Count && polygon.Count > 2; other++)
                {
                    if (other == i) continue;
                    ClipCloserTo(polygon, sites[i], sites[other]);
                }
                Vector2 center = Centroid(polygon);
                Vector2 minimum = polygon[0], maximum = polygon[0];
                for (int p = 1; p < polygon.Count; p++) { minimum = Vector2.Min(minimum, polygon[p]); maximum = Vector2.Max(maximum, polygon[p]); }
                var cell = new TerritoryCellState { Id = i + 1, Center = center, Size = maximum - minimum };
                cell.Polygon.AddRange(polygon);
                world.AddTerritory(cell);
            }
            float neighborRange = Mathf.Sqrt(cellWidth * cellWidth + cellHeight * cellHeight) * 1.32f;
            for (int i = 0; i < world.TerritoryCells.Count; i++)
            for (int j = i + 1; j < world.TerritoryCells.Count; j++)
            {
                TerritoryCellState left = world.TerritoryCells[i], right = world.TerritoryCells[j];
                if (Vector2.Distance(left.Center, right.Center) > neighborRange) continue;
                left.NeighborIds.Add(right.Id); right.NeighborIds.Add(left.Id);
            }
        }

        private static void ClipCloserTo(List<Vector2> polygon, Vector2 site, Vector2 other)
        {
            if (polygon.Count == 0) return;
            Vector2 midpoint = (site + other) * 0.5f;
            Vector2 normal = other - site;
            var output = new List<Vector2>(polygon.Count + 2);
            Vector2 previous = polygon[polygon.Count - 1];
            float previousDistance = Vector2.Dot(previous - midpoint, normal);
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                float currentDistance = Vector2.Dot(current - midpoint, normal);
                bool currentInside = currentDistance <= 0f, previousInside = previousDistance <= 0f;
                if (currentInside != previousInside)
                {
                    float denominator = previousDistance - currentDistance;
                    float t = Mathf.Abs(denominator) < 0.00001f ? 0f : previousDistance / denominator;
                    output.Add(Vector2.Lerp(previous, current, Mathf.Clamp01(t)));
                }
                if (currentInside) output.Add(current);
                previous = current; previousDistance = currentDistance;
            }
            polygon.Clear(); polygon.AddRange(output);
        }

        private static Vector2 Centroid(List<Vector2> polygon)
        {
            float area = 0f, x = 0f, y = 0f;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                float cross = polygon[j].x * polygon[i].y - polygon[i].x * polygon[j].y;
                area += cross; x += (polygon[j].x + polygon[i].x) * cross; y += (polygon[j].y + polygon[i].y) * cross;
            }
            area *= 0.5f;
            if (Mathf.Abs(area) < 0.0001f) { Vector2 average = Vector2.zero; for (int i = 0; i < polygon.Count; i++) average += polygon[i]; return average / Mathf.Max(1, polygon.Count); }
            return new Vector2(x / (6f * area), y / (6f * area));
        }
    }
}
