using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Lands (Plain layout) grass gradient: 1–2 room corners — always the ones FARTHEST from the
    /// paths — get a 3×3 or 4×4 FULL-grass cube tucked into the corner, wrapped by a 1–2 thick
    /// HALF-grass ring, then a 1–2 thick VERY-FEW-grass ring, then a 1–2 thick NO-grass ring that
    /// CLOSES the gradient; everything beyond is the PLAIN floor (the regular Floor pool). Purely
    /// visual: cells stay ordinary walkable Floor, levels are recorded via
    /// <see cref="RoomGrid.MarkGrass"/> (4/3/2/1) and painted from the biome's Grass pools. The
    /// rings grow by BFS that never enters a path cell NOR any cell touching one (8-adjacency) —
    /// the gradient always ends 1 tile of plain floor short of a path, and its far side stays plain.
    /// </summary>
    public class GrassPass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Plain) return;

            int t = config.WallThickness;
            var corners = new List<(int x, int y)>
            {
                (t, t),
                (grid.Width - t - 1, t),
                (t, grid.Height - t - 1),
                (grid.Width - t - 1, grid.Height - t - 1),
            };

            // Farthest-from-any-path first; the 1–2 chosen patches take the top of the ranking.
            corners.Sort((a, b) => PathDistance(grid, b).CompareTo(PathDistance(grid, a)));

            int count = rng.Next(1, 3); // 1 or 2 corner patches
            for (int i = 0; i < count; i++)
                GrowPatch(grid, rng, t, corners[i]);
        }

        /// <summary>The full-grass core rect nestled into the corner (extending inward on both
        /// axes), accepted only when EVERY cell is clean floor keeping the 1-tile path buffer — so
        /// the core is always a perfect rectangle, never clipped.</summary>
        private static bool TryCoreRect(RoomGrid grid, int t, (int x, int y) corner,
                                        int w, int h, out int x0, out int y0)
        {
            x0 = corner.x == t ? t : corner.x - w + 1;
            y0 = corner.y == t ? t : corner.y - h + 1;
            for (int x = x0; x < x0 + w; x++)
                for (int y = y0; y < y0 + h; y++)
                    if (grid.Get(x, y) != CellType.Floor || TouchesPath(grid, x, y))
                        return false;
            return true;
        }

        /// <summary>True when the cell IS a path cell or touches one (8-adjacency) — the gradient
        /// always keeps 1 tile of plain floor between itself and the paths.</summary>
        private static bool TouchesPath(RoomGrid grid, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (grid.IsPath(x + dx, y + dy))
                        return true;
            return false;
        }

        /// <summary>Chebyshev distance from a corner to the nearest path cell.</summary>
        private static int PathDistance(RoomGrid grid, (int x, int y) corner)
        {
            int best = int.MaxValue;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.IsPath(x, y))
                        best = System.Math.Min(best, System.Math.Max(
                            System.Math.Abs(x - corner.x), System.Math.Abs(y - corner.y)));
            return best;
        }

        /// <summary>One corner patch: the full-grass cube plus its two fading rings, grown by BFS
        /// (8-connected, so rings follow Chebyshev distance) that treats path cells as walls.</summary>
        private static void GrowPatch(RoomGrid grid, System.Random rng, int t, (int x, int y) corner)
        {
            int w = rng.Next(3, 5);         // full-grass core: 3–4 wide ×
            int h = rng.Next(3, 5);         // 3–4 tall — a perfect rectangle or square
            int halfThick = rng.Next(1, 3); // half-grass ring: 1–2 thick
            int fewThick = rng.Next(1, 3);  // very-few-grass ring: 1–2 thick
            int noneThick = rng.Next(1, 3); // no-grass ring closing the gradient: 1–2 thick

            // The core must be a PERFECT rect — every cell clean — so it's placed whole or not at
            // all: the rolled size first, a 3×3 fallback, else the corner gets no patch.
            if (!TryCoreRect(grid, t, corner, w, h, out int x0, out int y0) &&
                !TryCoreRect(grid, t, corner, w = 3, h = 3, out x0, out y0))
                return;

            var dist = new Dictionary<(int x, int y), int>();
            var queue = new Queue<(int x, int y)>();
            for (int x = x0; x < x0 + w; x++)
                for (int y = y0; y < y0 + h; y++)
                {
                    grid.MarkGrass(x, y, 4);
                    dist[(x, y)] = 0;
                    queue.Enqueue((x, y));
                }

            int maxDist = halfThick + fewThick + noneThick;
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                int d = dist[(cx, cy)];
                if (d >= maxDist) continue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (dist.ContainsKey((nx, ny))) continue;
                        if (grid.Get(nx, ny) != CellType.Floor || TouchesPath(grid, nx, ny)) continue;
                        int nd = d + 1;
                        dist[(nx, ny)] = nd;
                        grid.MarkGrass(nx, ny, nd <= halfThick ? 3 : nd <= halfThick + fewThick ? 2 : 1);
                        queue.Enqueue((nx, ny));
                    }
            }
        }
    }
}
