using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Aqua-style layout: the interior is open water crossed by a random network of straight
    /// walkway corridors. Each connection is an L-shaped corridor, randomly 2 or 4 cells wide —
    /// never 1, because blocks are shifted whole (not clipped) at the wall margin. Nodes are the
    /// 4 door landings plus a few random junction points; every node is connected to an
    /// already-connected node, so the whole network (doors included) is connected by construction.
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

            // 3) Visit nodes in random order; hook each onto the NEAREST already-visited node.
            // Nearest (not random) keeps total carve length minimal — random partners criss-cross
            // the small room and the overlapping corridors merge into one big floor field.
            // Wide (4) corridors are rare, SHORT-ish accents: capped per room and only allowed on
            // shorter hops, so the longest door-to-door runs never go wide.
            int maxWideLength = (grid.Width + grid.Height) / 3;
            int wideCount = 0;

            int[] order = rng.ShuffledIndices(nodes.Count);
            for (int i = 1; i < order.Length; i++)
            {
                var from = nodes[order[i]];
                var to = nodes[order[0]];
                int best = int.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    var visited = nodes[order[j]];
                    int dist = System.Math.Abs(visited.x - from.x) + System.Math.Abs(visited.y - from.y);
                    if (dist < best) { best = dist; to = visited; }
                }

                bool wide = wideCount < MaxWideCorridors
                            && best <= maxWideLength
                            && rng.NextDouble() < config.WideCorridorChance;
                if (wide) wideCount++;
                // 4 = two 2×2 patches side by side; 3 = a patch + a strip of plain floor; else 2.
                int width = wide ? 4
                    : rng.NextDouble() < config.MediumCorridorChance ? 3
                    : 2;
                CarveL(grid, from, to, width, t, rng);
            }
        }

        /// <summary>L-shaped corridor between two points; the elbow direction is random.
        /// Shared with <see cref="IslandPass"/>.</summary>
        internal static void CarveL(RoomGrid grid, (int x, int y) a, (int x, int y) b,
                                    int width, int margin, System.Random rng)
        {
            var corner = rng.Next(2) == 0 ? (b.x, a.y) : (a.x, b.y);
            CarveSegment(grid, a, corner, width, margin);
            CarveSegment(grid, corner, b, width, margin);
        }

        /// <summary>Carve along one axis-aligned segment, one block per step.</summary>
        private static void CarveSegment(RoomGrid grid, (int x, int y) a, (int x, int y) b,
                                         int width, int margin)
        {
            int dx = System.Math.Sign(b.x - a.x);
            int dy = System.Math.Sign(b.y - a.y);
            int x = a.x, y = a.y;

            CarveBlock(grid, x, y, width, margin);
            while (x != b.x || y != b.y)
            {
                x += dx;
                y += dy;
                CarveBlock(grid, x, y, width, margin);
            }
        }

        /// <summary>
        /// Carve a FULL width×width floor block roughly centred on the cell. At the wall margin the
        /// block is shifted inward instead of clipped, so a corridor can never end up thinner than
        /// its width (a clipped block would leave 1-wide slivers along the walls).
        /// Centring uses (width-1)/2 so even widths extend up/right of the cell — the same way
        /// doors/landings extend from their anchor — keeping 2-wide corridors flush with the
        /// 2-wide door openings instead of staggered one cell off them.
        /// </summary>
        private static void CarveBlock(RoomGrid grid, int cx, int cy, int width, int margin)
        {
            int ax = System.Math.Clamp(cx - (width - 1) / 2, margin, grid.Width - margin - width);
            int ay = System.Math.Clamp(cy - (width - 1) / 2, margin, grid.Height - margin - width);
            for (int x = ax; x < ax + width; x++)
                for (int y = ay; y < ay + width; y++)
                    grid[x, y] = CellType.Floor;
        }
    }
}
