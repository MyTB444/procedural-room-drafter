using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>
    /// All tunable data + tile references for one biome's room generation. A ScriptableObject so
    /// each biome is just an asset (Create ▸ TRV ▸ Biome Config), mirroring the CharacterStats pattern.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeConfig", menuName = "TRV/Biome Config", order = 0)]
    public class BiomeConfig : ScriptableObject
    {
        [field: Header("Grid (cells)")]
        [field: Min(7)]
        [field: SerializeField] public int Width { get; private set; } = 25;
        [field: Min(7)]
        [field: SerializeField] public int Height { get; private set; } = 25;

        [field: Header("Walls")]
        [field: Tooltip("Thickness of the outer wall ring in cells. The playable interior shrinks by " +
                        "this much per side, and each door keeps this many walls behind it (sealing the " +
                        "room until it's cleared — future). 2 = player can't exit yet.")]
        [field: Min(1)]
        [field: SerializeField] public int WallThickness { get; private set; } = 2;

        [field: Header("Doors")]
        [field: Tooltip("Width of each doorway in cells (centered on each edge). Keep >= corridor width.")]
        [field: Min(1)]
        [field: SerializeField] public int DoorWidth { get; private set; } = 2;

        [field: Tooltip("How many floor cells to carve inward from each door as a landing.")]
        [field: Min(1)]
        [field: SerializeField] public int LandingDepth { get; private set; } = 2;

        [field: Header("Corridors")]
        [field: Tooltip("Minimum width of carved connector corridors (cells). At least 2 so TRV fits.")]
        [field: Min(1)]
        [field: SerializeField] public int CorridorWidth { get; private set; } = 2;

        [field: Header("Cave (cellular automata)")]
        [field: Tooltip("Chance an interior cell starts as solid water (before smoothing).")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float FillPercent { get; private set; } = 0.45f;

        [field: Tooltip("Smoothing passes. More = rounder, larger caverns / water bodies.")]
        [field: Min(0)]
        [field: SerializeField] public int SmoothingIterations { get; private set; } = 4;

        [field: Tooltip("A cell becomes solid water if it has at least this many solid neighbours (of 8).")]
        [field: Range(1, 8)]
        [field: SerializeField] public int SolidThreshold { get; private set; } = 5;

        [field: Header("Tiles")]
        [field: SerializeField] public TileBase FloorTile { get; private set; }
        [field: SerializeField] public TileBase WaterTile { get; private set; }
        [field: SerializeField] public TileBase WallTile { get; private set; }
        [field: SerializeField] public TileBase DoorTile { get; private set; }
    }
}
