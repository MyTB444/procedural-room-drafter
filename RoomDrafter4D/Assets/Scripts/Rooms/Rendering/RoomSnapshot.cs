using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>
    /// A captured copy of one room's tiles across the 4 layers — including each cell's per-cell
    /// transform (rotation/scale), so a visited room (the hand-made start area, or rooms where you
    /// rotated tiles) is restored exactly when the player returns to its coord.
    /// </summary>
    public class RoomSnapshot
    {
        public BoundsInt Bounds;
        public LayerSnapshot Walls;
        public LayerSnapshot Map;
        public LayerSnapshot Door;
        public LayerSnapshot Extras;
    }

    /// <summary>One tilemap layer's tiles plus their per-cell transform matrices (parallel arrays).</summary>
    public struct LayerSnapshot
    {
        public TileBase[] Tiles;
        public Matrix4x4[] Transforms;
    }
}
