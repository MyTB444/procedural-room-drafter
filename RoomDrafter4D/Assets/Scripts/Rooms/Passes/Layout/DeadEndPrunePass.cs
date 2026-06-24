using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Halls cleanup: removes stray 1-tile DEAD-END floor corridors — floor that tapers to a tip
    /// connecting nothing. Iteratively turns every Floor cell with ≤ 1 walkable neighbour into Water
    /// (igroom doorways in <see cref="RoomGrid.Entrances"/> are protected; Door cells are never
    /// eroded). Leaf-erosion only ever removes degree-1 tips, so it can NEVER disconnect anything —
    /// the connected floor network stays intact, INCLUDING 1-wide corridors that actually bridge two
    /// areas (those cells have 2 neighbours); only the hanging dead-ends go. Halls-only.
    /// </summary>
    public class DeadEndPrunePass : IRoomPass
    {
        private static readonly (int dx, int dy)[] FourWay = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Halls) return;

            // Never prune an igroom doorway (would seal the room) — everything else a dead-end can eat.
            var keep = new HashSet<(int x, int y)>();
            foreach (var e in grid.Entrances) keep.Add((e.x, e.y));

            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int x = 0; x < grid.Width; x++)
                    for (int y = 0; y < grid.Height; y++)
                    {
                        if (grid[x, y] != CellType.Floor || keep.Contains((x, y))) continue;

                        int walkable = 0;
                        foreach (var (dx, dy) in FourWay)
                            if (grid.Get(x + dx, y + dy).IsWalkable()) walkable++;

                        if (walkable <= 1) // a dead-end tip (or fully stranded) — fill it back in
                        {
                            grid[x, y] = CellType.Water;
                            changed = true;
                        }
                    }
            }
        }
    }
}
