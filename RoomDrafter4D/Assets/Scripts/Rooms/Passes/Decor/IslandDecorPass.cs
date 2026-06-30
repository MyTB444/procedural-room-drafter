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

            // Aqua: every 4×4 island.
            foreach (var (ax, ay) in grid.IslandAnchors)
            {
                var cells = new List<(int x, int y)>();
                for (int x = ax; x < ax + IslandSize; x++)
                    for (int y = ay; y < ay + IslandSize; y++)
                        if (grid[x, y] == CellType.Floor)
                            cells.Add((x, y));
                PlaceDecor(grid.IslandDecor, rng, objects, min, max, cells);
            }

            // Halls: the SMALL igrooms' interiors (the big main room is excluded by HallPass).
            foreach (var area in grid.IgroomDecorAreas)
            {
                var cells = new List<(int x, int y)>();
                for (int x = area.xMin; x < area.xMax; x++)
                    for (int y = area.yMin; y < area.yMax; y++)
                        if (grid[x, y] == CellType.Floor)
                            cells.Add((x, y));
                PlaceDecor(grid.IslandDecor, rng, objects, min, max, cells);
            }

            // Halls big main room: same count/type as the small igrooms, but recorded SEPARATELY (only used
            // when the room's content is an upgrade — RoomManager appends it then). Keep the 3×3 around the
            // interior centre clear so it never lands on the upgrade pickup placed there.
            var main = grid.MainIgroomArea;
            if (main.width > 0 && main.height > 0)
            {
                int cx = main.xMin + main.width / 2, cy = main.yMin + main.height / 2;
                var cells = new List<(int x, int y)>();
                for (int x = main.xMin; x < main.xMax; x++)
                    for (int y = main.yMin; y < main.yMax; y++)
                        if (grid[x, y] == CellType.Floor &&
                            (System.Math.Abs(x - cx) > 1 || System.Math.Abs(y - cy) > 1)) // off the upgrade spot
                            cells.Add((x, y));
                PlaceDecor(grid.MainIgroomDecor, rng, objects, min, max, cells);
            }
        }

        /// <summary>Record up to a random [min, max] decor on distinct cells of <paramref name="cells"/>
        /// into <paramref name="target"/>, each cell's prefab chosen by per-type chance (a cell where
        /// nothing rolls in stays empty). The final on-collision check happens at spawn time in
        /// <see cref="IslandDecorPool"/>.</summary>
        private static void PlaceDecor(List<PatchPlacement> target, System.Random rng, IslandDecorEntry[] objects,
                                       int min, int max, List<(int x, int y)> cells)
        {
            rng.Shuffle(cells); // each shuffled cell is unique, so taking the first N never overlaps
            int count = rng.Next(min, max + 1);
            int placed = 0;
            foreach (var (x, y) in cells)
            {
                if (placed >= count) break;
                int index = PickByChance(rng, objects);
                if (index < 0) continue; // no type rolled in for this cell → leave it empty
                target.Add(new PatchPlacement(x, y, index));
                placed++;
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
