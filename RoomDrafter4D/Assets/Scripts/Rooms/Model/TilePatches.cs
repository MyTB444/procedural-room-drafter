using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    // The biome's hand-authored tile groups, and the runtime record of where one was placed.
    // NOTE: the [Serializable] class names are part of the BiomeConfig asset data — renaming them
    // loses what's assigned in the inspector.

    /// <summary>
    /// A pre-made square group of floor tiles (e.g. a 2×2 framed block) stamped over the base
    /// floor by <see cref="FloorDecorPass"/> wherever it fully fits on floor cells.
    /// </summary>
    [System.Serializable]
    public class FloorPatch
    {
        [Tooltip("The patch covers Size×Size cells (e.g. 2 or 4).")]
        [Min(1)] public int Size = 2;

        [Tooltip("Size×Size tiles, row-major with the TOP row first (left→right) — laid out the " +
                 "same way you see the group on the sprite sheet.")]
        public TileBase[] Tiles;
    }

    /// <summary>
    /// A hand-made decor group painted over open water on the Extras (front) layer by
    /// <see cref="WaterDecorPass"/>. Fixed footprint: <see cref="Width"/>×<see cref="Height"/>.
    /// </summary>
    [System.Serializable]
    public class WaterDecorPatch
    {
        public const int Width = 2;
        public const int Height = 3;

        [Tooltip("6 tiles: 2 wide × 3 tall, row-major with the TOP row first (left→right).")]
        public TileBase[] Tiles;
    }

    /// <summary>
    /// One placed item: its bottom-left cell plus an index into whichever BiomeConfig array it came
    /// from — floor patches / water decor tiles (stamped by the painter), or island decor prefabs
    /// (spawned by <see cref="IslandDecorPool"/>). Each source has its own list on <see cref="RoomGrid"/>.
    /// </summary>
    public readonly struct PatchPlacement
    {
        public readonly int X;
        public readonly int Y;
        public readonly int PatchIndex;

        public PatchPlacement(int x, int y, int patchIndex)
        {
            X = x;
            Y = y;
            PatchIndex = patchIndex;
        }
    }
}
