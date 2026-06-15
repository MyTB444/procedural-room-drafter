using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Scatters the biome's hand-made island decor pieces (<see cref="BiomeConfig.IslandDecorPatches"/>,
    /// 1 tile or 2 stacked vertically) on top of each island: 4–5 PER ISLAND, on cells that are
    /// still island floor (a building may have claimed the island's top row), never overlapping.
    /// Purely visual — placements go on <see cref="RoomGrid.IslandDecor"/> for the painter
    /// (Extras front layer); the cells stay walkable Floor.
    /// </summary>
    public class IslandDecorPass : IRoomPass
    {
        private const int MinPerIsland = 4;
        private const int MaxPerIsland = 5;
        private const int IslandSize = IslandPass.IslandSize;

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            var decors = config.IslandDecorPatches;
            if (decors == null || decors.Length == 0) return;

            var used = new HashSet<(int, int)>();
            foreach (var (ax, ay) in grid.IslandAnchors)
            {
                var cells = new List<(int x, int y)>();
                for (int x = ax; x < ax + IslandSize; x++)
                    for (int y = ay; y < ay + IslandSize; y++)
                        if (grid[x, y] == CellType.Floor)
                            cells.Add((x, y));
                rng.Shuffle(cells);

                int target = rng.Next(MinPerIsland, MaxPerIsland + 1);
                int placed = 0;
                foreach (var (x, y) in cells)
                {
                    if (placed >= target) break;
                    if (used.Contains((x, y))) continue;

                    int index = rng.Next(decors.Length);
                    int height = decors[index]?.Tiles?.Length ?? 0;
                    if (height < 1) continue;
                    if (height > 1) // 2-tall: the cell above must be free island floor too
                    {
                        if (y + 1 >= ay + IslandSize || grid[x, y + 1] != CellType.Floor) continue;
                        if (used.Contains((x, y + 1))) continue;
                        used.Add((x, y + 1));
                    }

                    used.Add((x, y));
                    grid.IslandDecor.Add(new PatchPlacement(x, y, index));
                    placed++;
                }
            }
        }
    }
}
