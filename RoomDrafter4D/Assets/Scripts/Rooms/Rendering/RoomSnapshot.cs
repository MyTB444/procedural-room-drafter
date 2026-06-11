using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>
    /// A captured copy of one room's tiles across the 4 layers, so a visited room — including the
    /// hand-made starting area — can be restored exactly when the player returns to its coord.
    /// </summary>
    public class RoomSnapshot
    {
        public BoundsInt Bounds;
        public TileBase[] Walls;
        public TileBase[] Map;
        public TileBase[] Door;
        public TileBase[] Extras;
    }
}
