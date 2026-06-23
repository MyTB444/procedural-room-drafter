using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Halls layout: the whole room is water (so the perimeter reads as water, not walls), holding
    /// several walled "igrooms" (rooms-within-the-room) — one big MAIN igroom plus a handful of
    /// SMALLER extras. Each igroom is a Floor rectangle with a FULL 1-cell Wall border (closed except
    /// a single 2-wide doorway on a water-facing side); every border cell's exact <see cref="WallKind"/>
    /// is recorded on the grid so the painter stamps the right tile (no neighbour-guessing).
    ///
    /// Placement is a DETERMINISTIC first-fit pack (not random — random fragments the small interior
    /// so only 1–2 rooms fit). To always reach <see cref="DesiredMinRooms"/> the main starts at its
    /// configured size and is SHRUNK only as far as needed: a floor-6 main (8×8 footprint) is so big
    /// it leaves room for one extra, so when the door layout is tight the main steps down (worst case
    /// to the same size as the extras) until at least 3 igrooms fit. Footprints touch the wall margin
    /// (no inset) and door reserves are kept minimal — both reclaim the width the third room needs;
    /// connectivity is preserved instead by opening each doorway onto interior open water.
    ///
    /// A single 2-wide corridor network (<see cref="CorridorCarver.ConnectThroughWater"/>) threads the
    /// water AROUND the igrooms, joining every igroom doorway AND every room door landing — guaranteed
    /// connected, so no igroom is stranded (which would let <see cref="ConnectivityPass"/> flood its
    /// floor away).
    /// </summary>
    public class HallPass : IRoomPass
    {
        /// <summary>Smallest igroom floor side: a 2-wide doorway + a flanking wall cell each side.</summary>
        private const int MinFloorSide = 4;

        /// <summary>Extra igroom floor WIDTH (footprint 6) — kept small so three rooms fit the width.</summary>
        private const int ExtraFloorWidth = MinFloorSide;

        private const int DesiredMinRooms = 3;
        private const int MaxRooms = 5;

        private static readonly Cardinal[] FourDirections =
            { Cardinal.North, Cardinal.East, Cardinal.South, Cardinal.West };

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;
            int gap = System.Math.Max(1, config.HallMoatThickness);
            int corridorWidth = System.Math.Max(2, config.CorridorWidth); // the 2-tile halls

            // Everything starts as water — including the perimeter (a solid water boundary).
            grid.Fill(CellType.Water);

            // Plan room footprints (on a scratch occupancy map) before carving anything.
            var rooms = PlanRooms(grid, config, rng, t, gap);

            // Carve every igroom: floor interior + recorded wall border.
            foreach (var r in rooms)
                CarveRoom(grid, r.x, r.y, r.fw, r.fh);

            // Open each igroom's doorway onto open water and collect the network nodes.
            var nodes = new List<(int x, int y)>();
            foreach (var r in rooms)
                nodes.Add(OpenDoorway(grid, r.x, r.y, r.fw, r.fh, t, rng));

            // The room's own doors join the network so the player can walk door → igroom → door.
            foreach (var d in FourDirections)
            {
                var landing = RoomDoors.Landing(config, grid, d);
                nodes.Add((landing.x, landing.y));
            }

            // One 2-wide corridor network linking every doorway + door landing, threading the water
            // around the igrooms. Guaranteed-connected, so ConnectivityPass finds nothing to prune.
            CorridorCarver.ConnectThroughWater(grid, nodes, t, rng, corridorWidth);
        }

        /// <summary>
        /// Pack a big main igroom + smaller extras with first-fit, shrinking the main until at least
        /// <see cref="DesiredMinRooms"/> fit (it stays as big as the door layout allows). Returns the
        /// chosen footprints; the grid is untouched (planning runs on a scratch occupancy map).
        /// </summary>
        private static List<(int x, int y, int fw, int fh)> PlanRooms(
            RoomGrid grid, BiomeConfig config, System.Random rng, int t, int gap)
        {
            int maxMain = System.Math.Max(MinFloorSide, config.HallMainRoomSize);
            var best = new List<(int x, int y, int fw, int fh)>();

            for (int mainFloor = maxMain; mainFloor >= MinFloorSide; mainFloor--)
            {
                var occupied = new bool[grid.Width, grid.Height];
                ReserveDoorApproaches(grid, occupied, config, t);
                var rooms = new List<(int x, int y, int fw, int fh)>();

                // The big main first (square), so the pack reserves its space up front.
                if (TryFindSpot(occupied, grid, mainFloor + 2, mainFloor + 2, t, gap, out var ms))
                    rooms.Add((ms.x, ms.y, mainFloor + 2, mainFloor + 2));

                // Then the smaller extras: fixed-width footprint so several fit across, with a little
                // height variety (floor 4 or 5).
                while (rooms.Count < MaxRooms)
                {
                    int fw = ExtraFloorWidth + 2;
                    int fh = ExtraFloorWidth + 2 + rng.Next(0, 2); // footprint 6 or 7 tall
                    if (!TryFindSpot(occupied, grid, fw, fh, t, gap, out var es))
                    {
                        // The taller pick may not fit where the shorter one would — retry at min height.
                        if (fh == ExtraFloorWidth + 2 ||
                            !TryFindSpot(occupied, grid, fw, ExtraFloorWidth + 2, t, gap, out es))
                            break;
                        fh = ExtraFloorWidth + 2;
                    }
                    rooms.Add((es.x, es.y, fw, fh));
                }

                if (rooms.Count >= DesiredMinRooms) return rooms;
                if (rooms.Count > best.Count) best = rooms;
            }
            return best; // tightest possible — fewer than the target only if the room genuinely can't hold them
        }

        /// <summary>First-fit search for a clear footprint (+ <paramref name="gap"/> moat), packing
        /// toward the bottom-left. Marks the footprint on success. Footprints may touch the wall margin
        /// (no inset) — the doorway picks a water-facing side, so a margin-hugging room still connects.</summary>
        private static bool TryFindSpot(bool[,] occupied, RoomGrid grid, int fw, int fh, int t, int gap,
                                        out (int x, int y) spot)
        {
            spot = default;
            int hiX = grid.Width - t - fw, hiY = grid.Height - t - fh;
            if (hiX < t || hiY < t) return false;

            for (int x0 = t; x0 <= hiX; x0++)
                for (int y0 = t; y0 <= hiY; y0++)
                    if (AreaClear(occupied, x0 - gap, y0 - gap, fw + 2 * gap, fh + 2 * gap))
                    {
                        Mark(occupied, x0, y0, fw, fh);
                        spot = (x0, y0);
                        return true;
                    }
            return false;
        }

        /// <summary>Carve the floor interior + full wall border, recording each border cell's exact
        /// wall kind (sides + the four corners) so the painter stamps the matching tile.</summary>
        private static void CarveRoom(RoomGrid grid, int x0, int y0, int fw, int fh)
        {
            int x1 = x0 + fw - 1, y1 = y0 + fh - 1; // far edges (y1 = top, y0 = bottom)
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    bool left = x == x0, right = x == x1, bottom = y == y0, top = y == y1;
                    if (!(left || right || bottom || top)) { grid[x, y] = CellType.Floor; continue; }

                    grid[x, y] = CellType.Wall;
                    grid.SetWallKind(x, y, CornerOrEdge(left, right, bottom, top));
                }
        }

        private static WallKind CornerOrEdge(bool left, bool right, bool bottom, bool top)
        {
            if (top && left) return WallKind.NorthWest;
            if (top && right) return WallKind.NorthEast;
            if (bottom && left) return WallKind.SouthWest;
            if (bottom && right) return WallKind.SouthEast;
            if (top) return WallKind.North;
            if (bottom) return WallKind.South;
            if (left) return WallKind.West;
            return WallKind.East;
        }

        /// <summary>
        /// Breach a 2-wide opening in one WATER-FACING wall side (a wall cell still flanks each side),
        /// trying the four sides in random order so the result varies. Returns the cell just OUTSIDE
        /// the opening — the lower-left of the 2-wide approach, so a width-2 corridor lines up flush.
        /// Picking a side whose approach is interior open water is what keeps every igroom connectable
        /// even when it hugs the wall margin. Falls back to the first side if none face open water.
        /// </summary>
        private static (int x, int y) OpenDoorway(RoomGrid grid, int x0, int y0, int fw, int fh,
                                                  int margin, System.Random rng)
        {
            int x1 = x0 + fw - 1, y1 = y0 + fh - 1;

            // Roll a door position per side, then prefer a side that opens onto interior water.
            int nx = rng.Next(x0 + 1, x1 - 1);          // North/South opening start (columns nx, nx+1)
            int ey = rng.Next(y0 + 1, y1 - 1);          // East/West opening start (rows ey, ey+1)
            var sides = new[]
            {
                (a: (nx, y1 + 1), w1: (nx, y1), w2: (nx + 1, y1)),     // North
                (a: (nx, y0 - 1), w1: (nx, y0), w2: (nx + 1, y0)),     // South
                (a: (x1 + 1, ey), w1: (x1, ey), w2: (x1, ey + 1)),     // East
                (a: (x0 - 1, ey), w1: (x0, ey), w2: (x0, ey + 1)),     // West
            };

            int[] order = rng.ShuffledIndices(sides.Length);
            int chosen = order[0];
            foreach (int i in order)
            {
                var (ax, ay) = sides[i].a;
                if (grid.IsInterior(ax, ay, margin) && grid[ax, ay] == CellType.Water) { chosen = i; break; }
            }

            var s = sides[chosen];
            grid[s.w1.Item1, s.w1.Item2] = CellType.Floor;
            grid[s.w2.Item1, s.w2.Item2] = CellType.Floor;
            return s.a;
        }

        /// <summary>Keep each door's landing clear so an igroom can't sit on a door and block it.
        /// Minimal depth (just the landing) so the pack keeps as much width as possible.</summary>
        private static void ReserveDoorApproaches(RoomGrid grid, bool[,] occupied, BiomeConfig config, int t)
        {
            int reserve = config.LandingDepth;
            int dw = config.DoorWidth;
            int n = grid.DoorStarts[(int)Cardinal.North];
            int s = grid.DoorStarts[(int)Cardinal.South];
            int e = grid.DoorStarts[(int)Cardinal.East];
            int w = grid.DoorStarts[(int)Cardinal.West];

            Mark(occupied, n, grid.Height - t - reserve, dw, reserve);  // top
            Mark(occupied, s, t, dw, reserve);                          // bottom
            Mark(occupied, grid.Width - t - reserve, e, reserve, dw);   // right
            Mark(occupied, t, w, reserve, dw);                          // left
        }

        private static bool AreaClear(bool[,] occupied, int x, int y, int w, int h)
        {
            int x1 = System.Math.Min(occupied.GetLength(0), x + w);
            int y1 = System.Math.Min(occupied.GetLength(1), y + h);
            for (int cx = System.Math.Max(0, x); cx < x1; cx++)
                for (int cy = System.Math.Max(0, y); cy < y1; cy++)
                    if (occupied[cx, cy]) return false;
            return true;
        }

        private static void Mark(bool[,] occupied, int x0, int y0, int w, int h)
        {
            int x1 = System.Math.Min(occupied.GetLength(0), x0 + w);
            int y1 = System.Math.Min(occupied.GetLength(1), y0 + h);
            for (int x = System.Math.Max(0, x0); x < x1; x++)
                for (int y = System.Math.Max(0, y0); y < y1; y++)
                    occupied[x, y] = true;
        }
    }
}
