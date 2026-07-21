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
    /// <see cref="SouthMargin"/> base-floor rows short of the south water. One corridor reads as an
    /// L/T, two as a (reversed) U, three as a comb — never symmetric by construction.
    ///
    /// Door rules (the pass runs after DoorPass, so Door cells are in the grid):
    /// • A corridor is either RIGHT NEXT to a door or at least 2 tiles away — a 1-tile gap between
    ///   corridor and door is never allowed (<see cref="MinDoorDistance"/> ≠ 2).
    /// • The run-out strip (the <see cref="SouthMargin"/> base-floor rows below a corridor's end)
    ///   must never touch a door (<see cref="RunOutTouchesDoor"/>).
    /// • Each side (east/west) door rolls <see cref="DoorHugChance"/> for a corridor placed right
    ///   against it, spanning past the door's rows — so side doors frequently open straight onto
    ///   high ground.
    /// </summary>
    public class HighGroundPass : IRoomPass
    {
        private const int BandDepth = 3;     // interior rows of the always-filled north band
        private const int CorridorWidth = 3;
        private const int MaxCorridors = 3;  // south-running corridors: 1..this
        private const int SouthMargin = 3;   // base-floor rows kept between a corridor end and the south water
        private const int CorridorGap = 4;   // min columns between two corridors (each moat eats 1, leaving 2 walkable)
        private const float DoorHugChance = 0.6f; // per side door: chance of a corridor right against it

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            if (config.Layout != BiomeLayout.Open) return;

            int t = config.WallThickness;
            int top = grid.Height - t - 1;        // topmost interior row
            int bandBottom = top - BandDepth + 1; // the band spans [bandBottom, top]

            // 1) North band: always high ground, full interior width.
            for (int x = t; x < grid.Width - t; x++)
                for (int y = bandBottom; y <= top; y++)
                    Mark(grid, x, y);

            int startY = bandBottom - 1;   // first row below the band
            int minEndY = t + SouthMargin; // lowest row a corridor may reach
            if (startY < minEndY) return;  // room too short for any corridor

            var doorCells = new List<(int x, int y)>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid[x, y] == CellType.Door) doorCells.Add((x, y));

            int count = rng.Next(1, MaxCorridors + 1);
            int firstLeft = t, lastLeft = grid.Width - t - CorridorWidth;
            var placed = new List<int>(count);
            var validEnds = new List<int>();
            var corridorCells = new List<(int x, int y)>();

            // 2) Door-hugging corridors first: each side door rolls its chance for a corridor placed
            // RIGHT against the room edge there, ending at/below the door's first row so it runs
            // alongside the whole doorway — the door then opens straight onto high ground.
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
                CollectValidEnds(grid, doorCells, left, minEndY, System.Math.Min(doorRow, startY), startY, validEnds);
                if (validEnds.Count == 0) continue;
                Carve(grid, left, validEnds[rng.Next(validEnds.Count)], startY, corridorCells);
                placed.Add(left);
            }

            // 3) Remaining corridors from shuffled candidates, kept when far enough from the ones
            // already placed — position, count AND length are all independent rolls, so the shape
            // is asymmetric by default. Lengths are rolled only among ends passing the door rules;
            // a position with no valid end at all is skipped.
            foreach (int i in rng.ShuffledIndices(lastLeft - firstLeft + 1))
            {
                if (placed.Count == count) break;
                int left = firstLeft + i;
                bool fits = true;
                foreach (int other in placed)
                    if (System.Math.Abs(left - other) < CorridorWidth + CorridorGap) { fits = false; break; }
                if (!fits) continue;

                CollectValidEnds(grid, doorCells, left, minEndY, startY, startY, validEnds);
                if (validEnds.Count == 0) continue;
                Carve(grid, left, validEnds[rng.Next(validEnds.Count)], startY, corridorCells);
                placed.Add(left);
            }

            // 4) Moat: every base-floor tile touching a south corridor (8-neighbourhood, so the full
            // outline incl. corners) becomes WATER — the corridors read as raised ground reachable
            // only from the band (or straight through a hugged door). The band itself is never
            // moated (its cells are high ground, and only plain Floor converts), and the outline
            // can't reach doors/landings thanks to the door-distance + run-out rules.
            foreach (var (cx, cy) in corridorCells)
                for (int nx = cx - 1; nx <= cx + 1; nx++)
                    for (int ny = cy - 1; ny <= cy + 1; ny++)
                        if (grid.Get(nx, ny) == CellType.Floor && !grid.IsHighGround(nx, ny))
                            grid[nx, ny] = CellType.Water;
        }

        private static void Carve(RoomGrid grid, int left, int endY, int startY,
                                  List<(int x, int y)> cells)
        {
            for (int x = left; x < left + CorridorWidth; x++)
                for (int y = endY; y <= startY; y++)
                {
                    Mark(grid, x, y);
                    cells.Add((x, y));
                }
        }

        /// <summary>Ends in [minEnd, maxEnd] valid for a corridor at <paramref name="left"/>: the
        /// run-out strip stays clear of doors AND the door-distance rule holds — the corridor is
        /// either right ON a door (distance 1, the hug case) or at least 3 tiles clear of every
        /// door (distance ≥4); the in-between gaps are never allowed.</summary>
        private static void CollectValidEnds(RoomGrid grid, List<(int x, int y)> doorCells, int left,
                                             int minEnd, int maxEnd, int startY, List<int> ends)
        {
            ends.Clear();
            for (int endY = minEnd; endY <= maxEnd; endY++)
            {
                if (RunOutTouchesDoor(grid, left, endY)) continue;
                int doorDist = MinDoorDistance(doorCells, left, endY, startY);
                if (doorDist == 1 || doorDist >= 4) ends.Add(endY);
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

        /// <summary>True when any cell of the corridor's run-out strip — the <see cref="SouthMargin"/>
        /// base-floor rows directly below its end row, across its full width — is 4-adjacent to a
        /// Door cell. Those rows must stay clear of doors so a doorway never opens straight onto
        /// the foot of the high ground.</summary>
        private static bool RunOutTouchesDoor(RoomGrid grid, int left, int endY)
        {
            for (int x = left; x < left + CorridorWidth; x++)
                for (int y = endY - SouthMargin; y < endY; y++)
                    if (grid.Get(x - 1, y) == CellType.Door || grid.Get(x + 1, y) == CellType.Door ||
                        grid.Get(x, y - 1) == CellType.Door || grid.Get(x, y + 1) == CellType.Door)
                        return true;
            return false;
        }

        /// <summary>Flag a cell — only actual Floor, so Door cells and the water ring stay untouched
        /// (the painter handles a door's own look via its inward neighbour).</summary>
        private static void Mark(RoomGrid grid, int x, int y)
        {
            if (grid[x, y] == CellType.Floor) grid.MarkHighGround(x, y);
        }
    }
}
