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
        /// Connect a set of points into ONE network while ROUTING AROUND walls, carving corridors
        /// <paramref name="width"/> tiles wide. Each node is linked to the rest of the network by a
        /// BFS that travels only Water (or cells already carved into the network) — never Wall and
        /// never an igroom's interior Floor — so the corridor threads the water BETWEEN igrooms and
        /// merges into earlier corridors, instead of plowing through their walls or cutting through a
        /// room. Used by <see cref="HallPass"/>. Connectivity is guaranteed as long as every node
        /// sits in the room's single open-water region (the caller keeps igroom footprints inset from
        /// the wall margin so each doorway approach does).
        /// </summary>
        public static void ConnectThroughWater(RoomGrid grid, IReadOnlyList<(int x, int y)> nodes,
                                               int margin, Random rng, int width = 1)
        {
            if (nodes == null || nodes.Count < 2) return;

            int[] order = rng.ShuffledIndices(nodes.Count);
            var network = new HashSet<(int x, int y)>();
            CarveWide(grid, nodes[order[0]], width, margin, network); // seed the network
            for (int i = 1; i < order.Length; i++)
                CarveToNetwork(grid, nodes[order[i]], network, margin, width, rng);
        }

        /// <summary>
        /// BFS from <paramref name="from"/> over Water (and cells already in the network) until it
        /// reaches the network, then carve that path <paramref name="width"/> wide. The network is the
        /// set of corridor cells carved so far; igroom interior Floor is excluded so corridors never
        /// route through a room. No-op if the node is somehow walled off from the network.
        /// </summary>
        private static void CarveToNetwork(RoomGrid grid, (int x, int y) from, HashSet<(int x, int y)> network,
                                           int margin, int width, Random rng)
        {
            if (network.Contains(from)) return;

            var came = new Dictionary<(int x, int y), (int x, int y)> { [from] = from };
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(from);
            (int x, int y) hit = from;
            bool found = false;

            while (queue.Count > 0 && !found)
            {
                var c = queue.Dequeue();
                foreach (var (dx, dy) in FourWay)
                {
                    var n = (x: c.x + dx, y: c.y + dy);
                    if (came.ContainsKey(n) || !grid.IsInterior(n.x, n.y, margin)) continue;
                    if (network.Contains(n)) { came[n] = c; hit = n; found = true; break; }
                    if (grid[n.x, n.y] != CellType.Water) continue; // walls + room interiors block
                    came[n] = c;
                    queue.Enqueue(n);
                }
            }
            if (!found) return; // node is walled off from the network — leave it (shouldn't happen)

            // Carve every cell from `from` up to (but not including) the network cell it joined.
            for (var c = came[hit]; ; c = came[c])
            {
                CarveWide(grid, c, width, margin, network);
                if (c == from) break;
            }
        }

        /// <summary>
        /// Widen the corridor to <paramref name="width"/> tiles AT <paramref name="cell"/> by carving a
        /// width×width Floor block that CONTAINS the cell — choosing the placement (the cell may sit at
        /// any position in the block) that is fully interior and wall-free, so the block widens toward
        /// OPEN water instead of always +x/+y. That keeps the corridor a full 2 tiles even where the
        /// path hugs an igroom wall (a fixed +x/+y block would clip into the wall and pinch to 1). Only
        /// Water cells in the block are turned to Floor — Walls/Doors/existing Floor are left as-is, so
        /// it never breaches a room or merges into its interior. Falls back to carving just the cell in
        /// a genuine 1-wide squeeze (e.g. a 1-cell moat between two igrooms). Carved cells join
        /// <paramref name="network"/> so later corridors can fuse with this one.
        /// </summary>
        private static void CarveWide(RoomGrid grid, (int x, int y) cell, int width, int margin,
                                      HashSet<(int x, int y)> network)
        {
            // Prefer the +x/+y placement (ox=oy=0) for a uniform look in the open, then fall back to
            // placements that extend the other way so the block can dodge an adjacent wall.
            for (int ox = 0; ox > -width; ox--)
                for (int oy = 0; oy > -width; oy--)
                {
                    int ax = cell.x + ox, ay = cell.y + oy;
                    if (!BlockClear(grid, ax, ay, width, margin)) continue;

                    for (int x = ax; x < ax + width; x++)
                        for (int y = ay; y < ay + width; y++)
                        {
                            if (grid[x, y] == CellType.Water) grid[x, y] = CellType.Floor;
                            network.Add((x, y));
                        }
                    return;
                }

            // No wall-free block fits here — carve the single cell so the path stays connected.
            if (grid.IsInterior(cell.x, cell.y, margin) && grid[cell.x, cell.y] == CellType.Water)
                grid[cell.x, cell.y] = CellType.Floor;
            network.Add(cell);
        }

        /// <summary>True if every cell of the width×width block at (ax, ay) is interior and not a
        /// Wall/Door — i.e. the block can become a solid floor patch without breaching anything.</summary>
        private static bool BlockClear(RoomGrid grid, int ax, int ay, int width, int margin)
        {
            for (int x = ax; x < ax + width; x++)
                for (int y = ay; y < ay + width; y++)
                {
                    if (!grid.IsInterior(x, y, margin)) return false;
                    var cell = grid[x, y];
                    if (cell == CellType.Wall || cell == CellType.Door) return false;
                }
            return true;
        }

        private static readonly (int dx, int dy)[] FourWay = { (1, 0), (-1, 0), (0, 1), (0, -1) };

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
