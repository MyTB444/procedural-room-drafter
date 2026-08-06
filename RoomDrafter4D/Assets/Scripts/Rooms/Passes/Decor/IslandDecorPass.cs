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

            // Aqua: every 4×4 island. The CENTRE 2×2 stays clear — the biome's upgrade BOOK spawns
            // on the island's middle point (RoomManager), and decor must not overlap it.
            foreach (var (ax, ay) in grid.IslandAnchors)
            {
                var cells = new List<(int x, int y)>();
                for (int x = ax; x < ax + IslandSize; x++)
                    for (int y = ay; y < ay + IslandSize; y++)
                    {
                        if (x >= ax + 1 && x <= ax + 2 && y >= ay + 1 && y <= ay + 2) continue; // book spot
                        if (grid[x, y] == CellType.Floor)
                            cells.Add((x, y));
                    }
                PlaceDecor(grid.IslandDecor, rng, objects, min, max, cells);
            }

            // Halls: the SMALL igrooms' interiors (the big main room is excluded by HallPass). Each
            // interior's CENTRE cell stays clear — the rare extra book spawns there.
            foreach (var area in grid.IgroomDecorAreas)
            {
                int scx = area.xMin + area.width / 2, scy = area.yMin + area.height / 2;
                var cells = new List<(int x, int y)>();
                for (int x = area.xMin; x < area.xMax; x++)
                    for (int y = area.yMin; y < area.yMax; y++)
                    {
                        if (x == scx && y == scy) continue; // book spot
                        if (grid[x, y] == CellType.Floor)
                            cells.Add((x, y));
                    }
                PlaceDecor(grid.IslandDecor, rng, objects, min, max, cells);
            }

            // (The big main igroom gets no scatter decor: its content is the filler + the book at
            // the centre — see RoomManager.SpawnMainContent / SpawnBooks.)
        }

        /// <summary>Record a random [min, max] decor count on distinct cells of <paramref name="cells"/>
        /// into <paramref name="target"/>, each cell's prefab chosen by per-type chance. Failed chance
        /// rolls are RE-SWEPT over the still-empty cells until the count is met (or a full sweep
        /// places nothing / cells run out) — so reserving cells for other content (e.g. the book
        /// spots) doesn't starve the decor count. The final on-collision check happens at spawn time
        /// in <see cref="IslandDecorPool"/>.</summary>
        private static void PlaceDecor(List<PatchPlacement> target, System.Random rng, IslandDecorEntry[] objects,
                                       int min, int max, List<(int x, int y)> cells)
        {
            rng.Shuffle(cells); // each shuffled cell is unique, so filling never overlaps
            int count = rng.Next(min, max + 1);
            int placed = 0;

            var unused = new List<(int x, int y)>(cells);
            while (placed < count && unused.Count > 0)
            {
                bool progressed = false;
                for (int i = unused.Count - 1; i >= 0 && placed < count; i--)
                {
                    int index = PickByChance(rng, objects);
                    if (index < 0) continue; // no type rolled in — this cell may fill on a later sweep
                    target.Add(new PatchPlacement(unused[i].x, unused[i].y, index));
                    unused.RemoveAt(i);
                    placed++;
                    progressed = true;
                }
                if (!progressed) break; // every chance is (near) zero — don't loop forever
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
