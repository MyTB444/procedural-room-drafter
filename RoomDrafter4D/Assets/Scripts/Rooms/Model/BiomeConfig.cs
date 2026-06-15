using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>Which layout pass shapes a biome's rooms.</summary>
    public enum BiomeLayout
    {
        Caves = 0,     // cellular-automata caverns (CavePass)
        Corridors = 1, // open water crossed by a random connected walkway network (CorridorPass)
    }

    // FloorPatch / WaterDecorPatch / IslandDecorPatch (the authored tile groups referenced below)
    // live in Model/TilePatches.cs.

    /// <summary>
    /// All tunable data + tile references for one biome's room generation. A ScriptableObject so
    /// each biome is just an asset (Create ▸ TRV ▸ Biome Config), mirroring the CharacterStats pattern.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeConfig", menuName = "TRV/Biome Config", order = 0)]
    public class BiomeConfig : ScriptableObject
    {
        [field: Header("Layout")]
        [field: Tooltip("Caves = organic cellular-automata caverns. Corridors = open water crossed " +
                        "by a random connected network of 2- or 4-wide walkways (Aqua).")]
        [field: SerializeField] public BiomeLayout Layout { get; private set; } = BiomeLayout.Caves;

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

        [field: Tooltip("Doors land anywhere along their edge, but never closer than this many " +
                        "cells to the room corners.")]
        [field: Min(0)]
        [field: SerializeField] public int DoorCornerPadding { get; private set; } = 2;

        [field: Header("Corridors")]
        [field: Tooltip("Minimum width of carved connector corridors (cells). At least 2 so TRV fits.")]
        [field: Min(1)]
        [field: SerializeField] public int CorridorWidth { get; private set; } = 2;

        [field: Header("Corridor layout")]
        [field: Tooltip("Chance a corridor rolls 4 cells wide instead of 2. Even then it must be a " +
                        "short connection, and at most 2 per room go wide — long door runs stay 2 wide. " +
                        "4-wide fits two 2×2 floor patches side by side.")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float WideCorridorChance { get; private set; } = 0.35f;

        [field: Tooltip("Chance a (non-4-wide) corridor rolls 3 cells wide instead of 2 — room for " +
                        "a 2×2 floor patch plus a strip of base floor beside it.")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float MediumCorridorChance { get; private set; } = 0.25f;

        [field: Tooltip("Random junction points added on top of the 4 door landings — each one adds " +
                        "a corridor, so more = denser. 0 = just the doors linked up.")]
        [field: Min(0)]
        [field: SerializeField] public int ExtraCorridorNodes { get; private set; } = 1;

        [field: Header("Tiles — Buildings (north-wall extensions)")]
        [field: Tooltip("Where ≥3 consecutive floor cells touch the north wall, that row becomes a " +
                        "'building' wall layer one cell inward (the corridor grows one row below to " +
                        "keep its size): left cap, middle(s), right cap. A run end facing water " +
                        "follows the water along the north wall if that stretch dead-ends at the " +
                        "room edge; otherwise the water tile it stops against becomes a framing " +
                        "wall (East/West wall tile).")]
        [field: SerializeField] public TileBase BuildingLeftTile { get; private set; }
        [field: SerializeField] public TileBase BuildingMiddleTile { get; private set; }
        [field: SerializeField] public TileBase BuildingRightTile { get; private set; }

        [field: Header("Water decor (Extras front layer)")]
        [field: Tooltip("Hand-made 2×3 decor groups painted over open water (any 3×3 all-water " +
                        "spot qualifies) — 2 to 4 per room, random pick per placement.")]
        [field: SerializeField] public WaterDecorPatch[] WaterDecorPatches { get; private set; }

        [field: Header("Island decor (Extras front layer)")]
        [field: Tooltip("Hand-made 1-tile or 2-tall decor pieces scattered on top of each island " +
                        "(4–5 per island), random pick per placement.")]
        [field: SerializeField] public IslandDecorPatch[] IslandDecorPatches { get; private set; }

        [field: Header("Water island")]
        [field: Tooltip("If the room's largest water body has at least this many cells, a strictly " +
                        "2-wide path is carved from the nearest floor to its middle, ending in a 4×4 " +
                        "island made of four 2×2 patches. 0 = never.")]
        [field: Min(0)]
        [field: SerializeField] public int MinWaterForIsland { get; private set; } = 40;

        [field: Header("Floor patches (decorative tile groups)")]
        [field: Tooltip("Pre-made tile groups (2×2, 4×4, …) stamped over the base floor where they fit.")]
        [field: SerializeField] public FloorPatch[] FloorPatches { get; private set; }

        [field: Tooltip("How densely patches pack the floor. Every floor cell is a candidate anchor; " +
                        "this is the chance each one tries to place a patch. ~1 = corridors almost " +
                        "fully tiled with patches (base floor only shows in the seams), ~0.1 = sparse.")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float FloorPatchCoverage { get; private set; } = 0.9f;

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
        [field: Tooltip("Floor pool: every floor cell picks one at random, all equal chance. " +
                        "One entry = a uniform floor.")]
        [field: SerializeField] public TileBase[] FloorTileVariants { get; private set; }

        [field: Tooltip("Water pool (e.g. 5 tiles): every water cell — and the water backdrop " +
                        "behind walls — picks one at random, all equal chance.")]
        [field: SerializeField] public TileBase[] WaterTileVariants { get; private set; }

        [field: SerializeField] public TileBase DoorTile { get; private set; }

        [field: Header("Tiles — Outer Walls")]
        [field: Tooltip("North-wall pool: every north AND south wall cell picks one at random, all " +
                        "equal chance. South is the pick rotated 180° — there is no south slot. " +
                        "One entry = a uniform band.")]
        [field: SerializeField] public TileBase[] NorthWallTileVariants { get; private set; }
        [field: SerializeField] public TileBase EastWallTile { get; private set; }
        [field: SerializeField] public TileBase WestWallTile { get; private set; }
        [field: Tooltip("Top-left corner block. Also used for the SW corner, rotated 90° left.")]
        [field: SerializeField] public TileBase NorthWestCornerTile { get; private set; }
        [field: Tooltip("Top-right corner block. Also used for the SE corner, rotated 90° right.")]
        [field: SerializeField] public TileBase NorthEastCornerTile { get; private set; }

        /// <summary>True when the north/south bands have tiles to draw from.</summary>
        public bool HasNorthWallTile =>
            NorthWallTileVariants != null && NorthWallTileVariants.Length > 0;

        /// <summary>
        /// Tile for a wall cell of the given kind (null = nothing painted). North/south cells draw
        /// from the variant pool, equal chance via the cell hash. Only the north side has slots —
        /// the painter derives the south side by rotation: South = North 180°, SouthWest =
        /// NorthWest 90° left, SouthEast = NorthEast 90° right.
        /// </summary>
        public TileBase WallTileFor(WallKind kind, int cellHash) => kind switch
        {
            WallKind.North => PickVariant(NorthWallTileVariants, cellHash),
            WallKind.South => PickVariant(NorthWallTileVariants, cellHash),
            WallKind.East => EastWallTile,
            WallKind.West => WestWallTile,
            WallKind.NorthWest => NorthWestCornerTile,
            WallKind.NorthEast => NorthEastCornerTile,
            WallKind.SouthWest => NorthWestCornerTile,
            WallKind.SouthEast => NorthEastCornerTile,
            _ => null,
        };

        /// <summary>Floor tile for a cell — random pick from the pool, equal chance.</summary>
        public TileBase FloorTileAt(int cellHash) => PickVariant(FloorTileVariants, cellHash);

        /// <summary>Water tile for a cell — random pick from the pool, equal chance.</summary>
        public TileBase WaterTileAt(int cellHash) => PickVariant(WaterTileVariants, cellHash);

        private static TileBase PickVariant(TileBase[] variants, int cellHash) =>
            variants == null || variants.Length == 0 ? null : variants[cellHash % variants.Length];
    }
}
