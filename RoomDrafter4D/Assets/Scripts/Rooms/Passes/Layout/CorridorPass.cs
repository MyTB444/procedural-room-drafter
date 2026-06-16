using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Aqua-style layout: the interior is open water crossed by a random network of straight walkway
    /// corridors. Nodes are the 4 door landings plus a few random junction points; <see cref="CorridorCarver"/>
    /// connects each to its nearest already-connected node with an L-corridor, so the whole network
    /// (doors included) is connected by construction. Per-connection width: usually 2; 3 on the
    /// MediumCorridorChance roll; a rare short 4-wide accent (WideCorridorChance, capped per room).
    /// </summary>
    public class CorridorPass : IRoomPass
    {
        /// <summary>Hard cap on 4-wide corridors per room — they're accents, not the norm.</summary>
        private const int MaxWideCorridors = 2;

        private static readonly Cardinal[] FourDirections =
            { Cardinal.North, Cardinal.East, Cardinal.South, Cardinal.West };

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;

            // 1) All water inside the wall ring.
            for (int x = t; x < grid.Width - t; x++)
                for (int y = t; y < grid.Height - t; y++)
                    grid[x, y] = CellType.Water;

            // 2) Network nodes: the 4 door landings + random interior junctions.
            var nodes = new List<(int x, int y)>(4 + config.ExtraCorridorNodes);
            foreach (var d in FourDirections)
            {
                var landing = RoomDoors.Landing(config, grid, d);
                nodes.Add((landing.x, landing.y));
            }
            for (int i = 0; i < config.ExtraCorridorNodes; i++)
                nodes.Add((rng.Next(t, grid.Width - t), rng.Next(t, grid.Height - t)));

            // 3) Connect them. Wide (4) corridors are rare, SHORT-ish accents: capped per room and
            // only on shorter hops, so the longest door-to-door runs stay 2 wide.
            int maxWideLength = (grid.Width + grid.Height) / 3;
            int wideCount = 0;
            CorridorCarver.ConnectNearest(grid, nodes, t, rng, length =>
            {
                bool wide = wideCount < MaxWideCorridors
                            && length <= maxWideLength
                            && rng.NextDouble() < config.WideCorridorChance;
                if (wide) { wideCount++; return 4; }              // two 2×2 patches side by side
                return rng.NextDouble() < config.MediumCorridorChance ? 3 : 2; // 3 = patch + a strip
            });
        }
    }
}
