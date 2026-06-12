using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Packs the biome's pre-made floor tile groups (<see cref="BiomeConfig.FloorPatches"/>,
    /// e.g. 2×2 framed blocks, 4×4 mosaics) densely over the finished layout: every cell is a
    /// candidate anchor, visited in random order and gated by
    /// <see cref="BiomeConfig.FloorPatchCoverage"/>, so corridors end up mostly tiled with patches
    /// and the base floor only shows through the leftover seams. A patch is only placed where it
    /// fully fits on Floor cells without overlapping another patch. Purely decorative —
    /// placements go on <see cref="RoomGrid.FloorPatches"/> for the painter; the cells stay
    /// <see cref="CellType.Floor"/>.
    /// </summary>
    public class FloorDecorPass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            var patches = config.FloorPatches;
            if (patches == null || patches.Length == 0 || config.FloorPatchCoverage <= 0f) return;

            var anchors = new List<(int x, int y)>(grid.Width * grid.Height);
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    anchors.Add((x, y));
            rng.Shuffle(anchors);

            var used = new bool[grid.Width, grid.Height];

            // Respect patches pre-placed by earlier passes (e.g. IslandPass's 4×4 island).
            foreach (var placed in grid.FloorPatches)
            {
                if (placed.PatchIndex >= patches.Length) continue;
                int s = patches[placed.PatchIndex]?.Size ?? 0;
                for (int dx = 0; dx < s; dx++)
                    for (int dy = 0; dy < s; dy++)
                        if (grid.InBounds(placed.X + dx, placed.Y + dy))
                            used[placed.X + dx, placed.Y + dy] = true;
            }

            foreach (var (x, y) in anchors)
            {
                if (rng.NextDouble() >= config.FloorPatchCoverage) continue;

                int patchIndex = rng.Next(patches.Length);
                int size = patches[patchIndex]?.Size ?? 0;
                if (size < 1 || x + size > grid.Width || y + size > grid.Height) continue;
                if (!Fits(grid, used, x, y, size)) continue;

                for (int dx = 0; dx < size; dx++)
                    for (int dy = 0; dy < size; dy++)
                        used[x + dx, y + dy] = true;
                grid.FloorPatches.Add(new PatchPlacement(x, y, patchIndex));
            }
        }

        private static bool Fits(RoomGrid grid, bool[,] used, int x, int y, int size)
        {
            for (int dx = 0; dx < size; dx++)
                for (int dy = 0; dy < size; dy++)
                    if (grid[x + dx, y + dy] != CellType.Floor || used[x + dx, y + dy])
                        return false;
            return true;
        }
    }
}
