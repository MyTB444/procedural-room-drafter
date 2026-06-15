namespace TRV
{
    /// <summary>
    /// Cellular-automata caves: random-seed the interior (inset past the thick wall ring) with water,
    /// then smooth a few times so water coalesces into organic bodies and floor into caverns. The wall
    /// ring is left untouched (out-of-bounds counts as solid, so caves close cleanly against the walls).
    /// </summary>
    public class CavePass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;

            // 1) Random-seed the interior.
            for (int x = t; x < grid.Width - t; x++)
                for (int y = t; y < grid.Height - t; y++)
                    grid[x, y] = rng.NextDouble() < config.FillPercent ? CellType.Water : CellType.Floor;

            // 2) Smooth.
            for (int i = 0; i < config.SmoothingIterations; i++)
                Smooth(grid, config);
        }

        private static void Smooth(RoomGrid grid, BiomeConfig config)
        {
            int t = config.WallThickness;
            var next = new CellType[grid.Width, grid.Height];

            // Preserve everything first (incl. wall ring), then recompute interior from a snapshot.
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    next[x, y] = grid[x, y];

            for (int x = t; x < grid.Width - t; x++)
                for (int y = t; y < grid.Height - t; y++)
                    next[x, y] = CountSolidNeighbours(grid, x, y) >= config.SolidThreshold
                        ? CellType.Water
                        : CellType.Floor;

            for (int x = t; x < grid.Width - t; x++)
                for (int y = t; y < grid.Height - t; y++)
                    grid[x, y] = next[x, y];
        }

        private static int CountSolidNeighbours(RoomGrid grid, int cx, int cy)
        {
            int count = 0;
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    if (x == cx && y == cy) continue;
                    if (grid.Get(x, y).IsSolid()) count++; // OOB → Wall (solid)
                }
            return count;
        }
    }
}
