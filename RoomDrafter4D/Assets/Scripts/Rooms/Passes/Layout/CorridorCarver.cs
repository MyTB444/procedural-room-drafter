using System;
using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Shared corridor carving for the water-based layouts. Both Aqua (<see cref="CorridorPass"/>)
    /// and Halls (<see cref="HallPass"/>) build their walkways by connecting a set of points with
    /// L-shaped corridors over water; <see cref="IslandPass"/> reuses the single-L carve too. Pure
    /// grid ops — no Unity dependency.
    /// </summary>
    public static class CorridorCarver
    {
        /// <summary>
        /// Connect a set of points into ONE network: visit them in random order and carve an
        /// L-corridor from each to the NEAREST already-connected point (a randomized
        /// minimum-spanning shape — keeps total carve length low so the result reads as corridors,
        /// not a flooded field). <paramref name="widthFor"/> picks the width for a connection from
        /// its Manhattan length (so callers vary width however they like).
        /// </summary>
        public static void ConnectNearest(RoomGrid grid, IReadOnlyList<(int x, int y)> nodes,
                                          int margin, Random rng, Func<int, int> widthFor)
        {
            if (nodes == null || nodes.Count < 2) return;

            int[] order = rng.ShuffledIndices(nodes.Count);
            for (int i = 1; i < order.Length; i++)
            {
                var from = nodes[order[i]];
                var to = nodes[order[0]];
                int best = int.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    var visited = nodes[order[j]];
                    int dist = Math.Abs(visited.x - from.x) + Math.Abs(visited.y - from.y);
                    if (dist < best) { best = dist; to = visited; }
                }

                CarveL(grid, from, to, Math.Max(1, widthFor(best)), margin, rng);
            }
        }

        /// <summary>
        /// Connect a set of points into ONE network while ROUTING AROUND walls — each new node is
        /// linked to its nearest already-connected node by a BFS path that only travels Water/Floor
        /// (never Wall). Used by <see cref="HallPass"/> so the path threads the water between igrooms
        /// instead of plowing through their walls (which would leave them open). Carves width 1.
        /// </summary>
        public static void ConnectThroughWater(RoomGrid grid, IReadOnlyList<(int x, int y)> nodes,
                                               int margin, Random rng)
        {
            if (nodes == null || nodes.Count < 2) return;

            int[] order = rng.ShuffledIndices(nodes.Count);
            var connected = new List<(int x, int y)> { nodes[order[0]] };
            for (int i = 1; i < order.Length; i++)
            {
                var from = nodes[order[i]];
                var to = connected[0];
                int best = int.MaxValue;
                foreach (var c in connected)
                {
                    int dist = Math.Abs(c.x - from.x) + Math.Abs(c.y - from.y);
                    if (dist < best) { best = dist; to = c; }
                }
                CarveWaterPath(grid, from, to, margin);
                connected.Add(from);
            }
        }

        /// <summary>
        /// Grow the floor outward into the water by <paramref name="iterations"/> cells: each pass
        /// turns every Water cell touching a Floor cell into Floor. Walls block it and enclosed floor
        /// (e.g. a Halls igroom interior, ringed by its own walls) has no water to spread into — so
        /// this only fattens the open corridors/landings while shrinking the water. Stays inside the
        /// <paramref name="margin"/> ring so the room keeps its water boundary.
        /// </summary>
        public static void GrowFloorIntoWater(RoomGrid grid, int margin, int iterations)
        {
            var toFloor = new List<(int x, int y)>();
            for (int i = 0; i < iterations; i++)
            {
                toFloor.Clear();
                for (int x = margin; x < grid.Width - margin; x++)
                    for (int y = margin; y < grid.Height - margin; y++)
                    {
                        if (grid[x, y] != CellType.Water) continue;
                        if (grid.Get(x + 1, y) == CellType.Floor || grid.Get(x - 1, y) == CellType.Floor ||
                            grid.Get(x, y + 1) == CellType.Floor || grid.Get(x, y - 1) == CellType.Floor)
                            toFloor.Add((x, y));
                    }
                foreach (var (x, y) in toFloor)
                    grid[x, y] = CellType.Floor;
            }
        }

        private static readonly (int dx, int dy)[] FourWay = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>BFS the shortest Water/Floor path from <paramref name="from"/> to
        /// <paramref name="to"/> (walls block it) and carve it to Floor. No-op if walled off.</summary>
        private static void CarveWaterPath(RoomGrid grid, (int x, int y) from, (int x, int y) to, int margin)
        {
            var came = new Dictionary<(int x, int y), (int x, int y)> { [from] = from };
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(from);
            bool found = false;

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to) { found = true; break; }
                foreach (var (dx, dy) in FourWay)
                {
                    var n = (x: c.x + dx, y: c.y + dy);
                    if (came.ContainsKey(n) || !grid.IsInterior(n.x, n.y, margin)) continue;
                    var cell = grid[n.x, n.y];
                    if (cell != CellType.Water && cell != CellType.Floor && n != to) continue;
                    came[n] = c;
                    queue.Enqueue(n);
                }
            }
            if (!found) return;

            for (var c = to; c != from; c = came[c])
                grid[c.x, c.y] = CellType.Floor;
            grid[from.x, from.y] = CellType.Floor;
        }

        /// <summary>L-shaped corridor between two points; the elbow direction is random.</summary>
        public static void CarveL(RoomGrid grid, (int x, int y) a, (int x, int y) b,
                                  int width, int margin, Random rng)
        {
            var corner = rng.Next(2) == 0 ? (b.x, a.y) : (a.x, b.y);
            CarveSegment(grid, a, corner, width, margin);
            CarveSegment(grid, corner, b, width, margin);
        }

        /// <summary>Carve along one axis-aligned segment, one block per step.</summary>
        private static void CarveSegment(RoomGrid grid, (int x, int y) a, (int x, int y) b, int width, int margin)
        {
            int dx = Math.Sign(b.x - a.x);
            int dy = Math.Sign(b.y - a.y);
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
        /// its width. Centring uses (width-1)/2 so even widths extend up/right of the cell — the same
        /// way doors/landings extend from their anchor — keeping 2-wide corridors flush with the
        /// 2-wide door openings instead of staggered one cell off them.
        /// </summary>
        private static void CarveBlock(RoomGrid grid, int cx, int cy, int width, int margin)
        {
            int ax = Math.Clamp(cx - (width - 1) / 2, margin, grid.Width - margin - width);
            int ay = Math.Clamp(cy - (width - 1) / 2, margin, grid.Height - margin - width);
            for (int x = ax; x < ax + width; x++)
                for (int y = ay; y < ay + width; y++)
                    grid[x, y] = CellType.Floor;
        }
    }
}
