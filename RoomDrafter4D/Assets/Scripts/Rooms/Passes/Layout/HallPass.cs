using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Halls layout: a solid FLOOR field packed with MANY walled "igrooms" (rooms-within-the-room) —
    /// one big MAIN igroom plus as many SMALLER ones as fit. The whole interior is floor; the igrooms
    /// are Floor rectangles with a full 1-cell Wall border (closed except a single doorway), and the
    /// floor BETWEEN them is the walkable space — there are no corridors and no water, just rooms with
    /// floor halls weaving between them. igrooms may sit right against the room edge: their outer wall
    /// becomes the room's edge tile (the perimeter is sealed to wall around them).
    ///
    /// Connectivity is automatic: the between-floor is one connected region (igrooms are spaced
    /// <see cref="BiomeConfig.HallMoatThickness"/> ≥ 1 cells apart, so a floor lane always wraps each
    /// one), every igroom doorway opens onto it, and the 4 room doors land in it — so nothing is
    /// stranded and <see cref="ConnectivityPass"/> finds nothing to prune.
    /// </summary>
    public class HallPass : IRoomPass
    {
        /// <summary>Smallest igroom floor side: a 2-wide doorway + a flanking wall cell each side.</summary>
        private const int MinFloorSide = 3;

        /// <summary>Cap on how many igrooms to pack (first-fit stops earlier when space runs out).</summary>
        private const int MaxRooms = 12;

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;
            int gap = System.Math.Max(1, config.HallMoatThickness); // floor lane between igrooms

            // The whole room is floor; the boundary is re-walled at the end (SealPerimeter).
            grid.Fill(CellType.Floor);

            // Plan as many igroom footprints as fit (main + smaller), on a scratch occupancy map.
            var rooms = PlanRooms(grid, config, rng, t, gap);

            // Carve each igroom: room-floor interior + recorded wall border, then a doorway onto the
            // surrounding floor field.
            foreach (var r in rooms)
                CarveRoom(grid, r.x, r.y, r.fw, r.fh);
            foreach (var r in rooms)
                OpenDoorway(grid, r.x, r.y, r.fw, r.fh, t, rng);

            // Record interiors of the SMALL igrooms (everything but the big main room) for decor.
            RecordDecorAreas(grid, rooms);

            // Close the room boundary with WATER (as Halls always did) — any perimeter cell still open
            // floor becomes water; igroom walls that reached the edge stay as the edge tile there.
            // DoorPass then punches the 4 doors back through.
            WaterBoundary(grid, t);
        }

        /// <summary>
        /// First-fit pack of a big main igroom + as many smaller extras as fit. Footprints may touch
        /// the room edge (the doorway picks an inward side, the edge wall is sealed). Returns the
        /// chosen footprints; the grid is untouched (planning runs on a scratch occupancy map).
        /// </summary>
        private static List<(int x, int y, int fw, int fh)> PlanRooms(
            RoomGrid grid, BiomeConfig config, System.Random rng, int t, int gap)
        {
            var occupied = new bool[grid.Width, grid.Height];
            ReserveDoorApproaches(grid, occupied, config, t);
            var rooms = new List<(int x, int y, int fw, int fh)>();

            // The big main first (square), then everything else smaller than it.
            int mainFloor = System.Math.Max(MinFloorSide + 1, config.HallMainRoomSize);
            if (TryFindSpot(occupied, grid, mainFloor + 2, mainFloor + 2, gap, out var ms))
                rooms.Add((ms.x, ms.y, mainFloor + 2, mainFloor + 2));

            int extraMax = System.Math.Max(MinFloorSide, mainFloor - 1);
            while (rooms.Count < MaxRooms)
            {
                int fw = rng.Next(MinFloorSide, extraMax + 1) + 2;
                int fh = rng.Next(MinFloorSide, extraMax + 1) + 2;
                if (!TryFindSpot(occupied, grid, fw, fh, gap, out var es))
                {
                    // This random size didn't fit anywhere — try the smallest before giving up, so the
                    // pack keeps filling the leftover gaps with little rooms.
                    if ((fw == MinFloorSide + 2 && fh == MinFloorSide + 2) ||
                        !TryFindSpot(occupied, grid, MinFloorSide + 2, MinFloorSide + 2, gap, out es))
                        break;
                    rooms.Add((es.x, es.y, MinFloorSide + 2, MinFloorSide + 2));
                    continue;
                }
                rooms.Add((es.x, es.y, fw, fh));
            }
            return rooms;
        }

        /// <summary>First-fit search for a clear footprint (+ <paramref name="gap"/> floor lane around
        /// it), packing toward the bottom-left. Footprints may touch the room edge. Marks on success.</summary>
        private static bool TryFindSpot(bool[,] occupied, RoomGrid grid, int fw, int fh, int gap,
                                        out (int x, int y) spot)
        {
            spot = default;
            int hiX = grid.Width - fw, hiY = grid.Height - fh;
            if (hiX < 0 || hiY < 0) return false;

            for (int x0 = 0; x0 <= hiX; x0++)
                for (int y0 = 0; y0 <= hiY; y0++)
                    if (AreaClear(occupied, x0 - gap, y0 - gap, fw + 2 * gap, fh + 2 * gap))
                    {
                        Mark(occupied, x0, y0, fw, fh);
                        spot = (x0, y0);
                        return true;
                    }
            return false;
        }

        /// <summary>Carve the room-floor interior + full wall border, recording each border cell's exact
        /// wall kind (sides + the four corners) so the painter stamps the matching tile.</summary>
        private static void CarveRoom(RoomGrid grid, int x0, int y0, int fw, int fh)
        {
            int x1 = x0 + fw - 1, y1 = y0 + fh - 1; // far edges (y1 = top, y0 = bottom)
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    bool left = x == x0, right = x == x1, bottom = y == y0, top = y == y1;
                    if (!(left || right || bottom || top))
                    {
                        // Interior floor: paved with the distinct room-floor tiles by the painter.
                        grid[x, y] = CellType.Floor;
                        grid.MarkRoomFloor(x, y);
                        continue;
                    }

                    grid[x, y] = CellType.Wall;
                    grid.SetWallKind(x, y, CornerOrEdge(left, right, bottom, top));
                }
        }

        /// <summary>Record the INTERIOR rect of each igroom EXCEPT the largest (the main room, which gets
        /// no decor for now) onto <see cref="RoomGrid.IgroomDecorAreas"/> — interior = footprint minus the
        /// 1-cell wall border.</summary>
        private static void RecordDecorAreas(RoomGrid grid, List<(int x, int y, int fw, int fh)> rooms)
        {
            if (rooms.Count == 0) return;

            int mainIdx = 0; // the big main room = the largest footprint
            for (int i = 1; i < rooms.Count; i++)
                if (rooms[i].fw * rooms[i].fh > rooms[mainIdx].fw * rooms[mainIdx].fh) mainIdx = i;

            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                int iw = r.fw - 2, ih = r.fh - 2; // interior (drop the wall border)
                if (iw <= 0 || ih <= 0) continue;
                var interior = new RectInt(r.x + 1, r.y + 1, iw, ih);
                if (i == mainIdx) grid.MainIgroomArea = interior; // the big main room → main-room content
                else grid.IgroomDecorAreas.Add(interior);        // small igrooms → scattered decor
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
        /// Breach a 2-wide doorway in one INWARD-facing wall side (a wall cell still flanks each side),
        /// trying the four sides in random order so the result varies. The chosen side is one whose
        /// BOTH outside cells are interior floor — so the full 2-wide opening faces the floor field,
        /// never off the room edge. Falls back to the first side if (somehow) none qualify.
        /// </summary>
        private static void OpenDoorway(RoomGrid grid, int x0, int y0, int fw, int fh,
                                        int margin, System.Random rng)
        {
            int x1 = x0 + fw - 1, y1 = y0 + fh - 1;

            int nx = rng.Next(x0 + 1, x1 - 1); // North/South opening start (columns nx, nx+1), flanked
            int ey = rng.Next(y0 + 1, y1 - 1); // East/West opening start (rows ey, ey+1), flanked
            var sides = new[]
            {
                (facing: Cardinal.North, a1: (nx, y1 + 1), a2: (nx + 1, y1 + 1), d1: (nx, y1), d2: (nx + 1, y1)),
                (facing: Cardinal.South, a1: (nx, y0 - 1), a2: (nx + 1, y0 - 1), d1: (nx, y0), d2: (nx + 1, y0)),
                (facing: Cardinal.East,  a1: (x1 + 1, ey), a2: (x1 + 1, ey + 1), d1: (x1, ey), d2: (x1, ey + 1)),
                (facing: Cardinal.West,  a1: (x0 - 1, ey), a2: (x0 - 1, ey + 1), d1: (x0, ey), d2: (x0, ey + 1)),
            };

            int[] order = rng.ShuffledIndices(sides.Length);
            int chosen = order[0];
            foreach (int i in order)
            {
                if (IsInteriorFloor(grid, sides[i].a1, margin) && IsInteriorFloor(grid, sides[i].a2, margin))
                {
                    chosen = i;
                    break;
                }
            }

            var s = sides[chosen];
            grid[s.d1.Item1, s.d1.Item2] = CellType.Floor;
            grid[s.d2.Item1, s.d2.Item2] = CellType.Floor;

            // Stairs ON the 2 doorway cells themselves (in the wall line, between the flanking walls) —
            // but only when the front beyond is a clean 2-wide floor (skip a 1-tile pinch, the rare
            // fallback where no side had both approach cells open). The staircase rotates as a rigid
            // pair, so the left/right halves swap cells with the facing: d1 is the WEST cell for N/S
            // doors and the SOUTH cell for E/W doors, holding the left half for South/East entrances.
            if (IsInteriorFloor(grid, s.a1, margin) && IsInteriorFloor(grid, s.a2, margin))
            {
                bool d1IsLeft = s.facing is Cardinal.South or Cardinal.East;
                grid.Entrances.Add((s.d1.Item1, s.d1.Item2, s.facing, d1IsLeft));
                grid.Entrances.Add((s.d2.Item1, s.d2.Item2, s.facing, !d1IsLeft));
            }
        }

        private static bool IsInteriorFloor(RoomGrid grid, (int x, int y) c, int margin) =>
            grid.IsInterior(c.x, c.y, margin) && grid[c.x, c.y] == CellType.Floor;

        /// <summary>Close the room boundary with water: every perimeter-ring cell still open Floor
        /// becomes Water (the unwalkable border Halls has always used). igroom walls that reached the
        /// edge stay as Wall, so an edge igroom's wall is the room's edge tile there.</summary>
        private static void WaterBoundary(RoomGrid grid, int t)
        {
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (!grid.IsInterior(x, y, t) && grid[x, y] == CellType.Floor)
                        grid[x, y] = CellType.Water;
        }

        /// <summary>Keep each door's landing clear so an igroom can't sit on a door and block it.</summary>
        private static void ReserveDoorApproaches(RoomGrid grid, bool[,] occupied, BiomeConfig config, int t)
        {
            int reserve = config.WallThickness + config.LandingDepth;
            int dw = config.DoorWidth;
            int n = grid.DoorStarts[(int)Cardinal.North];
            int s = grid.DoorStarts[(int)Cardinal.South];
            int e = grid.DoorStarts[(int)Cardinal.East];
            int w = grid.DoorStarts[(int)Cardinal.West];

            Mark(occupied, n, grid.Height - reserve, dw, reserve);  // top
            Mark(occupied, s, 0, dw, reserve);                      // bottom
            Mark(occupied, grid.Width - reserve, e, reserve, dw);   // right
            Mark(occupied, 0, w, reserve, dw);                      // left
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
