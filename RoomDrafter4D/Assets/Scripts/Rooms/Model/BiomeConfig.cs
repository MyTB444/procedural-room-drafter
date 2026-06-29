using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>Which layout pass shapes a biome's rooms.</summary>
    public enum BiomeLayout
    {
        Corridors = 0, // open water crossed by a random connected walkway network (CorridorPass)
        Halls = 1,     // one big walled inner room ("igroom") ringed by water (HallPass)
    }

    // FloorPatch / WaterDecorPatch (the authored tile groups referenced below) live in
    // Model/TilePatches.cs. Island decor uses GameObject prefabs (IslandDecorObjects), not tiles.

    /// <summary>One island-decor prefab plus its independent chance to appear. (Class so new inspector
    /// entries default to Chance = 1.) NOTE: this [Serializable] name is asset data — renaming it loses
    /// assignments.</summary>
    [System.Serializable]
    public class IslandDecorEntry
    {
        [Tooltip("The decor prefab spawned on islands (pooled at runtime).")]
        public GameObject Prefab;

        [Tooltip("Independent chance (0–1) this type appears on a decor cell. 1 = always, 0 = never.")]
        [Range(0f, 1f)] public float Chance = 1f;
    }

    /// <summary>
    /// All tunable data + tile references for one biome's room generation. A ScriptableObject so
    /// each biome is just an asset (Create ▸ TRV ▸ Biome Config), mirroring the CharacterStats pattern.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeConfig", menuName = "TRV/Biome Config", order = 0)]
    public class BiomeConfig : ScriptableObject
    {
        [field: Header("Layout")]
        [field: Tooltip("Corridors = open water crossed by a random connected network of 2-/4-wide " +
                        "walkways (Aqua). Halls = one big walled igroom ringed by water.")]
        [field: SerializeField] public BiomeLayout Layout { get; private set; } = BiomeLayout.Corridors;

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

        [field: Header("Hall layout")]
        [field: Tooltip("Halls: minimum cells of water kept between igrooms and from the room edge.")]
        [field: Min(1)]
        [field: SerializeField] public int HallMoatThickness { get; private set; } = 2;

        [field: Tooltip("Floor size of the one guaranteed main igroom (square). Shrinks per room if the " +
                        "door layout is too tight to also fit the smaller extras.")]
        [field: Min(2)]
        [field: SerializeField] public int HallMainRoomSize { get; private set; } = 6;

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

        [field: Header("Enemies")]
        [field: Tooltip("Enemy prefabs that can spawn in this biome's rooms — one is picked at " +
                        "random per spawn (leave a slot empty to skip it). Spawned from EnemyPool.")]
        [field: SerializeField] public GameObject[] EnemyPrefabs { get; private set; }

        [field: Tooltip("How many enemies to spawn when a room is first generated.")]
        [field: Min(0)]
        [field: SerializeField] public int EnemiesPerRoom { get; private set; } = 3;

        [field: Tooltip("Optional miniboss prefab(s). At most ONE miniboss spawns per room, taking one of " +
                        "the EnemiesPerRoom slots (the rest are EnemyPrefabs); random pick if several. Empty = none.")]
        [field: SerializeField] public GameObject[] MinibossPrefabs { get; private set; }

        [field: Tooltip("Chance (0–1) a room actually spawns its one miniboss (when MinibossPrefabs is set).")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float MinibossChance { get; private set; } = 1f;

        [field: Tooltip("Enemies won't spawn within this many cells of the door the player enters from.")]
        [field: Min(0)]
        [field: SerializeField] public int EnemySpawnSafeRadius { get; private set; } = 4;

        [field: Header("Island decor (pooled objects)")]
        [field: Tooltip("Decor prefabs scattered on each island, each with a relative pick chance " +
                        "(weight). Spawned/reused from IslandDecorPool at runtime — not painted to a tilemap.")]
        [field: SerializeField] public IslandDecorEntry[] IslandDecorObjects { get; private set; }

        [field: Tooltip("How many decor objects per island (random in [min, max] inclusive). Capped by " +
                        "the free island floor cells.")]
        [field: Min(0)]
        [field: SerializeField] public int IslandDecorMinPerIsland { get; private set; } = 4;

        [field: Min(0)]
        [field: SerializeField] public int IslandDecorMaxPerIsland { get; private set; } = 5;

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

        [field: Header("Tiles — Floor & Water")]
        [field: Tooltip("Floor pool: every floor cell picks one at random, all equal chance. " +
                        "One entry = a uniform floor.")]
        [field: SerializeField] public TileBase[] FloorTileVariants { get; private set; }

        [field: Tooltip("Room-floor pool: fills the INTERIOR of Halls igrooms (a distinct floor from " +
                        "the corridor floor). Leave empty to reuse the regular Floor pool.")]
        [field: SerializeField] public TileBase[] RoomFloorTileVariants { get; private set; }

        [field: Tooltip("Water pool: every water cell — and the water backdrop behind walls — picks one " +
                        "at random, all equal chance. ONE entry = uniform water (use a single AnimatedTile " +
                        "here for animated water).")]
        [field: SerializeField] public TileBase[] WaterTileVariants { get; private set; }

        [field: Tooltip("North-edge water tile: the water cells along the very top of the room (the " +
                        "north water border in Halls) use this instead of the regular water pool. " +
                        "Empty = reuse the regular water.")]
        [field: SerializeField] public TileBase NorthWaterTile { get; private set; }

        [field: Tooltip("UnderWall tile: painted on the Extras FRONT layer over a water cell that sits " +
                        "directly BENEATH a Building or Floor tile (the shadowed underside). Empty = off.")]
        [field: SerializeField] public TileBase UnderWallTile { get; private set; }

        [field: Tooltip("Waterfall tile (Halls): 1–2 columns painted on the FrontOfEverything layer " +
                        "(topmost, above all others) from the north edge down — through the first floor " +
                        "block + the gap after it, landing on the next floor (igroom interior floor is " +
                        "flowed over, stairs stop it). Use an AnimatedTile (e.g. 6 frames). Empty = off.")]
        [field: SerializeField] public TileBase WaterfallTile { get; private set; }

        [field: Tooltip("Waterfall ender tile (Halls): painted on the Map layer at the 2 tiles just below " +
                        "where the igroom-threading waterfall stops (its middle tile) — the base/pool of " +
                        "the fall. A 3×3 collision pad is also stamped centered on the first of them. Empty = off.")]
        [field: SerializeField] public TileBase WaterfallEnderTile { get; private set; }

        [field: Tooltip("Waterfall base patch (Halls): a 3×3 tile group (9 tiles row-major TOP row first) " +
                        "painted on the COLLISION layer centred on the first waterfall ender tile (the " +
                        "fall's base). Use tiles with a collider type so it blocks movement. Empty = off.")]
        [field: SerializeField] public TileBase[] WaterfallBasePatch { get; private set; }

        [field: Tooltip("Floor decor column (Halls): a vertical tile strip authored TOP→BOTTOM, placed " +
                        "1–3 times on the field floor on the ExtrasFrontOfPlayer layer (bottom tile on the " +
                        "floor, rising up). Its bottom cell gets a water collision tile on UnseenCollision " +
                        "so the base blocks movement. Empty = off.")]
        [field: SerializeField] public TileBase[] FloorDecorColumn { get; private set; }

        [field: Tooltip("Cube patch (Halls): a 3×3 tile group, 9 tiles row-major TOP row first. 3 per " +
                        "room, split across two layers (always 2/1, never all on one): some on " +
                        "ExtrasBehind with the bottom-middle cell on an igroom's south corner, the rest on " +
                        "ExtrasFrontOfPlayer with the bottom edge along the room's south edge (kept clear " +
                        "of each other and the floor decor columns). Empty = off.")]
        [field: SerializeField] public TileBase[] CubePatch { get; private set; }

        [field: Header("Tiles — Room doors (the 4 perimeter doors)")]
        [field: SerializeField] public TileBase DoorTile { get; private set; }

        [field: Tooltip("Left half of the 2-wide ROOM door (the 4 perimeter doors), on the Door layer, " +
                        "rotated to the door's edge like the igroom doors. Replaces Door Tile when set " +
                        "(assign only on Halls). Authored facing NORTH; must NOT lock its transform.")]
        [field: SerializeField] public TileBase LeftRoomDoorTile { get; private set; }

        [field: Tooltip("Right half of the 2-wide room door (paired with Left Room Door Tile).")]
        [field: SerializeField] public TileBase RightRoomDoorTile { get; private set; }

        [field: Tooltip("Left half of the 2-wide 'leading' tiles painted one cell IN FRONT of each room " +
                        "door (inward, away from the edge), on the Extras front layer, rotated to the " +
                        "door's edge like the room door. Authored facing NORTH; must NOT lock transform.")]
        [field: SerializeField] public TileBase LeftLeadingTile { get; private set; }

        [field: Tooltip("Right half of the 2-wide leading tiles (paired with Left Leading Tile).")]
        [field: SerializeField] public TileBase RightLeadingTile { get; private set; }

        [field: Tooltip("Unique wall tile for the cell immediately LEFT (west) of the NORTH room door, " +
                        "painted on the collision layer in place of the regular flank wall (Aqua) or " +
                        "water border (Halls). Empty = off.")]
        [field: SerializeField] public TileBase NorthDoorLeftWallTile { get; private set; }

        [field: Tooltip("Unique wall tile for the cell immediately RIGHT (east) of the NORTH room door.")]
        [field: SerializeField] public TileBase NorthDoorRightWallTile { get; private set; }

        [field: Header("Tiles — Igroom entrance (stairs + door halves)")]
        [field: Tooltip("Left half of the 2-wide stairs in front of a Halls igroom entrance. Authored " +
                        "facing NORTH (the left half when ascending north); the painter rotates it to the " +
                        "entrance's direction. Must NOT lock its transform. Empty = no stairs.")]
        [field: SerializeField] public TileBase LeftStairTile { get; private set; }

        [field: Tooltip("Right half of the 2-wide stairs (paired with Left Stair Tile).")]
        [field: SerializeField] public TileBase RightStairTile { get; private set; }

        [field: Tooltip("Left half of the 2-wide igroom door, on the Extras front layer over the same " +
                        "doorway cells as the stairs. Authored facing NORTH; rotated to the entrance " +
                        "direction like the stairs. Must NOT lock its transform. Empty = no door.")]
        [field: SerializeField] public TileBase LeftDoorTile { get; private set; }

        [field: Tooltip("Right half of the 2-wide igroom door (paired with Left Door Tile).")]
        [field: SerializeField] public TileBase RightDoorTile { get; private set; }

        [field: Header("Tiles — ExtrasBehind background (Halls)")]
        [field: Tooltip("Horizontal bands painted on ExtrasBehind from beneath the north edge down: a " +
                        "random run of NorthBehind, then Transition1, Transition2, then Blank fills the " +
                        "rest to the south edge. Assign North Behind to enable.")]
        [field: SerializeField] public TileBase NorthBehindTile { get; private set; }
        [field: SerializeField] public TileBase Transition1Tile { get; private set; }
        [field: SerializeField] public TileBase Transition2Tile { get; private set; }
        [field: SerializeField] public TileBase BlankBehindTile { get; private set; }

        [field: Tooltip("End-cap tiles for the NorthBehind / Transition1 bands where they meet a floor " +
                        "tile horizontally: floor to the WEST → the Left end, floor to the EAST → the " +
                        "Right end. Empty = use the regular band tile.")]
        [field: SerializeField] public TileBase NorthBehindLeftEndTile { get; private set; }
        [field: SerializeField] public TileBase NorthBehindRightEndTile { get; private set; }
        [field: SerializeField] public TileBase Transition1LeftEndTile { get; private set; }
        [field: SerializeField] public TileBase Transition1RightEndTile { get; private set; }

        [field: Header("Tiles — Walls")]
        [field: Tooltip("ON (Aqua): derive the south/SW/SE wall tiles by rotating the north/NW/NE " +
                        "ones — leave the South/SW/SE slots empty. OFF (Halls): assign every " +
                        "direction explicitly, no rotation.")]
        [field: SerializeField] public bool RotateSouthWalls { get; private set; } = true;

        [field: Tooltip("North-wall pool: each north wall cell picks one at random (1 entry = uniform).")]
        [field: SerializeField] public TileBase[] NorthWallTileVariants { get; private set; }
        [field: Tooltip("South-wall pool. Used only when Rotate South Walls is OFF (else rotated north).")]
        [field: SerializeField] public TileBase[] SouthWallTileVariants { get; private set; }
        [field: SerializeField] public TileBase EastWallTile { get; private set; }
        [field: SerializeField] public TileBase WestWallTile { get; private set; }
        [field: SerializeField] public TileBase NorthWestCornerTile { get; private set; }
        [field: SerializeField] public TileBase NorthEastCornerTile { get; private set; }
        [field: Tooltip("Used only when Rotate South Walls is OFF (else NW/NE rotated 90°).")]
        [field: SerializeField] public TileBase SouthWestCornerTile { get; private set; }
        [field: SerializeField] public TileBase SouthEastCornerTile { get; private set; }

        /// <summary>True when the north band has tiles to draw from (gates the south 180° rotation).</summary>
        public bool HasNorthWallTile =>
            NorthWallTileVariants != null && NorthWallTileVariants.Length > 0;

        /// <summary>
        /// Tile for a wall cell of the given kind (null = nothing painted). When
        /// <see cref="RotateSouthWalls"/> is on, the south side reuses the north/NW/NE tiles and the
        /// painter rotates them; when off, it returns the explicit South/SW/SE slots (no rotation).
        /// </summary>
        public TileBase WallTileFor(WallKind kind, int cellHash) => kind switch
        {
            WallKind.North => PickVariant(NorthWallTileVariants, cellHash),
            WallKind.South => PickVariant(RotateSouthWalls ? NorthWallTileVariants : SouthWallTileVariants, cellHash),
            WallKind.East => EastWallTile,
            WallKind.West => WestWallTile,
            WallKind.NorthWest => NorthWestCornerTile,
            WallKind.NorthEast => NorthEastCornerTile,
            WallKind.SouthWest => RotateSouthWalls ? NorthWestCornerTile : SouthWestCornerTile,
            WallKind.SouthEast => RotateSouthWalls ? NorthEastCornerTile : SouthEastCornerTile,
            _ => null,
        };

        /// <summary>Floor tile for a cell — random pick from the pool, equal chance.</summary>
        public TileBase FloorTileAt(int cellHash) => PickVariant(FloorTileVariants, cellHash);

        /// <summary>Floor tile for an igroom INTERIOR cell — picks from the room-floor pool, or falls
        /// back to the regular floor pool when that pool is empty (so biomes that don't set it look
        /// unchanged).</summary>
        public TileBase RoomFloorTileAt(int cellHash) =>
            RoomFloorTileVariants != null && RoomFloorTileVariants.Length > 0
                ? PickVariant(RoomFloorTileVariants, cellHash)
                : FloorTileAt(cellHash);

        /// <summary>Water tile for a cell — random pick from the pool, equal chance.</summary>
        public TileBase WaterTileAt(int cellHash) => PickVariant(WaterTileVariants, cellHash);

        /// <summary>Water tile for a north-edge cell — the dedicated north tile, or the regular water
        /// pool when none is set.</summary>
        public TileBase NorthWaterTileAt(int cellHash) =>
            NorthWaterTile != null ? NorthWaterTile : WaterTileAt(cellHash);

        private static TileBase PickVariant(TileBase[] variants, int cellHash) =>
            variants == null || variants.Length == 0 ? null : variants[cellHash % variants.Length];
    }
}
