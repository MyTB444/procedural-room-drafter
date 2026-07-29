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
    ///
    /// <see cref="RouteAvoiding"/> routes the same network around RESERVED rectangles (the grass
    /// patches' reshape, see <see cref="GrassPass"/>): Z connectors are rolled outside every rect's
    /// span, a band leg that would cross a rect BRIDGES over it with two short jogs (recursively,
    /// so a bridge can itself bridge the next rect), and an L whose corner falls inside a rect
    /// detours around it — every segment still strictly 2 wide.
    /// </summary>
    public class PathPass : IRoomPass
    {
        private struct DoorNode
        {
            public int Band;           // first row (W/E) or column (N/S) of the 2-wide door
            public int MouthX, MouthY; // the cell right in front of the door (on the band's first line)
            public bool Horizontal;    // true = exits horizontally (W/E door)
        }

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Plain) return;
            Route(grid, rng, config);
        }

        /// <summary>Roll one full path network (all 4 doors).</summary>
        internal static void Route(RoomGrid grid, System.Random rng, BiomeConfig config) =>
            RouteCore(grid, rng, config, null);

        /// <summary>Roll the network while keeping every path cell OUT of the blocked rects
        /// (inclusive bounds, may poke past the interior) — used by <see cref="GrassPass"/>'s
        /// reshape, which reserves the grass footprints (plus their 1-tile buffer) first.</summary>
        internal static void RouteAvoiding(RoomGrid grid, System.Random rng, BiomeConfig config,
                                           List<(int x0, int y0, int x1, int y1)> blocked) =>
            RouteCore(grid, rng, config, blocked);

        private static void RouteCore(RoomGrid grid, System.Random rng, BiomeConfig config,
                                      List<(int x0, int y0, int x1, int y1)> blocked)
        {
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
                Connect(grid, rng, t, nodes[i], nodes[nearest], blocked);
            }
        }

        private static void Connect(RoomGrid grid, System.Random rng, int t, DoorNode a, DoorNode b,
                                    List<(int x0, int y0, int x1, int y1)> blocked)
        {
            if (a.Horizontal && !b.Horizontal) LRoute(grid, t, a, b, blocked);
            else if (!a.Horizontal && b.Horizontal) LRoute(grid, t, b, a, blocked);
            else if (a.Horizontal) ZHorizontal(grid, rng, t, a, b, blocked);
            else ZVertical(grid, rng, t, a, b, blocked);
        }

        /// <summary>Different axes: one L — the horizontal door's band runs to the vertical door's
        /// band, which then runs to its mouth. If the L's corner would land inside a blocked rect,
        /// the route detours around that rect instead (4 clean segments, themselves bridge-aware
        /// against the remaining rects).</summary>
        private static void LRoute(RoomGrid grid, int t, DoorNode h, DoorNode v,
                                   List<(int x0, int y0, int x1, int y1)> blocked)
        {
            if (blocked != null)
                foreach (var r in blocked)
                {
                    if (!Overlaps(v.Band, v.Band + 1, r.x0, r.x1) ||
                        !Overlaps(h.Band, h.Band + 1, r.y0, r.y1)) continue;

                    // Corner inside this rect: mouth-side column, then v-mouth-side row, around it.
                    var rest = Without(blocked, r);
                    int cs = h.MouthX < r.x0 ? r.x0 - 2 : r.x1 + 1;
                    int rs = v.MouthY > r.y1 ? r.y1 + 1 : r.y0 - 2;
                    PaveH(grid, t, rest, h.Band, System.Math.Min(h.MouthX, cs), System.Math.Max(h.MouthX, cs + 1));
                    PaveV(grid, t, rest, cs, System.Math.Min(h.Band, rs), System.Math.Max(h.Band + 1, rs + 1));
                    PaveH(grid, t, rest, rs, System.Math.Min(cs, v.Band), System.Math.Max(cs + 1, v.Band + 1));
                    PaveV(grid, t, rest, v.Band, System.Math.Min(rs, v.MouthY), System.Math.Max(rs + 1, v.MouthY));
                    return;
                }

            PaveH(grid, t, blocked, h.Band,
                  System.Math.Min(h.MouthX, v.Band), System.Math.Max(h.MouthX, v.Band + 1));
            PaveV(grid, t, blocked, v.Band,
                  System.Math.Min(v.MouthY, h.Band), System.Math.Max(v.MouthY, h.Band + 1));
        }

        /// <summary>W↔E: each door's band runs to a RANDOM connector column, joined vertically.
        /// With blocked rects the connector is rolled outside all their columns.</summary>
        private static void ZHorizontal(RoomGrid grid, System.Random rng, int t, DoorNode a, DoorNode b,
                                        List<(int x0, int y0, int x1, int y1)> blocked)
        {
            int cx = RollOutside(rng, t, grid.Width - t - 2, blocked, cols: true);
            PaveH(grid, t, blocked, a.Band, System.Math.Min(a.MouthX, cx), System.Math.Max(a.MouthX, cx + 1));
            PaveH(grid, t, blocked, b.Band, System.Math.Min(b.MouthX, cx), System.Math.Max(b.MouthX, cx + 1));
            PaveV(grid, t, blocked, cx, System.Math.Min(a.Band, b.Band), System.Math.Max(a.Band + 1, b.Band + 1));
        }

        /// <summary>N↔S: each door's band runs to a RANDOM connector row, joined horizontally.
        /// With blocked rects the connector is rolled outside all their rows.</summary>
        private static void ZVertical(RoomGrid grid, System.Random rng, int t, DoorNode a, DoorNode b,
                                      List<(int x0, int y0, int x1, int y1)> blocked)
        {
            int cy = RollOutside(rng, t, grid.Height - t - 2, blocked, cols: false);
            PaveV(grid, t, blocked, a.Band, System.Math.Min(a.MouthY, cy), System.Math.Max(a.MouthY, cy + 1));
            PaveV(grid, t, blocked, b.Band, System.Math.Min(b.MouthY, cy), System.Math.Max(b.MouthY, cy + 1));
            PaveH(grid, t, blocked, cy, System.Math.Min(a.Band, b.Band), System.Math.Max(a.Band + 1, b.Band + 1));
        }

        /// <summary>A 2-wide connector value whose band c..c+1 avoids every blocked rect's span
        /// (columns or rows) — uniform over the valid values; plain roll when nothing is blocked.</summary>
        private static int RollOutside(System.Random rng, int lo, int hi,
                                       List<(int x0, int y0, int x1, int y1)> blocked, bool cols)
        {
            if (blocked == null || blocked.Count == 0) return rng.Next(lo, hi + 1);

            var valid = new List<int>();
            for (int c = lo; c <= hi; c++)
            {
                bool ok = true;
                foreach (var r in blocked)
                {
                    int exLo = cols ? r.x0 : r.y0, exHi = cols ? r.x1 : r.y1;
                    if (Overlaps(c, c + 1, exLo, exHi)) { ok = false; break; }
                }
                if (ok) valid.Add(c);
            }
            return valid.Count > 0 ? valid[rng.Next(valid.Count)] : rng.Next(lo, hi + 1);
        }

        /// <summary>Horizontal 2-wide band; a crossed blocked rect is BRIDGED with two jogs, each
        /// sub-segment recursively avoiding the remaining rects. Endpoints are guaranteed outside
        /// every rect by the reshape's spot rules (door safety + connector rolls).</summary>
        private static void PaveH(RoomGrid grid, int t, List<(int x0, int y0, int x1, int y1)> blocked,
                                  int r0, int xLo, int xHi)
        {
            if (blocked != null)
                foreach (var r in blocked)
                {
                    if (!Overlaps(r0, r0 + 1, r.y0, r.y1) || !Overlaps(xLo, xHi, r.x0, r.x1)) continue;

                    var rest = Without(blocked, r);
                    int jr = r.y1 + 2 <= grid.Height - t - 1 ? r.y1 + 1 : r.y0 - 2; // jog rows
                    int jw = System.Math.Max(t, r.x0 - 2);                          // west jog cols
                    int je = System.Math.Min(grid.Width - t - 2, r.x1 + 1);         // east jog cols
                    PaveH(grid, t, rest, r0, xLo, r.x0 - 1);
                    PaveH(grid, t, rest, r0, r.x1 + 1, xHi);
                    PaveV(grid, t, rest, jw, System.Math.Min(r0, jr), System.Math.Max(r0 + 1, jr + 1));
                    PaveV(grid, t, rest, je, System.Math.Min(r0, jr), System.Math.Max(r0 + 1, jr + 1));
                    PaveH(grid, t, rest, jr, jw, je + 1);
                    return;
                }

            PaveRect(grid, xLo, xHi, r0, r0 + 1);
        }

        /// <summary>Vertical 2-wide band; a crossed blocked rect is bridged, like <see cref="PaveH"/>.</summary>
        private static void PaveV(RoomGrid grid, int t, List<(int x0, int y0, int x1, int y1)> blocked,
                                  int c0, int yLo, int yHi)
        {
            if (blocked != null)
                foreach (var r in blocked)
                {
                    if (!Overlaps(c0, c0 + 1, r.x0, r.x1) || !Overlaps(yLo, yHi, r.y0, r.y1)) continue;

                    var rest = Without(blocked, r);
                    int jc = r.x1 + 2 <= grid.Width - t - 1 ? r.x1 + 1 : r.x0 - 2;   // jog cols
                    int js = System.Math.Max(t, r.y0 - 2);                           // south jog rows
                    int jn = System.Math.Min(grid.Height - t - 2, r.y1 + 1);         // north jog rows
                    PaveV(grid, t, rest, c0, yLo, r.y0 - 1);
                    PaveV(grid, t, rest, c0, r.y1 + 1, yHi);
                    PaveH(grid, t, rest, js, System.Math.Min(c0, jc), System.Math.Max(c0 + 1, jc + 1));
                    PaveH(grid, t, rest, jn, System.Math.Min(c0, jc), System.Math.Max(c0 + 1, jc + 1));
                    PaveV(grid, t, rest, jc, js, jn + 1);
                    return;
                }

            PaveRect(grid, c0, c0 + 1, yLo, yHi);
        }

        private static List<(int x0, int y0, int x1, int y1)> Without(
            List<(int x0, int y0, int x1, int y1)> blocked, (int x0, int y0, int x1, int y1) rect)
        {
            var rest = new List<(int x0, int y0, int x1, int y1)>(blocked);
            rest.Remove(rect);
            return rest;
        }

        private static bool Overlaps(int lo, int hi, int otherLo, int otherHi) =>
            hi >= otherLo && lo <= otherHi;

        /// <summary>Flag every Floor cell in the inclusive rect as path (empty ranges no-op).</summary>
        private static void PaveRect(RoomGrid grid, int x0, int x1, int y0, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (grid.Get(x, y) == CellType.Floor) grid.MarkPath(x, y);
        }
    }
}
