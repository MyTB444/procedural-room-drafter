using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Lands (Plain layout) paths: STRICTLY 2-tile-thick corridors linking all 4 doors and nothing
    /// else — purely visual (cells stay ordinary walkable Floor, only flagged via
    /// <see cref="RoomGrid.MarkPath"/>; the painter paves them from
    /// <see cref="BiomeConfig.PathTileVariants"/>). Every route STARTS right in front of its door,
    /// leaving straight through the mouth along the door's own 2-wide band. Doors connect one by
    /// one to the NEAREST already-connected door: a door pair on different axes joins with a single
    /// L (each leg on its door's band); an opposite pair (W↔E / N↔S) joins with a Z whose connector
    /// runs at a RANDOM column/row — the only wander, so routes stay minimal (no dead ends, no
    /// meander) but differ room to room. Overlapping legs simply share cells.
    /// </summary>
    public class PathPass : IRoomPass
    {
        private struct DoorNode
        {
            public int Band;        // first row (W/E) or column (N/S) of the 2-wide door
            public int MouthX, MouthY; // the cell right in front of the door (on the band's first line)
            public bool Horizontal; // true = exits horizontally (W/E door)
        }

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Plain) return;

            int t = config.WallThickness;
            var nodes = new List<DoorNode>
            {
                new DoorNode { Band = grid.DoorStarts[(int)Cardinal.North], MouthY = grid.Height - t - 1, Horizontal = false },
                new DoorNode { Band = grid.DoorStarts[(int)Cardinal.South], MouthY = t, Horizontal = false },
                new DoorNode { Band = grid.DoorStarts[(int)Cardinal.West], MouthX = t, Horizontal = true },
                new DoorNode { Band = grid.DoorStarts[(int)Cardinal.East], MouthX = grid.Width - t - 1, Horizontal = true },
            };
            for (int i = 0; i < nodes.Count; i++) // the mouth point's other coordinate is the band
            {
                var n = nodes[i];
                if (n.Horizontal) n.MouthY = n.Band; else n.MouthX = n.Band;
                nodes[i] = n;
            }
            rng.Shuffle(nodes);

            for (int i = 1; i < nodes.Count; i++)
            {
                int nearest = 0;
                int best = int.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    int d = System.Math.Abs(nodes[i].MouthX - nodes[j].MouthX) +
                            System.Math.Abs(nodes[i].MouthY - nodes[j].MouthY);
                    if (d < best) { best = d; nearest = j; }
                }
                Connect(grid, rng, t, nodes[i], nodes[nearest]);
            }
        }

        private static void Connect(RoomGrid grid, System.Random rng, int t, DoorNode a, DoorNode b)
        {
            if (a.Horizontal && !b.Horizontal) LRoute(grid, a, b);
            else if (!a.Horizontal && b.Horizontal) LRoute(grid, b, a);
            else if (a.Horizontal) ZHorizontal(grid, rng, t, a, b);
            else ZVertical(grid, rng, t, a, b);
        }

        /// <summary>Different axes: one L — the horizontal door's band runs to the vertical door's
        /// band, which then runs to its mouth. Both legs sit exactly on their door's 2 lines, so
        /// each route leaves its door dead straight.</summary>
        private static void LRoute(RoomGrid grid, DoorNode h, DoorNode v)
        {
            PaveRect(grid, System.Math.Min(h.MouthX, v.Band), System.Math.Max(h.MouthX, v.Band + 1),
                     h.Band, h.Band + 1);
            PaveRect(grid, v.Band, v.Band + 1,
                     System.Math.Min(v.MouthY, h.Band), System.Math.Max(v.MouthY, h.Band + 1));
        }

        /// <summary>W↔E: each door's band runs to a RANDOM connector column, joined vertically.</summary>
        private static void ZHorizontal(RoomGrid grid, System.Random rng, int t, DoorNode a, DoorNode b)
        {
            int cx = rng.Next(t, grid.Width - t - 1); // connector's left column (2 wide fits inside)
            PaveRect(grid, System.Math.Min(a.MouthX, cx), System.Math.Max(a.MouthX, cx + 1), a.Band, a.Band + 1);
            PaveRect(grid, System.Math.Min(b.MouthX, cx), System.Math.Max(b.MouthX, cx + 1), b.Band, b.Band + 1);
            PaveRect(grid, cx, cx + 1,
                     System.Math.Min(a.Band, b.Band), System.Math.Max(a.Band + 1, b.Band + 1));
        }

        /// <summary>N↔S: each door's band runs to a RANDOM connector row, joined horizontally.</summary>
        private static void ZVertical(RoomGrid grid, System.Random rng, int t, DoorNode a, DoorNode b)
        {
            int cy = rng.Next(t, grid.Height - t - 1); // connector's bottom row (2 wide fits inside)
            PaveRect(grid, a.Band, a.Band + 1, System.Math.Min(a.MouthY, cy), System.Math.Max(a.MouthY, cy + 1));
            PaveRect(grid, b.Band, b.Band + 1, System.Math.Min(b.MouthY, cy), System.Math.Max(b.MouthY, cy + 1));
            PaveRect(grid, System.Math.Min(a.Band, b.Band), System.Math.Max(a.Band + 1, b.Band + 1),
                     cy, cy + 1);
        }

        /// <summary>Flag every Floor cell in the inclusive rect as path.</summary>
        private static void PaveRect(RoomGrid grid, int x0, int x1, int y0, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (grid.Get(x, y) == CellType.Floor) grid.MarkPath(x, y);
        }
    }
}
