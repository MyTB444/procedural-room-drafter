using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Halls layout: the whole room is water (so the perimeter reads as water, not walls), holding
    /// several walled "igrooms" (rooms-within-the-room) — one guaranteed main
    /// (<see cref="BiomeConfig.HallMainRoomSize"/> floor, square) plus up to
    /// <see cref="BiomeConfig.HallExtraRooms"/> extras of random size, packed
    /// <see cref="BiomeConfig.HallMoatThickness"/> cells apart. Each igroom is a Floor rectangle with
    /// a FULL 1-cell Wall border (it stays completely closed except a single 2-wide doorway breached
    /// on a random side — floor side min 4 leaves room for it + flanking wall). Every border cell's
    /// exact <see cref="WallKind"/> is recorded on the grid so the painter stamps the right
    /// directional/corner tile — no neighbour-guessing. The connecting path is routed THROUGH THE
    /// WATER around the igrooms (<see cref="CorridorCarver.ConnectThroughWater"/>), so it never
    /// breaches a wall except at the doorways. Finally the floor is GROWN into the surrounding water
    /// (<see cref="BiomeConfig.HallFloorGrowth"/>) to fatten the corridors and drain the room — igrooms
    /// are walled so only the paths spread, leaving big halls of floor and little water.
    /// </summary>
    public class HallPass : IRoomPass
    {
        private const int PlacementAttempts = 40;

        /// <summary>Smallest igroom floor side: a 2-wide doorway + a flanking wall cell each side.</summary>
        private const int MinFloorSide = 4;

        private static readonly Cardinal[] FourDirections =
            { Cardinal.North, Cardinal.East, Cardinal.South, Cardinal.West };

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;
            int gap = System.Math.Max(1, config.HallMoatThickness);

            // Everything starts as water — including the perimeter (a solid water boundary).
            grid.Fill(CellType.Water);

            var occupied = new bool[grid.Width, grid.Height];
            var nodes = new List<(int x, int y)>();

            // Keep the door approaches clear so no igroom lands on a door and blocks it.
            ReserveDoorApproaches(grid, occupied, config, t, gap);

            // The guaranteed main igroom, then the random extras (placed wherever they still fit).
            int main = System.Math.Max(2, config.HallMainRoomSize);
            TryPlaceRoom(grid, occupied, nodes, main, main, t, gap, rng);

            // Min floor side 4: a 2-wide doorway plus a wall cell flanking each side needs it.
            int min = System.Math.Max(MinFloorSide, config.HallRoomSizeRange.x);
            int max = System.Math.Max(min, config.HallRoomSizeRange.y);
            for (int i = 0; i < config.HallExtraRooms; i++)
                TryPlaceRoom(grid, occupied, nodes, rng.Next(min, max + 1), rng.Next(min, max + 1), t, gap, rng);

            // The room's own doors join the network so the player can walk door → igroom → door.
            foreach (var d in FourDirections)
            {
                var landing = RoomDoors.Landing(config, grid, d);
                nodes.Add((landing.x, landing.y));
            }

            // The path that links every doorway + door landing, threading the water around igrooms.
            CorridorCarver.ConnectThroughWater(grid, nodes, t, rng);

            // Fatten the paths and drain the room: grow the floor into the surrounding water (igrooms
            // are walled, so only the corridors/landings spread — leaving big halls, little water).
            CorridorCarver.GrowFloorIntoWater(grid, t, config.HallFloorGrowth);
        }

        /// <summary>
        /// Try to drop an igroom of the given FLOOR size somewhere it fits (footprint = floor + a
        /// 1-cell wall border, kept <paramref name="gap"/> cells from other igrooms and the edge).
        /// On success carves it, opens a doorway, and adds the doorway's outside cell to
        /// <paramref name="nodes"/>.
        /// </summary>
        private static void TryPlaceRoom(RoomGrid grid, bool[,] occupied, List<(int x, int y)> nodes,
                                         int floorW, int floorH, int t, int gap, System.Random rng)
        {
            int fw = floorW + 2, fh = floorH + 2; // footprint (walls included)
            if (fw > grid.Width - 2 * t || fh > grid.Height - 2 * t) return;

            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                int x0 = rng.Next(t, grid.Width - t - fw + 1);
                int y0 = rng.Next(t, grid.Height - t - fh + 1);
                if (!AreaClear(occupied, x0 - gap, y0 - gap, fw + 2 * gap, fh + 2 * gap)) continue;

                Mark(occupied, x0, y0, fw, fh);
                CarveRoom(grid, x0, y0, fw, fh);
                nodes.Add(OpenDoorway(grid, x0, y0, fw, fh, rng));
                return;
            }
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

        /// <summary>Breach a 2-wide opening in one random wall side (a wall cell still flanks each
        /// side of it) and return the cell just OUTSIDE it — the network node the water path connects
        /// to. The floor min side of 4 guarantees room for the 2 cells + flanks.</summary>
        private static (int x, int y) OpenDoorway(RoomGrid grid, int x0, int y0, int fw, int fh, System.Random rng)
        {
            int x1 = x0 + fw - 1, y1 = y0 + fh - 1;
            switch (rng.Next(4))
            {
                case 0: // North
                {
                    int x = rng.Next(x0 + 1, x1 - 1);
                    grid[x, y1] = CellType.Floor; grid[x + 1, y1] = CellType.Floor;
                    return (x, y1 + 1);
                }
                case 1: // South
                {
                    int x = rng.Next(x0 + 1, x1 - 1);
                    grid[x, y0] = CellType.Floor; grid[x + 1, y0] = CellType.Floor;
                    return (x, y0 - 1);
                }
                case 2: // East
                {
                    int y = rng.Next(y0 + 1, y1 - 1);
                    grid[x1, y] = CellType.Floor; grid[x1, y + 1] = CellType.Floor;
                    return (x1 + 1, y);
                }
                default: // West
                {
                    int y = rng.Next(y0 + 1, y1 - 1);
                    grid[x0, y] = CellType.Floor; grid[x0, y + 1] = CellType.Floor;
                    return (x0 - 1, y);
                }
            }
        }

        /// <summary>Block out the band just inside each door so igrooms can't sit on a door's landing.</summary>
        private static void ReserveDoorApproaches(RoomGrid grid, bool[,] occupied, BiomeConfig config, int t, int gap)
        {
            int reserve = config.LandingDepth + gap;
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
