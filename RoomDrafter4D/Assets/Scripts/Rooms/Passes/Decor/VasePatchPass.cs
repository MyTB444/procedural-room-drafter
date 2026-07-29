using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Lands (Plain layout) vase patches: 1–3 random 2×2 floor patches on plain ground — NEVER on
    /// a path or grass cell (nor beside grass, so the gradient's edge ring stays visible), clear
    /// of door landings, and kept apart from each other. Records bottom-left cells on
    /// <see cref="RoomGrid.VaseSpots"/>: the painter stamps <see cref="BiomeConfig.VaseFloorPatch"/>
    /// there (Map layer), and RoomManager spawns a breakable VASE at each patch's centre point
    /// (breaks persist via the room snapshot, like other decor).
    /// </summary>
    public class VasePatchPass : IRoomPass
    {
        private const int MinCount = 1;
        private const int MaxCount = 3;
        private const int Spacing = 2; // min Chebyshev gap between two patch rects

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Plain) return;

            int t = config.WallThickness;

            // Door landings — a patch (and its vase) never sits in a doorway approach.
            var landings = new HashSet<(int x, int y)>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid[x, y] != CellType.Door) continue;
                    int ix = x < t ? 1 : x >= grid.Width - t ? -1 : 0; // inward step
                    int iy = y < t ? 1 : y >= grid.Height - t ? -1 : 0;
                    for (int k = 1; k <= config.LandingDepth; k++)
                        landings.Add((x + ix * k, y + iy * k));
                }

            bool PlainCell(int cx, int cy)
            {
                if (grid.Get(cx, cy) != CellType.Floor || grid.IsPath(cx, cy)) return false;
                if (landings.Contains((cx, cy))) return false;
                for (int dx = -1; dx <= 1; dx++) // on OR beside grass = the gradient/its edge ring
                    for (int dy = -1; dy <= 1; dy++)
                        if (grid.GrassLevelAt(cx + dx, cy + dy) > 0)
                            return false;
                return true;
            }

            var candidates = new List<Vector2Int>(); // bottom-left of the 2×2
            for (int x = t; x < grid.Width - t - 1; x++)
                for (int y = t; y < grid.Height - t - 1; y++)
                    if (PlainCell(x, y) && PlainCell(x + 1, y) && PlainCell(x, y + 1) && PlainCell(x + 1, y + 1))
                        candidates.Add(new Vector2Int(x, y));

            rng.Shuffle(candidates);
            int target = rng.Next(MinCount, MaxCount + 1);
            foreach (var spot in candidates)
            {
                if (grid.VaseSpots.Count == target) break;
                bool clear = true;
                foreach (var placed in grid.VaseSpots)
                    if (Mathf.Max(Mathf.Abs(placed.x - spot.x), Mathf.Abs(placed.y - spot.y)) < 2 + Spacing)
                    { clear = false; break; }
                if (clear) grid.VaseSpots.Add(spot);
            }

            ScatterGrassVases(grid, rng);
        }

        /// <summary>4–6 vases scattered anywhere on the GRASSY part of every grass patch — the
        /// full, half and very-few layers (levels 4/3/2; the no-grass ring is excluded). Each
        /// patch is its own 4-connected component (patches are kept apart, so components never
        /// merge). Capped by the component's cell count.</summary>
        private static void ScatterGrassVases(RoomGrid grid, System.Random rng)
        {
            bool Grassy(int gx, int gy) => grid.GrassLevelAt(gx, gy) >= 2;

            var visited = new HashSet<(int x, int y)>();
            var component = new List<Vector2Int>();
            var stack = new Stack<(int x, int y)>();

            for (int sx = 0; sx < grid.Width; sx++)
                for (int sy = 0; sy < grid.Height; sy++)
                {
                    if (!Grassy(sx, sy) || visited.Contains((sx, sy))) continue;

                    component.Clear();
                    visited.Add((sx, sy));
                    stack.Push((sx, sy));
                    while (stack.Count > 0)
                    {
                        var (cx, cy) = stack.Pop();
                        component.Add(new Vector2Int(cx, cy));
                        Visit(cx - 1, cy); Visit(cx + 1, cy); Visit(cx, cy - 1); Visit(cx, cy + 1);
                    }
                    void Visit(int nx, int ny)
                    {
                        if (!Grassy(nx, ny) || !visited.Add((nx, ny))) return;
                        stack.Push((nx, ny));
                    }

                    rng.Shuffle(component);
                    int count = System.Math.Min(rng.Next(4, 7), component.Count);
                    for (int i = 0; i < count; i++)
                        grid.GrassVaseSpots.Add(component[i]);
                }
        }
    }
}
