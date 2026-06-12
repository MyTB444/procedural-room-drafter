using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Every body of water big enough (<see cref="BiomeConfig.MinWaterForIsland"/>) gets an
    /// island: a strictly 2-wide path carved from the nearest existing floor out to the water's
    /// middle, ending in a 4×4 island pre-built from four 2×2 floor patches — so a room can grow
    /// several islands. Runs after ConnectivityPass (all floor is already connected, so ANY
    /// nearest floor cell is a valid hook-in point) and before FloorDecorPass (which respects the
    /// islands' pre-placed patches).
    /// </summary>
    public class IslandPass : IRoomPass
    {
        internal const int IslandSize = 4; // islands are IslandSize×IslandSize (read by IslandDecorPass)

        private const int PathWidth = 2;

        private static readonly (int dx, int dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.MinWaterForIsland <= 0) return;

            foreach (var region in WaterRegions(grid))
                if (region.Count >= config.MinWaterForIsland)
                    PlaceIsland(grid, rng, config, region);
        }

        private static void PlaceIsland(RoomGrid grid, System.Random rng, BiomeConfig config,
                                        List<(int x, int y)> region)
        {
            int t = config.WallThickness;

            // Island centre = the region's water cell nearest its centroid (the centroid itself
            // can fall outside the water in a concave region). An earlier island's path may have
            // carved through this region, so cells that are no longer water are skipped.
            long sumX = 0, sumY = 0;
            foreach (var (x, y) in region) { sumX += x; sumY += y; }
            var centroid = (x: (int)(sumX / region.Count), y: (int)(sumY / region.Count));
            var centre = centroid;
            int bestDist = int.MaxValue;
            foreach (var (x, y) in region)
            {
                if (grid[x, y] != CellType.Water) continue;
                int d = System.Math.Abs(x - centroid.x) + System.Math.Abs(y - centroid.y);
                if (d < bestDist) { bestDist = d; centre = (x, y); }
            }
            if (bestDist == int.MaxValue) return; // region was consumed by earlier carving

            // Hook the path into the nearest existing floor cell.
            var hook = centre;
            bestDist = int.MaxValue;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid[x, y] != CellType.Floor) continue;
                    int d = System.Math.Abs(x - centre.x) + System.Math.Abs(y - centre.y);
                    if (d < bestDist) { bestDist = d; hook = (x, y); }
                }
            if (bestDist == int.MaxValue) return; // a room with no floor at all

            CorridorPass.CarveL(grid, hook, centre, PathWidth, t, rng);

            // The island: a 4×4 floor block centred on the spot (shifted whole at the wall margin).
            int ax = System.Math.Clamp(centre.x - IslandSize / 2, t, grid.Width - t - IslandSize);
            int ay = System.Math.Clamp(centre.y - IslandSize / 2, t, grid.Height - t - IslandSize);
            for (int x = ax; x < ax + IslandSize; x++)
                for (int y = ay; y < ay + IslandSize; y++)
                    grid[x, y] = CellType.Floor;
            grid.IslandAnchors.Add((ax, ay)); // for IslandDecorPass

            // Pre-place its four 2×2 patches so the island is always fully patched.
            var twoByTwo = new List<int>();
            if (config.FloorPatches != null)
                for (int i = 0; i < config.FloorPatches.Length; i++)
                    if (config.FloorPatches[i]?.Size == 2)
                        twoByTwo.Add(i);
            if (twoByTwo.Count == 0) return; // no 2×2 patches authored — FloorDecorPass will fill

            grid.FloorPatches.Add(new PatchPlacement(ax, ay, twoByTwo[rng.Next(twoByTwo.Count)]));
            grid.FloorPatches.Add(new PatchPlacement(ax + 2, ay, twoByTwo[rng.Next(twoByTwo.Count)]));
            grid.FloorPatches.Add(new PatchPlacement(ax, ay + 2, twoByTwo[rng.Next(twoByTwo.Count)]));
            grid.FloorPatches.Add(new PatchPlacement(ax + 2, ay + 2, twoByTwo[rng.Next(twoByTwo.Count)]));
        }

        /// <summary>All 4-connected regions of Water cells.</summary>
        private static List<List<(int x, int y)>> WaterRegions(RoomGrid grid)
        {
            var visited = new bool[grid.Width, grid.Height];
            var regions = new List<List<(int x, int y)>>();

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (visited[x, y] || grid[x, y] != CellType.Water) continue;

                    var region = new List<(int x, int y)>();
                    var queue = new Queue<(int x, int y)>();
                    queue.Enqueue((x, y));
                    visited[x, y] = true;
                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        region.Add((cx, cy));
                        foreach (var (dx, dy) in Dirs)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (grid.InBounds(nx, ny) && !visited[nx, ny] && grid[nx, ny] == CellType.Water)
                            {
                                visited[nx, ny] = true;
                                queue.Enqueue((nx, ny));
                            }
                        }
                    }
                    regions.Add(region);
                }
            return regions;
        }
    }
}
