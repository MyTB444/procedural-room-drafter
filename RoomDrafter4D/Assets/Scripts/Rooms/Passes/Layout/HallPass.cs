using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Halls layout: the whole room is water (so the perimeter reads as water, not walls), holding
    /// several walled "igrooms" (rooms-within-the-room) — one guaranteed main room
    /// (<see cref="BiomeConfig.HallMainRoomSize"/> floor, square) plus <see cref="BiomeConfig.HallExtraRooms"/>
    /// extras of random size, kept <see cref="BiomeConfig.HallMoatThickness"/> cells apart. Each
    /// igroom is a Floor rectangle with a 1-cell Wall border and a 2-wide doorway on a random side.
    /// A path then connects every igroom doorway (and the room's own door landings) through the
    /// water via <see cref="CorridorCarver"/>. Walls render with the biome's directional wall tiles.
    /// </summary>
    public class HallPass : IRoomPass
    {
        private const int CorridorWidth = 2;
        private const int PlacementAttempts = 30;

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

            // The guaranteed main igroom, then the random extras.
            int main = System.Math.Max(2, config.HallMainRoomSize);
            TryPlaceRoom(grid, occupied, nodes, main, main, t, gap, rng);

            int min = System.Math.Max(2, config.HallRoomSizeRange.x);
            int max = System.Math.Max(min, config.HallRoomSizeRange.y);
            for (int i = 0; i < config.HallExtraRooms; i++)
                TryPlaceRoom(grid, occupied, nodes, rng.Next(min, max + 1), rng.Next(min, max + 1), t, gap, rng);

            // The room's own doors join the network so the player can walk door → igroom → door.
            foreach (var d in FourDirections)
            {
                var landing = RoomDoors.Landing(config, grid, d);
                nodes.Add((landing.x, landing.y));
            }

            // The path connecting every doorway and door landing across the water.
            CorridorCarver.ConnectNearest(grid, nodes, t, rng, _ => CorridorWidth);
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

        private static void CarveRoom(RoomGrid grid, int x0, int y0, int fw, int fh)
        {
            for (int x = x0; x < x0 + fw; x++)
                for (int y = y0; y < y0 + fh; y++)
                {
                    bool border = x == x0 || x == x0 + fw - 1 || y == y0 || y == y0 + fh - 1;
                    grid[x, y] = border ? CellType.Wall : CellType.Floor;
                }
        }

        /// <summary>Breach a 2-wide opening in one random wall side; return the cell just OUTSIDE it
        /// (the network node — the corridor connects there, the opening links it to the floor).</summary>
        private static (int x, int y) OpenDoorway(RoomGrid grid, int x0, int y0, int fw, int fh, System.Random rng)
        {
            switch (rng.Next(4))
            {
                case 0: // North
                {
                    int x = rng.Next(x0 + 1, x0 + fw - 2), wy = y0 + fh - 1;
                    grid[x, wy] = CellType.Floor; grid[x + 1, wy] = CellType.Floor;
                    return (x, wy + 1);
                }
                case 1: // South
                {
                    int x = rng.Next(x0 + 1, x0 + fw - 2);
                    grid[x, y0] = CellType.Floor; grid[x + 1, y0] = CellType.Floor;
                    return (x, y0 - 1);
                }
                case 2: // East
                {
                    int y = rng.Next(y0 + 1, y0 + fh - 2), wx = x0 + fw - 1;
                    grid[wx, y] = CellType.Floor; grid[wx, y + 1] = CellType.Floor;
                    return (wx + 1, y);
                }
                default: // West
                {
                    int y = rng.Next(y0 + 1, y0 + fh - 2);
                    grid[x0, y] = CellType.Floor; grid[x0, y + 1] = CellType.Floor;
                    return (x0 - 1, y);
                }
            }
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

        private static void Mark(bool[,] occupied, int x0, int y0, int fw, int fh)
        {
            for (int x = x0; x < x0 + fw; x++)
                for (int y = y0; y < y0 + fh; y++)
                    occupied[x, y] = true;
        }
    }
}
