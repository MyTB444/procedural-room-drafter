using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Scatters the biome's hand-made 2×3 water decor groups (<see cref="BiomeConfig.WaterDecorPatches"/>)
    /// over open water: any spot with a full 3×3 of water qualifies (the 2×3 decor sits inside it
    /// at a random horizontal offset). Between <see cref="MinCount"/> and <see cref="MaxCount"/>
    /// per room, never overlapping each other. Purely visual — placements go on
    /// <see cref="RoomGrid.WaterDecor"/> for the painter (Extras front layer); cells stay Water.
    /// </summary>
    public class WaterDecorPass : IRoomPass
    {
        private const int MinCount = 2;
        private const int MaxCount = 4;
        private const int DecorWidth = WaterDecorPatch.Width;
        private const int DecorHeight = WaterDecorPatch.Height;
        private const int AreaSize = 3; // required all-water block around a candidate spot

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            var decors = config.WaterDecorPatches;
            if (decors == null || decors.Length == 0) return;

            // Every 3×3 all-water block is a candidate spot.
            var anchors = new List<(int x, int y)>();
            for (int x = 0; x <= grid.Width - AreaSize; x++)
                for (int y = 0; y <= grid.Height - AreaSize; y++)
                    if (AllWater(grid, x, y))
                        anchors.Add((x, y));
            if (anchors.Count == 0) return;
            rng.Shuffle(anchors);

            int target = rng.Next(MinCount, MaxCount + 1);
            var used = new bool[grid.Width, grid.Height];
            int placed = 0;

            foreach (var (ax, ay) in anchors)
            {
                if (placed >= target) break;

                int x = ax + rng.Next(0, AreaSize - DecorWidth + 1); // random offset inside the 3×3
                int y = ay;
                if (Overlaps(used, x, y)) continue;

                for (int dx = 0; dx < DecorWidth; dx++)
                    for (int dy = 0; dy < DecorHeight; dy++)
                        used[x + dx, y + dy] = true;
                grid.WaterDecor.Add(new PatchPlacement(x, y, rng.Next(decors.Length)));
                placed++;
            }
        }

        private static bool AllWater(RoomGrid grid, int x, int y)
        {
            for (int dx = 0; dx < AreaSize; dx++)
                for (int dy = 0; dy < AreaSize; dy++)
                    if (grid[x + dx, y + dy] != CellType.Water)
                        return false;
            return true;
        }

        private static bool Overlaps(bool[,] used, int x, int y)
        {
            for (int dx = 0; dx < DecorWidth; dx++)
                for (int dy = 0; dy < DecorHeight; dy++)
                    if (used[x + dx, y + dy])
                        return true;
            return false;
        }
    }
}
