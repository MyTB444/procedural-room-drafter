using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Picks where the biome's island decor objects (<see cref="BiomeConfig.IslandDecorObjects"/>)
    /// go: 4–5 PER ISLAND, on distinct cells that are still island floor (a building may have
    /// claimed the island's top row). This pass only RECORDS placements on
    /// <see cref="RoomGrid.IslandDecor"/> (cell + prefab index) — the actual GameObjects are
    /// spawned/reused from <see cref="IslandDecorPool"/> when the room is shown, not painted here.
    /// </summary>
    public class IslandDecorPass : IRoomPass
    {
        private const int MinPerIsland = 4;
        private const int MaxPerIsland = 5;
        private const int IslandSize = IslandPass.IslandSize;

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            var objects = config.IslandDecorObjects;
            if (objects == null || objects.Length == 0) return;

            foreach (var (ax, ay) in grid.IslandAnchors)
            {
                var cells = new List<(int x, int y)>();
                for (int x = ax; x < ax + IslandSize; x++)
                    for (int y = ay; y < ay + IslandSize; y++)
                        if (grid[x, y] == CellType.Floor)
                            cells.Add((x, y));
                rng.Shuffle(cells);

                // Each shuffled cell is unique, so taking the first N never overlaps.
                int target = rng.Next(MinPerIsland, MaxPerIsland + 1);
                int placed = 0;
                foreach (var (x, y) in cells)
                {
                    if (placed >= target) break;
                    grid.IslandDecor.Add(new PatchPlacement(x, y, rng.Next(objects.Length)));
                    placed++;
                }
            }
        }
    }
}
