using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Lands (Plain layout) grass gradient: 1–2 grass patches whose WHOLE footprint — a FULL-grass
    /// core (2–3 × 2–3, perfect rectangle) + 1-thick HALF-grass ring + 1-thick VERY-FEW-grass
    /// ring + 1–2 thick NO-grass ring (only the OUTER ring keeps a roll) — is NESTED PERFECT
    /// RECTANGLES (levels = Chebyshev distance to the core rect); everything beyond is the PLAIN
    /// floor. The footprint is validated BEFORE
    /// anything is marked (every cell plain Floor, ≥1 tile clear of every path, ≥
    /// <see cref="MinPatchGap"/> clear of the other patch — never on/beside the same corner) —
    /// all or nothing, never clipped.
    ///
    /// Placement tiers, so a room ALWAYS gets the rolled patch count:
    /// 1. Corners farthest-from-path first (rolled size, then minimal), falling through to nearer
    ///    corners, topping up with the best free spot (<see cref="PlaceAnywhere"/>).
    /// 2. Nothing placed at all: reroute the paths (fresh random routes) up to
    ///    <see cref="MaxPathReroutes"/> times until something fits.
    /// 3. Still short of the count (mid-edge door bands can pin EVERY footprint): FULL RESHAPE —
    ///    wipe paths+grass, RESERVE door-safe spots for every patch (distinct corners first, then
    ///    free spots, all ≥ MinPatchGap apart), route the paths AROUND them
    ///    (<see cref="PathPass.RouteAvoiding"/> bridges/detours), then mark the patches.
    /// </summary>
    public class GrassPass : IRoomPass
    {
        /// <summary>Cap on tier-2 path rerolls when nothing places at all.</summary>
        private const int MaxPathReroutes = 8;

        /// <summary>Min Chebyshev gap between two patch FOOTPRINTS — patches never share a corner
        /// or sit flush against each other, and their reshape jogs never collide.</summary>
        private const int MinPatchGap = 4;

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Plain) return;

            int t = config.WallThickness;
            int count = rng.Next(3) == 0 ? 1 : 2; // 2 patches twice as often as 1
            int placed = TryPlacePatches(grid, rng, t, count);
            if (placed >= count) return;

            // Tier 2: with NOTHING placed the paths may just be unluckily routed — reroll them.
            if (placed == 0)
                for (int attempt = 0; attempt < MaxPathReroutes; attempt++)
                {
                    grid.ClearPaths();
                    PathPass.Route(grid, rng, config);
                    placed = TryPlacePatches(grid, rng, t, count);
                    if (placed >= count) return;
                    if (placed > 0) break; // partial success — rerolls won't find the 2nd footprint
                }

            // Tier 3: guarantee the full count — reserve every patch first, route around them.
            if (placed < count)
                FullReshape(grid, rng, config, t, count);
        }

        /// <summary>Place up to <paramref name="count"/> patches: corners farthest-from-path first
        /// (falling through to nearer ones), then the best free spot for any remainder. Returns how
        /// many actually placed.</summary>
        private static int TryPlacePatches(RoomGrid grid, System.Random rng, int t, int count)
        {
            var corners = new List<(int x, int y)>
            {
                (t, t),
                (grid.Width - t - 1, t),
                (t, grid.Height - t - 1),
                (grid.Width - t - 1, grid.Height - t - 1),
            };
            corners.Sort((a, b) => PathDistance(grid, b).CompareTo(PathDistance(grid, a)));

            int placed = 0;
            foreach (var corner in corners)
            {
                if (placed == count) break;
                if (GrowPatch(grid, rng, t, corner)) placed++;
            }
            while (placed < count && PlaceAnywhere(grid, t)) placed++;
            return placed;
        }

        /// <summary>One corner patch: rolled size first, minimal size as fallback, else nothing.
        /// Returns whether a patch actually placed.</summary>
        private static bool GrowPatch(RoomGrid grid, System.Random rng, int t, (int x, int y) corner)
        {
            int w = rng.Next(2, 4);         // full-grass core: 2–3 wide ×
            int h = rng.Next(2, 4);         // 2–3 tall — a perfect rectangle or square
            int noneThick = rng.Next(1, 3); // only the OUTER (no-grass) ring keeps its 1–2 roll

            int cx0 = corner.x == t ? t : corner.x - w + 1;
            int cy0 = corner.y == t ? t : corner.y - h + 1;
            if (TryPlaceAt(grid, t, cx0, cy0, w, h, 1, 1, noneThick)) return true;

            cx0 = corner.x == t ? t : corner.x - 1; // minimal 2×2 core
            cy0 = corner.y == t ? t : corner.y - 1;
            return TryPlaceAt(grid, t, cx0, cy0, 2, 2, 1, 1, 1);
        }

        /// <summary>Scan every position for the MINIMAL patch and place it at the valid spot whose
        /// core sits farthest from the paths. No rng — deterministic best. Returns whether anything
        /// placed.</summary>
        private static bool PlaceAnywhere(RoomGrid grid, int t)
        {
            int bestScore = -1, bestX = 0, bestY = 0;
            for (int x0 = t; x0 <= grid.Width - t - 2; x0++)
                for (int y0 = t; y0 <= grid.Height - t - 2; y0++)
                {
                    if (!ValidateAt(grid, t, x0, y0, 2, 2, 3)) continue;
                    int score = PathDistance(grid, (x0, y0)); // core vs paths
                    if (score > bestScore) { bestScore = score; bestX = x0; bestY = y0; }
                }
            if (bestScore < 0) return false;
            return TryPlaceAt(grid, t, bestX, bestY, 2, 2, 1, 1, 1);
        }

        // ---- Tier 3: reserve first, route around ---------------------------------------------

        /// <summary>Reserve door-safe spots for EVERY patch (distinct corners first — never the
        /// same corner — then row-constrained free spots, all ≥ MinPatchGap apart), wipe paths +
        /// grass, route the paths around the reserved footprints, then mark the patches.</summary>
        private static void FullReshape(RoomGrid grid, System.Random rng, BiomeConfig config, int t, int count)
        {
            grid.ClearPaths();
            grid.ClearGrass();

            var doorCells = new List<(int x, int y)>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid[x, y] == CellType.Door) doorCells.Add((x, y));

            var reserved = new List<(int cx0, int cy0, int w, int h, int half, int few, int none)>();
            var footprints = new List<(int x0, int y0, int x1, int y1)>();

            // Corners first, farthest from the doors first — one patch per corner at most.
            var corners = new List<(int x, int y)>
            {
                (t, t),
                (grid.Width - t - 1, t),
                (t, grid.Height - t - 1),
                (grid.Width - t - 1, grid.Height - t - 1),
            };
            corners.Sort((a, b) => DoorDistance(doorCells, b).CompareTo(DoorDistance(doorCells, a)));
            foreach (var corner in corners)
            {
                if (reserved.Count == count) break;
                // Reserving on a path-free field would let big rolled rings always fit and clamp
                // into near-identical MASSIVE patches — so the reshape rolls the core size only
                // and keeps 1-thick rings (footprint 5–6 per axis), falling back to minimal.
                int w = rng.Next(2, 4), h = rng.Next(2, 4);
                int cx0 = corner.x == t ? t : corner.x - w + 1;
                int cy0 = corner.y == t ? t : corner.y - h + 1;
                if (TryReserve(grid, doorCells, footprints, reserved, t, cx0, cy0, w, h, 1, 1, 1))
                    continue;
                cx0 = corner.x == t ? t : corner.x - 1;
                cy0 = corner.y == t ? t : corner.y - 1;
                TryReserve(grid, doorCells, footprints, reserved, t, cx0, cy0, 2, 2, 1, 1, 1);
            }

            // Free spots for any remainder: rows sized so a jog band fits beside the blocked rect,
            // best door distance first.
            while (reserved.Count < count)
            {
                var rowChoices = new List<int> { t + 3, grid.Height - t - 5 };
                if (rowChoices[1] == rowChoices[0]) rowChoices.RemoveAt(1);
                int bestScore = int.MinValue, bestX = 0, bestY = 0;
                foreach (int cy0 in rowChoices)
                {
                    if (cy0 - 3 < t || cy0 + 4 > grid.Height - t - 1) continue;
                    for (int cx0 = t + 3; cx0 + 4 <= grid.Width - t - 1; cx0++)
                    {
                        int score = ReservationScore(grid, doorCells, footprints, t,
                                                     Footprint(grid, t, cx0, cy0, 2, 2, 3));
                        if (score > bestScore) { bestScore = score; bestX = cx0; bestY = cy0; }
                    }
                }
                if (bestScore == int.MinValue) break; // no more room — place what we have
                TryReserve(grid, doorCells, footprints, reserved, t, bestX, bestY, 2, 2, 1, 1, 1);
            }

            if (reserved.Count == 0) // nothing reservable at all — restore plain paths and bail
            {
                PathPass.Route(grid, rng, config);
                return;
            }

            var blocked = new List<(int x0, int y0, int x1, int y1)>();
            foreach (var f in footprints)
                blocked.Add((f.x0 - 1, f.y0 - 1, f.x1 + 1, f.y1 + 1)); // + the 1-tile path buffer
            PathPass.RouteAvoiding(grid, rng, config, blocked);

            foreach (var p in reserved)
                TryPlaceAt(grid, t, p.cx0, p.cy0, p.w, p.h, p.half, p.few, p.none);
        }

        /// <summary>Validate a reservation (no paths exist yet), recording it on success. Rules:
        /// footprint in plain floor; ≥3 from every door cell (mouths stay clear of the blocked
        /// rect); a jog band fits on at least one side per axis; ≥ MinPatchGap from every other
        /// reservation; and the combined blocked rects must still leave the router at least one
        /// legal Z-connector column AND row — otherwise the router's fallback would pave through
        /// a footprint (weird paths + a failed patch).</summary>
        private static bool TryReserve(RoomGrid grid, List<(int x, int y)> doorCells,
                                       List<(int x0, int y0, int x1, int y1)> footprints,
                                       List<(int cx0, int cy0, int w, int h, int half, int few, int none)> reserved,
                                       int t, int cx0, int cy0, int w, int h, int half, int few, int none)
        {
            var f = Footprint(grid, t, cx0, cy0, w, h, half + few + none);
            if (ReservationScore(grid, doorCells, footprints, t, f) == int.MinValue) return false;

            reserved.Add((cx0, cy0, w, h, half, few, none));
            footprints.Add(f);
            return true;
        }

        /// <summary>Score a reservation footprint (int.MinValue = invalid), preferring door
        /// distance. Shared by corner reservations and the free-spot scan.</summary>
        private static int ReservationScore(RoomGrid grid, List<(int x, int y)> doorCells,
                                            List<(int x0, int y0, int x1, int y1)> footprints,
                                            int t, (int x0, int y0, int x1, int y1) f)
        {
            for (int x = f.x0; x <= f.x1; x++)
                for (int y = f.y0; y <= f.y1; y++)
                    if (grid.Get(x, y) != CellType.Floor) return int.MinValue;

            int doorDist = int.MaxValue;
            foreach (var d in doorCells)
                doorDist = System.Math.Min(doorDist, RectDistance(d, f.x0, f.y0, f.x1, f.y1));
            if (doorDist < 3) return int.MinValue;

            bool xJog = f.x1 + 3 <= grid.Width - t - 1 || f.x0 - 3 >= t;
            bool yJog = f.y1 + 3 <= grid.Height - t - 1 || f.y0 - 3 >= t;
            if (!xJog || !yJog) return int.MinValue;

            foreach (var other in footprints)
                if (RectGap(f, other) < MinPatchGap) return int.MinValue;

            if (!HasFreeBand(t, grid.Width - t - 2, footprints, f, cols: true) ||
                !HasFreeBand(t, grid.Height - t - 2, footprints, f, cols: false))
                return int.MinValue;

            return doorDist;
        }

        /// <summary>True when some 2-wide band c..c+1 in [lo, hi] stays clear of every blocked rect
        /// (each footprint + its 1-tile buffer) including the candidate — the router needs at least
        /// one such column and row for its Z connectors.</summary>
        private static bool HasFreeBand(int lo, int hi,
                                        List<(int x0, int y0, int x1, int y1)> footprints,
                                        (int x0, int y0, int x1, int y1) candidate, bool cols)
        {
            for (int c = lo; c <= hi; c++)
            {
                bool ok = BandClear(c, candidate, cols);
                if (ok)
                    foreach (var f in footprints)
                        if (!BandClear(c, f, cols)) { ok = false; break; }
                if (ok) return true;
            }
            return false;
        }

        private static bool BandClear(int c, (int x0, int y0, int x1, int y1) f, bool cols)
        {
            int lo = (cols ? f.x0 : f.y0) - 1, hi = (cols ? f.x1 : f.y1) + 1; // + the path buffer
            return c + 1 < lo || c > hi;
        }

        // ---- Shared placement/validation ------------------------------------------------------

        /// <summary>Validate the whole footprint at an explicit core origin, then mark it — all or
        /// nothing, so the gradient is always nested perfect rectangles (the room walls may clamp
        /// the outer rings, keeping the footprint a wall-nestled rectangle).</summary>
        private static bool TryPlaceAt(RoomGrid grid, int t, int cx0, int cy0,
                                       int w, int h, int halfThick, int fewThick, int noneThick)
        {
            int maxDist = halfThick + fewThick + noneThick;
            if (!ValidateAt(grid, t, cx0, cy0, w, h, maxDist)) return false;

            int cx1 = cx0 + w - 1, cy1 = cy0 + h - 1;
            var f = Footprint(grid, t, cx0, cy0, w, h, maxDist);
            for (int x = f.x0; x <= f.x1; x++)
                for (int y = f.y0; y <= f.y1; y++)
                {
                    int dx = x < cx0 ? cx0 - x : x > cx1 ? x - cx1 : 0;
                    int dy = y < cy0 ? cy0 - y : y > cy1 ? y - cy1 : 0;
                    int d = System.Math.Max(dx, dy);
                    grid.MarkGrass(x, y, d == 0 ? 4 : d <= halfThick ? 3 : d <= halfThick + fewThick ? 2 : 1);
                }
            return true;
        }

        /// <summary>True when every footprint cell is plain Floor ≥1 tile clear of every path, and
        /// no other patch sits within <see cref="MinPatchGap"/> of the footprint (patches never
        /// share a corner or touch).</summary>
        private static bool ValidateAt(RoomGrid grid, int t, int cx0, int cy0, int w, int h, int maxDist)
        {
            var f = Footprint(grid, t, cx0, cy0, w, h, maxDist);
            for (int x = f.x0; x <= f.x1; x++)
                for (int y = f.y0; y <= f.y1; y++)
                    if (grid.Get(x, y) != CellType.Floor || TouchesPath(grid, x, y))
                        return false;
            // Grass within MinPatchGap-1 of the footprint = another patch closer than the allowed
            // gap (a gap of exactly MinPatchGap is fine — it's what reservations produce).
            for (int x = f.x0 - (MinPatchGap - 1); x <= f.x1 + (MinPatchGap - 1); x++)
                for (int y = f.y0 - (MinPatchGap - 1); y <= f.y1 + (MinPatchGap - 1); y++)
                    if (grid.GrassLevelAt(x, y) > 0)
                        return false;
            return true;
        }

        /// <summary>The patch footprint: core grown by <paramref name="maxDist"/>, clamped to the
        /// interior.</summary>
        private static (int x0, int y0, int x1, int y1) Footprint(RoomGrid grid, int t,
                                                                  int cx0, int cy0, int w, int h, int maxDist) =>
            (System.Math.Max(t, cx0 - maxDist),
             System.Math.Max(t, cy0 - maxDist),
             System.Math.Min(grid.Width - t - 1, cx0 + w - 1 + maxDist),
             System.Math.Min(grid.Height - t - 1, cy0 + h - 1 + maxDist));

        /// <summary>True when the cell IS a path cell or touches one (8-adjacency) — the gradient
        /// always keeps 1 tile of plain floor between itself and the paths.</summary>
        private static bool TouchesPath(RoomGrid grid, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (grid.IsPath(x + dx, y + dy))
                        return true;
            return false;
        }

        /// <summary>Chebyshev distance from a cell to the nearest path cell.</summary>
        private static int PathDistance(RoomGrid grid, (int x, int y) cell)
        {
            int best = int.MaxValue;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.IsPath(x, y))
                        best = System.Math.Min(best, System.Math.Max(
                            System.Math.Abs(x - cell.x), System.Math.Abs(y - cell.y)));
            return best;
        }

        /// <summary>Chebyshev distance from a cell to the nearest door cell.</summary>
        private static int DoorDistance(List<(int x, int y)> doorCells, (int x, int y) cell)
        {
            int best = int.MaxValue;
            foreach (var d in doorCells)
                best = System.Math.Min(best, System.Math.Max(
                    System.Math.Abs(d.x - cell.x), System.Math.Abs(d.y - cell.y)));
            return best;
        }

        /// <summary>Chebyshev distance from a cell to an inclusive rect.</summary>
        private static int RectDistance((int x, int y) c, int x0, int y0, int x1, int y1)
        {
            int dx = c.x < x0 ? x0 - c.x : c.x > x1 ? c.x - x1 : 0;
            int dy = c.y < y0 ? y0 - c.y : c.y > y1 ? c.y - y1 : 0;
            return System.Math.Max(dx, dy);
        }

        /// <summary>Chebyshev gap between two inclusive rects (0 = touching/overlapping).</summary>
        private static int RectGap((int x0, int y0, int x1, int y1) a, (int x0, int y0, int x1, int y1) b)
        {
            int dx = a.x1 < b.x0 ? b.x0 - a.x1 : b.x1 < a.x0 ? a.x0 - b.x1 : 0;
            int dy = a.y1 < b.y0 ? b.y0 - a.y1 : b.y1 < a.y0 ? a.y0 - b.y1 : 0;
            return System.Math.Max(dx, dy);
        }
    }
}
