using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Picks where the biome's island decor objects (<see cref="BiomeConfig.IslandDecorObjects"/>)
    /// go: a per-island count (<see cref="BiomeConfig.IslandDecorMinPerIsland"/>..Max, adjustable), on
    /// distinct cells that are still island floor (a building may have claimed the island's top row).
    /// This pass only RECORDS placements on <see cref="RoomGrid.IslandDecor"/> (cell + prefab index) —
    /// the actual GameObjects are spawned/reused from <see cref="IslandDecorPool"/> when the room is
    /// shown, not painted here.
    /// </summary>
    public class IslandDecorPass : IRoomPass
    {
        private const int IslandSize = IslandPass.IslandSize;

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            var objects = config.IslandDecorObjects;
            if (objects == null || objects.Length == 0) return;

            int min = System.Math.Max(0, config.IslandDecorMinPerIsland);
            int max = System.Math.Max(min, config.IslandDecorMaxPerIsland);
            if (max == 0) return;

            foreach (var (ax, ay) in grid.IslandAnchors)
            {
                var cells = new List<(int x, int y)>();
                for (int x = ax; x < ax + IslandSize; x++)
                    for (int y = ay; y < ay + IslandSize; y++)
                        if (grid[x, y] == CellType.Floor)
                            cells.Add((x, y));
                rng.Shuffle(cells);

                // Each shuffled cell is unique, so taking the first N never overlaps.
                int target = rng.Next(min, max + 1);
                int placed = 0;
                foreach (var (x, y) in cells)
                {
                    if (placed >= target) break;
                    int index = PickByChance(rng, objects);
                    if (index < 0) continue; // no type rolled in for this cell → leave it empty
                    grid.IslandDecor.Add(new PatchPlacement(x, y, index));
                    placed++;
                }
            }
        }

        /// <summary>Pick a decor index by each type's INDEPENDENT chance: try the types in random order
        /// and take the first whose chance roll succeeds (entries with no prefab / chance ≤ 0 are skipped).
        /// Returns -1 when none rolled in (the cell gets no decor this time).</summary>
        private static int PickByChance(System.Random rng, IslandDecorEntry[] entries)
        {
            foreach (int i in rng.ShuffledIndices(entries.Length))
            {
                var e = entries[i];
                if (e == null || e.Prefab == null || e.Chance <= 0f) continue;
                if (rng.NextDouble() < e.Chance) return i;
            }
            return -1;
        }
    }
}
