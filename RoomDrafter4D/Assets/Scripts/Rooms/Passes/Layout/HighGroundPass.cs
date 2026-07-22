using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Anubis (Open layout) "high ground": a second floor look carved OVER the base field. Cells stay
    /// ordinary walkable Floor — they're only flagged (<see cref="RoomGrid.MarkHighGround"/>) so the
    /// painter paves them with <see cref="BiomeConfig.HighGroundTileVariants"/> instead of the base
    /// floor. Shape: the top <see cref="BandDepth"/> interior rows are ALWAYS high ground (full
    /// width), then 1..<see cref="MaxCorridors"/> corridors of width <see cref="CorridorWidth"/> run
    /// south from the band — random independent positions and lengths, each stopping at least
    /// <see cref="SouthMargin"/> rows short of the south water. One corridor reads as an L/T, two as
    /// a (reversed) U, three as a comb — never symmetric by construction.
    ///
    /// Each corridor is MOATED: its outline (1 tile around it) becomes Water, and the foot deepens
    /// to <see cref="FootMoatDepth"/> water rows below the end — so corridors are raised ground
    /// reachable only from the band (or straight through a hugged door). The painter renders this
    /// interior water on ExtrasFullBehind + UnseenCollision.
    ///
    /// Door rules (the pass runs after DoorPass, so Door cells are in the grid):
    /// • A corridor is either RIGHT ON a door or ≥3 tiles clear of every door — a 1- or 2-tile gap
    ///   is never allowed (<see cref="MinDoorDistance"/> must be 1 or ≥4).
    /// • The planned moat must never flood a door's landing cells nor touch a door
    ///   (<see cref="MoatBlocksDoor"/>) — lengths violating it aren't rolled.
    /// • Each side (east/west) door rolls <see cref="DoorHugChance"/> for a corridor placed right
    ///   against it, spanning past the door's rows — so side doors frequently open straight onto
    ///   high ground.
    /// </summary>
    public class HighGroundPass : IRoomPass
    {
        private const int BandDepth = 3;     // interior rows of the always-filled north band
        private const int CorridorWidth = 3;
        private const int MaxCorridors = 3;  // south-running corridors: 1..this
        private const int SouthMargin = 3;   // rows kept between a corridor end and the south water
        private const int CorridorGap = 5;   // min columns between two corridors (each moat eats 1, leaving 3 walkable — never a 2-wide floor lane)
        private const int FootMoatDepth = 3; // water rows below a corridor's end (the 1-tile outline + 2 extra)
        private const int MinCorridorLength = 2; // rows below the band — a 1-tile stub never reads as a corridor
        private const float DoorHugChance = 0.6f; // per side door: chance of a corridor right against it

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Open) return;

            int t = config.WallThickness;
            int top = grid.Height - t - 1;        // topmost interior row
            int bandBottom = top - BandDepth + 1; // the band spans [bandBottom, top]

            // North band: always high ground, full interior width.
            for (int x = t; x < grid.Width - t; x++)
                for (int y = bandBottom; y <= top; y++)
                    Mark(grid, x, y);

            // An east/west door straddling the band's bottom edge would read half high ground,
            // half regular floor — never allowed: shift it down to sit fully on the field.
            FixStraddlingDoor(grid, Cardinal.West, t, bandBottom, config.DoorWidth);
            FixStraddlingDoor(grid, Cardinal.East, t, bandBottom, config.DoorWidth);

            int startY = bandBottom - 1;   // first row below the band
            int minEndY = t + SouthMargin; // lowest row a corridor may reach
            if (startY >= minEndY)         // room tall enough for corridors
            {
                var placed = PlaceCorridors(grid, rng, config, t, startY, minEndY);
                CarveCorridors(grid, placed, startY);
                FloodMoats(grid, placed, startY);
                ErodeThinFloor(grid, t);
            }

            // Stairs: the band's bottom edge is the ONLY seam where high and low ground touch
            // (corridors are fully moated), so every separated regular-floor area gets a 3×3 stair
            // site there — the field always touches the band somewhere, so at least one places.
            PlaceStairs(grid, rng, t, startY);
        }

        /// <summary>Roll the corridor set: 1..<see cref="MaxCorridors"/> total. Door-hugging
        /// corridors go first — each side door rolls <see cref="DoorHugChance"/> for one placed
        /// right against the room edge there, ending ≥2 rows below the door's first row (the door
        /// opens straight onto high ground, with 2 corridor tiles before its last line). Remaining
        /// slots fill from shuffled candidate columns, enforcing corridor spacing and the per-side
        /// edge-gap rule: gap 0 (the edge position) is fine, gap 1 forbidden (a non-edge corridor
        /// stays ≥2 from the high-ground edge), gap 3 forbidden (moat + 2-wide strip = no room for
        /// a stairs patch → trapped pocket), gap 2 and ≥4 fine. Lengths are rolled only among ends
        /// passing <see cref="CollectValidEnds"/>; a position with no valid end is skipped.</summary>
        private static List<(int left, int endY)> PlaceCorridors(RoomGrid grid, System.Random rng,
                                                                 BiomeConfig config, int t, int startY, int minEndY)
        {
            var doorCells = new List<(int x, int y)>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid[x, y] == CellType.Door) doorCells.Add((x, y));

            // The landing cells in front of every door — the moat must never flood these.
            var landingCells = new HashSet<(int x, int y)>();
            foreach (var (dx, dy) in doorCells)
            {
                int sx = dx < t ? 1 : dx >= grid.Width - t ? -1 : 0;  // inward step
                int sy = dy < t ? 1 : dy >= grid.Height - t ? -1 : 0;
                for (int k = 1; k <= config.LandingDepth; k++)
                    landingCells.Add((dx + sx * k, dy + sy * k));
            }

            int count = rng.Next(1, MaxCorridors + 1);
            int firstLeft = t, lastLeft = grid.Width - t - CorridorWidth;
            var placed = new List<(int left, int endY)>(count);
            var validEnds = new List<int>();

            var hugs = new List<(int left, int doorRow)>
            {
                (firstLeft, grid.DoorStarts[(int)Cardinal.West]),
                (lastLeft, grid.DoorStarts[(int)Cardinal.East]),
            };
            rng.Shuffle(hugs);
            foreach (var (left, doorRow) in hugs)
            {
                if (placed.Count == count) break;
                if (rng.NextDouble() >= DoorHugChance) continue;
                CollectValidEnds(grid, doorCells, landingCells, left,
                                 minEndY, System.Math.Min(doorRow - 2, startY), startY, validEnds);
                if (validEnds.Count == 0) continue;
                placed.Add((left, validEnds[rng.Next(validEnds.Count)]));
            }

            foreach (int i in rng.ShuffledIndices(lastLeft - firstLeft + 1))
            {
                if (placed.Count == count) break;
                int left = firstLeft + i;

                int westGap = left - firstLeft, eastGap = lastLeft - left;
                if (westGap == 1 || eastGap == 1 || westGap == 3 || eastGap == 3) continue;

                bool fits = true;
                foreach (var other in placed)
                    if (System.Math.Abs(left - other.left) < CorridorWidth + CorridorGap) { fits = false; break; }
                if (!fits) continue;

                CollectValidEnds(grid, doorCells, landingCells, left, minEndY, startY, startY, validEnds);
                if (validEnds.Count == 0) continue;
                placed.Add((left, validEnds[rng.Next(validEnds.Count)]));
            }

            return placed;
        }

        /// <summary>Mark every corridor cell high ground and record each end (centre column, end
        /// row) on <see cref="RoomGrid.HighGroundEnds"/>. ALL corridors are marked before any moat
        /// floods, so a later corridor is never eaten by an earlier one's moat.</summary>
        private static void CarveCorridors(RoomGrid grid, List<(int left, int endY)> placed, int startY)
        {
            foreach (var (left, endY) in placed)
            {
                for (int x = left; x < left + CorridorWidth; x++)
                    for (int y = endY; y <= startY; y++)
                        Mark(grid, x, y);
                grid.HighGroundEnds.Add((left + CorridorWidth / 2, endY)); // centre column, end row
            }
        }

        /// <summary>Moat every corridor: its 1-tile outline becomes water and the foot deepens to
        /// <see cref="FootMoatDepth"/> rows below the end. Only plain Floor converts — high ground
        /// (the band, other corridors) and the existing water ring are untouched.</summary>
        private static void FloodMoats(RoomGrid grid, List<(int left, int endY)> placed, int startY)
        {
            foreach (var (left, endY) in placed)
                for (int x = left - 1; x <= left + CorridorWidth; x++)
                    for (int y = endY - FootMoatDepth; y <= startY + 1; y++)
                        if (grid.Get(x, y) == CellType.Floor && !grid.IsHighGround(x, y))
                            grid[x, y] = CellType.Water;
        }

        /// <summary>No 1-wide floor: a plain Floor cell that isn't part of ANY fully-walkable 2×2
        /// block is a 1-tile line/pocket (e.g. the lane a moat leaves against the boundary water) —
        /// eroded to water, repeated until stable since an erosion can expose new thin cells. High
        /// ground is never eroded (band/corridors are 3 wide by construction), and door landings
        /// survive (2-wide × 2-deep = their own 2×2).</summary>
        private static void ErodeThinFloor(RoomGrid grid, int t)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int x = t; x < grid.Width - t; x++)
                    for (int y = t; y < grid.Height - t; y++)
                    {
                        if (grid[x, y] != CellType.Floor || grid.IsHighGround(x, y)) continue;
                        if (InAnyWalkable2x2(grid, x, y)) continue;
                        grid[x, y] = CellType.Water;
                        changed = true;
                    }
            }
        }

        /// <summary>One 3×3 stairs site per 4-connected component of regular floor, so any field
        /// area sealed off by moats stays reachable through the high ground. A site spans 3 columns
        /// × 3 rows with its TOP row ON the band's bottom row (high ground by construction — the
        /// stairs cut into the band's lip) and its two lower rows on regular floor, picked at
        /// random among the component's valid columns. A component that never reaches the band seam
        /// (a doorless pocket) has no site and is skipped. Recorded on
        /// <see cref="RoomGrid.StairPatches"/>.</summary>
        private static void PlaceStairs(RoomGrid grid, System.Random rng, int t, int startY)
        {
            bool Regular(int x, int y) => grid.Get(x, y) == CellType.Floor && !grid.IsHighGround(x, y);

            // Label the regular-floor components (4-connected flood fill).
            var comp = new int[grid.Width, grid.Height];
            int count = 0;
            var stack = new Stack<(int x, int y)>();
            for (int sx = t; sx < grid.Width - t; sx++)
                for (int sy = t; sy < grid.Height - t; sy++)
                {
                    if (!Regular(sx, sy) || comp[sx, sy] != 0) continue;
                    comp[sx, sy] = ++count;
                    stack.Push((sx, sy));
                    while (stack.Count > 0)
                    {
                        var (cx, cy) = stack.Pop();
                        Visit(cx - 1, cy); Visit(cx + 1, cy); Visit(cx, cy - 1); Visit(cx, cy + 1);
                    }
                    void Visit(int nx, int ny)
                    {
                        if (!grid.InBounds(nx, ny) || comp[nx, ny] != 0 || !Regular(nx, ny)) return;
                        comp[nx, ny] = count;
                        stack.Push((nx, ny));
                    }
                }

            // Candidate stair columns per component: the patch's two LOWER rows (c..c+2 ×
            // startY-1..startY) must be all regular floor — the top row lands on the band's bottom
            // row, which is high ground across the full width by construction.
            var sites = new Dictionary<int, List<int>>();
            for (int c = t; c + 2 < grid.Width - t; c++)
            {
                bool ok = true;
                for (int x = c; x <= c + 2 && ok; x++)
                    for (int y = startY - 1; y <= startY; y++)
                        if (!Regular(x, y)) { ok = false; break; }
                if (!ok) continue;
                int id = comp[c + 1, startY];
                if (!sites.TryGetValue(id, out var list)) sites[id] = list = new List<int>();
                list.Add(c);
            }

            for (int id = 1; id <= count; id++) // by id, so rng draws stay deterministic
                if (sites.TryGetValue(id, out var list))
                    grid.StairPatches.Add((list[rng.Next(list.Count)], startY - 1));
        }

        /// <summary>True when the cell belongs to at least one 2×2 block of walkable cells
        /// (Floor incl. high ground, or Door) — the "not a 1-tile line" test.</summary>
        private static bool InAnyWalkable2x2(RoomGrid grid, int x, int y)
        {
            for (int ox = -1; ox <= 0; ox++)
                for (int oy = -1; oy <= 0; oy++)
                    if (grid.Get(x + ox, y + oy).IsWalkable() &&
                        grid.Get(x + ox + 1, y + oy).IsWalkable() &&
                        grid.Get(x + ox, y + oy + 1).IsWalkable() &&
                        grid.Get(x + ox + 1, y + oy + 1).IsWalkable())
                        return true;
            return false;
        }

        /// <summary>Ends in [minEnd, maxEnd] valid for a corridor at <paramref name="left"/>: at
        /// least <see cref="MinCorridorLength"/> rows long, the door-distance rule holds — right ON
        /// a door (distance 1, the hug case) or ≥3 tiles clear (distance ≥4) — AND the planned moat
        /// neither floods a door landing nor touches a door.</summary>
        private static void CollectValidEnds(RoomGrid grid, List<(int x, int y)> doorCells,
                                             HashSet<(int x, int y)> landingCells, int left,
                                             int minEnd, int maxEnd, int startY, List<int> ends)
        {
            ends.Clear();
            maxEnd = System.Math.Min(maxEnd, startY - (MinCorridorLength - 1)); // never a 1-tile stub
            for (int endY = minEnd; endY <= maxEnd; endY++)
            {
                int doorDist = MinDoorDistance(doorCells, left, endY, startY);
                if (doorDist != 1 && doorDist < 4) continue;
                if (MoatBlocksDoor(grid, landingCells, left, endY, startY)) continue;
                ends.Add(endY);
            }
        }

        /// <summary>Minimum Chebyshev distance from any door cell to the corridor's footprint rect
        /// (1 = touching/adjacent; door cells sit in the border ring so 0 can't happen).</summary>
        private static int MinDoorDistance(List<(int x, int y)> doorCells, int left, int endY, int startY)
        {
            int min = int.MaxValue;
            int right = left + CorridorWidth - 1;
            foreach (var (dx, dy) in doorCells)
            {
                int ddx = dx < left ? left - dx : dx > right ? dx - right : 0;
                int ddy = dy < endY ? endY - dy : dy > startY ? dy - startY : 0;
                int d = System.Math.Max(ddx, ddy);
                if (d < min) min = d;
            }
            return min;
        }

        /// <summary>True when any cell the corridor's moat would flood (its 1-tile outline, with the
        /// foot deepened to <see cref="FootMoatDepth"/> rows) is a door-landing cell or 4-adjacent
        /// to a Door cell — flooding there would seal a doorway shut. Only cells that would actually
        /// convert count (plain Floor; the band, other corridors and the water ring are skipped).</summary>
        private static bool MoatBlocksDoor(RoomGrid grid, HashSet<(int x, int y)> landingCells,
                                           int left, int endY, int startY)
        {
            int right = left + CorridorWidth - 1;
            for (int x = left - 1; x <= right + 1; x++)
                for (int y = endY - FootMoatDepth; y <= startY + 1; y++)
                {
                    if (x >= left && x <= right && y >= endY && y <= startY) continue; // the corridor itself
                    if (grid.Get(x, y) != CellType.Floor || grid.IsHighGround(x, y)) continue; // won't flood
                    if (landingCells.Contains((x, y))) return true;
                    if (grid.Get(x - 1, y) == CellType.Door || grid.Get(x + 1, y) == CellType.Door ||
                        grid.Get(x, y - 1) == CellType.Door || grid.Get(x, y + 1) == CellType.Door)
                        return true;
                }
            return false;
        }

        /// <summary>Shift an east/west door DOWN when its cell band crosses the high-ground band's
        /// bottom edge (top cell(s) beside the band, bottom cell(s) beside the field) — the door
        /// would paint half high ground, half regular floor. Moved so its top row sits just below
        /// the band, i.e. fully regular floor. Runs before corridors/moats, so door rules and hug
        /// placements all see the corrected position; the vacated ring cells return to water and
        /// the new rows' landings are already field floor.</summary>
        private static void FixStraddlingDoor(RoomGrid grid, Cardinal side, int t, int bandBottom, int doorWidth)
        {
            int start = grid.DoorStarts[(int)side];
            if (start >= bandBottom || start + doorWidth - 1 < bandBottom) return; // no straddle

            int newStart = bandBottom - doorWidth;
            int x = side == Cardinal.West ? t - 1 : grid.Width - t;
            for (int y = start; y < start + doorWidth; y++) grid[x, y] = CellType.Water;
            for (int y = newStart; y < newStart + doorWidth; y++) grid[x, y] = CellType.Door;
            grid.DoorStarts[(int)side] = newStart;
        }

        /// <summary>Flag a cell — only actual Floor, so Door cells and the water ring stay untouched
        /// (the painter handles a door's own look via its inward neighbour).</summary>
        private static void Mark(RoomGrid grid, int x, int y)
        {
            if (grid[x, y] == CellType.Floor) grid.MarkHighGround(x, y);
        }
    }
}
