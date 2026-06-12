using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Guarantees every door reaches the main cavern. Finds the largest walkable region, carves a
    /// corridor (CorridorWidth-wide) from each door landing into it, then floods any stray floor
    /// pockets with water so no unreachable floor is left behind.
    /// </summary>
    public class ConnectivityPass : IRoomPass
    {
        private static readonly (int dx, int dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        private static readonly Cardinal[] FourDirections =
            { Cardinal.North, Cardinal.East, Cardinal.South, Cardinal.West };

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int margin = config.WallThickness; // never carve into the wall ring / door seals

            foreach (var landing in DoorLandings(config, grid))
            {
                var main = LargestRegion(grid);
                if (!main.Contains(landing))
                    CarveTo(grid, landing, main, config.CorridorWidth, margin);
            }

            // Remove stray pockets that didn't end up connected to the main region.
            var finalMain = LargestRegion(grid);
            for (int x = margin; x < grid.Width - margin; x++)
                for (int y = margin; y < grid.Height - margin; y++)
                    if (grid[x, y] == CellType.Floor && !finalMain.Contains((x, y)))
                        grid[x, y] = CellType.Water;
        }

        /// <summary>The interior floor landing just inside each of the 4 doors.</summary>
        private static List<(int, int)> DoorLandings(BiomeConfig config, RoomGrid grid)
        {
            var result = new List<(int, int)>(4);
            foreach (var d in FourDirections)
            {
                var l = RoomDoors.Landing(config, grid, d);
                result.Add((l.x, l.y));
            }
            return result;
        }

        /// <summary>Largest connected component of walkable (Floor/Door) cells.</summary>
        private static HashSet<(int, int)> LargestRegion(RoomGrid grid)
        {
            var visited = new bool[grid.Width, grid.Height];
            var best = new HashSet<(int, int)>();

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (visited[x, y] || !grid[x, y].IsWalkable()) continue;
                    var region = Flood(grid, x, y, visited);
                    if (region.Count > best.Count) best = region;
                }
            return best;
        }

        private static HashSet<(int, int)> Flood(RoomGrid grid, int sx, int sy, bool[,] visited)
        {
            var region = new HashSet<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue((sx, sy));
            visited[sx, sy] = true;

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                region.Add((x, y));
                foreach (var (dx, dy) in Dirs)
                {
                    int nx = x + dx, ny = y + dy;
                    if (grid.InBounds(nx, ny) && !visited[nx, ny] && grid[nx, ny].IsWalkable())
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
            return region;
        }

        /// <summary>BFS the shortest interior path from start to any cell in target, carving it.</summary>
        private static void CarveTo(RoomGrid grid, (int, int) start, HashSet<(int, int)> target, int width, int margin)
        {
            var cameFrom = new Dictionary<(int, int), (int, int)> { [start] = start };
            var queue = new Queue<(int, int)>();
            queue.Enqueue(start);

            (int, int) hit = start;
            bool found = false;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (target.Contains(cur)) { hit = cur; found = true; break; }

                foreach (var (dx, dy) in Dirs)
                {
                    int nx = cur.Item1 + dx, ny = cur.Item2 + dy;
                    if (!grid.IsInterior(nx, ny, margin)) continue; // never carve into the wall ring
                    var n = (nx, ny);
                    if (cameFrom.ContainsKey(n)) continue;
                    cameFrom[n] = cur;
                    queue.Enqueue(n);
                }
            }

            if (!found) return;

            for (var c = hit; !c.Equals(start); c = cameFrom[c])
                CarveCell(grid, c.Item1, c.Item2, width, margin);
            CarveCell(grid, start.Item1, start.Item2, width, margin);
        }

        /// <summary>Carve a width×width floor block, staying inside the wall ring and never erasing a door.</summary>
        private static void CarveCell(RoomGrid grid, int x, int y, int width, int margin)
        {
            for (int dx = 0; dx < width; dx++)
                for (int dy = 0; dy < width; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (grid.IsInterior(nx, ny, margin) && grid[nx, ny] != CellType.Door)
                        grid[nx, ny] = CellType.Floor;
                }
        }
    }
}
