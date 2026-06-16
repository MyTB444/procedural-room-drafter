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
