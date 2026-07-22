using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>
    /// A captured copy of one room's tiles across the 10 layers — including each cell's per-cell
    /// transform (rotation/scale), so a visited room (the hand-made start area, or rooms where you
    /// rotated tiles) is restored exactly when the player returns to its coord. Island decor objects
    /// aren't tiles, so their placements ride along here and are re-spawned from the pool on return.
    /// </summary>
    public class RoomSnapshot
    {
        public BoundsInt Bounds;
        public LayerSnapshot Walls;
        public LayerSnapshot UnseenCollision;
        public LayerSnapshot Collision2;
        public LayerSnapshot Collision3;
        public LayerSnapshot Collision4;
        public LayerSnapshot Map;
        public LayerSnapshot Door;
        public LayerSnapshot Extras;
        public LayerSnapshot ExtrasBehind;
        public LayerSnapshot ExtrasFullBehind;
        public LayerSnapshot ExtrasFrontOfPlayer;
        public LayerSnapshot FrontOfEverything;
        public List<PatchPlacement> IslandDecor;
        public BiomeConfig Biome; // which biome generated this room (decor indices map into its arrays)

        // Rolled main-room content (Halls big igroom). Re-spawned on revisit so the room stays consistent.
        public RectInt MainArea;          // interior of the big main igroom (width 0 = none)
        public int MainUpgradeIndex = -1; // index into Biome.MainRoomUpgrades, or -1
        public int MainFillerIndex = -1;  // index into Biome.MainRoomFillers, or -1
        public List<Vector2Int> MainFillerCells; // cells still holding an unbroken filler (breaks persist)
    }

    /// <summary>One tilemap layer's tiles plus their per-cell transform matrices (parallel arrays).</summary>
    public struct LayerSnapshot
    {
        public TileBase[] Tiles;
        public Matrix4x4[] Transforms;
    }
}
