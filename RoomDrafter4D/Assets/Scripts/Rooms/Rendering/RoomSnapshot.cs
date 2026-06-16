using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>
    /// A captured copy of one room's tiles across the 5 layers — including each cell's per-cell
    /// transform (rotation/scale), so a visited room (the hand-made start area, or rooms where you
    /// rotated tiles) is restored exactly when the player returns to its coord. Island decor objects
    /// aren't tiles, so their placements ride along here and are re-spawned from the pool on return.
    /// </summary>
    public class RoomSnapshot
    {
        public BoundsInt Bounds;
        public LayerSnapshot Walls;
        public LayerSnapshot Map;
        public LayerSnapshot Door;
        public LayerSnapshot Extras;
        public LayerSnapshot ExtrasBehind;
        public List<PatchPlacement> IslandDecor;
        public BiomeConfig Biome; // which biome generated this room (decor indices map into its arrays)
    }

    /// <summary>One tilemap layer's tiles plus their per-cell transform matrices (parallel arrays).</summary>
    public struct LayerSnapshot
    {
        public TileBase[] Tiles;
        public Matrix4x4[] Transforms;
    }
}
