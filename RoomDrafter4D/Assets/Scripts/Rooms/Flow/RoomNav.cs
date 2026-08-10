using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Walkability + A* pathfinding for the CURRENT room, so enemies can route AROUND water/walls
    /// instead of pressing straight into them. RoomManager rebuilds it on every room load from the
    /// painted collision layers (a cell is walkable when HasSolidAt finds no solid tile) and sets it as
    /// <see cref="Current"/>. Cell↔world conversion is delegated to the painter, so callers work in
    /// world space. Pure logic — no scene presence; reusable by any enemy.
    /// </summary>
    public class RoomNav
    {
        /// <summary>The nav grid for the room currently shown (set by RoomManager).</summary>
        public static RoomNav Current { get; set; }

        private TilemapPainter _painter;
        private bool[,] _walkable;
        private int _w, _h;

        private static readonly Vector2Int[] Dirs8 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        public void Rebuild(TilemapPainter painter, int width, int height)
        {
            _painter = painter;
            _w = width;
            _h = height;
            _walkable = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    _walkable[x, y] = !painter.HasSolidAt(x, y);
        }

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;
        public bool IsWalkable(int x, int y) => InBounds(x, y) && _walkable[x, y];

        public Vector2Int CellOf(Vector3 world) => _painter.WorldToGridCell(world);
        public Vector3 WorldOf(Vector2Int cell) => _painter.CellCenterWorld(cell.x, cell.y);

        /// <summary>
        /// A* from <paramref name="start"/> to <paramref name="goal"/> (8-connected, no corner
        /// cutting). Returns the cells start→goal inclusive, or null if blocked/unreachable.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            if (_walkable == null || !IsWalkable(start.x, start.y) || !IsWalkable(goal.x, goal.y))
                return null;
            if (start == goal) return new List<Vector2Int> { start };

            int n = _w * _h;
            var g = new float[n];
            var f = new float[n];
            var cameFrom = new int[n];
            var closed = new bool[n];
            var inOpen = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; cameFrom[i] = -1; }

            int startI = start.x * _h + start.y;
            int goalI = goal.x * _h + goal.y;
            g[startI] = 0f;
            f[startI] = Heuristic(start, goal);

            var open = new List<int> { startI };
            inOpen[startI] = true;

            while (open.Count > 0)
            {
                // Pop the lowest f — linear scan is fine for a ~350-cell room.
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (f[open[i]] < f[open[best]]) best = i;
                int currentI = open[best];
                if (currentI == goalI) return Reconstruct(cameFrom, currentI);

                open[best] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                inOpen[currentI] = false;
                closed[currentI] = true;

                int cx = currentI / _h, cy = currentI % _h;
                foreach (var d in Dirs8)
                {
                    int nx = cx + d.x, ny = cy + d.y;
                    if (!IsWalkable(nx, ny)) continue;
                    // No diagonal corner cutting: both orthogonal neighbours must be open.
                    if (d.x != 0 && d.y != 0 && (!IsWalkable(cx + d.x, cy) || !IsWalkable(cx, cy + d.y)))
                        continue;

                    int ni = nx * _h + ny;
                    if (closed[ni]) continue;
                    float step = (d.x != 0 && d.y != 0) ? 1.41421356f : 1f;
                    float tentative = g[currentI] + step;
                    if (tentative >= g[ni]) continue;

                    cameFrom[ni] = currentI;
                    g[ni] = tentative;
                    f[ni] = tentative + Heuristic(new Vector2Int(nx, ny), goal);
                    if (!inOpen[ni]) { open.Add(ni); inOpen[ni] = true; }
                }
            }
            return null; // unreachable
        }

        // Octile distance — admissible for 8-connected movement.
        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) + (1.41421356f - 2f) * Mathf.Min(dx, dy);
        }

        private List<Vector2Int> Reconstruct(int[] cameFrom, int currentI)
        {
            var path = new List<Vector2Int>();
            for (int i = currentI; i != -1; i = cameFrom[i])
                path.Add(new Vector2Int(i / _h, i % _h));
            path.Reverse();
            return path;
        }
    }
}
