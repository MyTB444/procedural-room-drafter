using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>Which layout pass shapes a biome's rooms.</summary>
    public enum BiomeLayout
    {
        Corridors = 0, // open water crossed by a random connected walkway network (CorridorPass)
        Halls = 1,     // one big walled inner room ("igroom") ringed by water (HallPass)
        Open = 2,      // floor field with a water shoreline + high ground (OpenFieldPass — Anubis)
        Plain = 3,     // the plainest room: full wall ring, all-floor interior (PlainFieldPass — Lands)
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
                        "walkways (Aqua). Halls = one big walled igroom ringed by water. " +
                        "Open = floor field with a water shoreline + high ground (Anubis). " +
                        "Plain = full wall ring, all-floor interior (Lands).")]
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

        [field: Header("Main room content (Halls big igroom)")]
        [field: Tooltip("Upgrade prefabs (each an Upgrade with an Id) that can fill the big main igroom — " +
                        "each offered ONCE per run, removed once collected. RoomManager rolls ONE of " +
                        "{the still-available upgrades + the fillers below} at equal chance.")]
        [field: SerializeField] public GameObject[] MainRoomUpgrades { get; private set; }

        [field: Tooltip("Filler decor prefabs (e.g. crates, vases) — rolling one FILLS the big main igroom " +
                        "with that breakable prefab. Each is one equal-chance option alongside the upgrades.")]
        [field: SerializeField] public GameObject[] MainRoomFillers { get; private set; }

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

        [field: Tooltip("Path pool (Plain/Lands): the floor look of the strictly 2-wide corridors " +
                        "linking all 4 doors (PathPass). Per-cell random pick like the floor pool. " +
                        "Leave empty to reuse the regular Floor pool (path invisible).")]
        [field: SerializeField] public TileBase[] PathTileVariants { get; private set; }

        [field: Tooltip("FULL-grass pool (Plain/Lands): the 3×3/4×4 corner cubes at the heart of " +
                        "the grass gradient (GrassPass). Beyond the gradient's rings the rest of " +
                        "the room is the PLAIN floor = the regular Floor pool. Leave empty to " +
                        "reuse the Floor pool (gradient invisible).")]
        [field: SerializeField] public TileBase[] GrassFullTileVariants { get; private set; }

        [field: Tooltip("HALF-grass pool (Plain/Lands): the 1–2 thick ring around the full-grass " +
                        "cube. Leave empty to reuse the Floor pool.")]
        [field: SerializeField] public TileBase[] GrassHalfTileVariants { get; private set; }

        [field: Tooltip("VERY-FEW-grass pool (Plain/Lands): the 1–2 thick ring around the " +
                        "half-grass ring. Leave empty to reuse the Floor pool.")]
        [field: SerializeField] public TileBase[] GrassFewTileVariants { get; private set; }

        [field: Tooltip("NO-grass pool (Plain/Lands): the final 1–2 thick ring that closes the " +
                        "gradient — after it the rest of the floor is the PLAIN floor (the regular " +
                        "Floor pool). Leave empty to reuse the Floor pool.")]
        [field: SerializeField] public TileBase[] GrassNoneTileVariants { get; private set; }

        [field: Tooltip("Grass patch EDGE, NORTH side (Plain/Lands): painted on the plain-floor " +
                        "cell just ABOVE where the gradient ends (grass directly below it). Edge " +
                        "cells may sit right beside a path. Empty = no edge on that side.")]
        [field: SerializeField] public TileBase GrassEdgeNorthTile { get; private set; }

        [field: Tooltip("Grass patch edge, SOUTH side: the cell just below the patch.")]
        [field: SerializeField] public TileBase GrassEdgeSouthTile { get; private set; }

        [field: Tooltip("Grass patch edge, EAST side: the cell just right of the patch.")]
        [field: SerializeField] public TileBase GrassEdgeEastTile { get; private set; }

        [field: Tooltip("Grass patch edge, WEST side: the cell just left of the patch.")]
        [field: SerializeField] public TileBase GrassEdgeWestTile { get; private set; }

        [field: Tooltip("Grass patch edge, NORTH-EAST corner: the cell diagonally above-right of " +
                        "the patch's corner (grass only to its south-west).")]
        [field: SerializeField] public TileBase GrassEdgeNorthEastTile { get; private set; }

        [field: Tooltip("Grass patch edge, NORTH-WEST corner (grass only to its south-east).")]
        [field: SerializeField] public TileBase GrassEdgeNorthWestTile { get; private set; }

        [field: Tooltip("Grass patch edge, SOUTH-EAST corner (grass only to its north-west).")]
        [field: SerializeField] public TileBase GrassEdgeSouthEastTile { get; private set; }

        [field: Tooltip("Grass patch edge, SOUTH-WEST corner (grass only to its north-east).")]
        [field: SerializeField] public TileBase GrassEdgeSouthWestTile { get; private set; }

        [field: Tooltip("High-ground pool (Open/Anubis): the second floor look carved over the base " +
                        "field by HighGroundPass — the north band + its south-running corridors. " +
                        "Leave empty to reuse the regular Floor pool.")]
        [field: SerializeField] public TileBase[] HighGroundTileVariants { get; private set; }

        [field: Tooltip("Thin shoreline wall painted ON TOP of a REGULAR floor cell (on the " +
                        "Collision layer) when WATER lies directly to its LEFT (west). High-ground " +
                        "floor gets its own edge tiles (separate slots, to come). A cell bordering " +
                        "water on several sides paints ONE tile, picked in priority order " +
                        "left/right/top/bottom (corner tiles to come). Empty = off. The tile's " +
                        "Collider Type decides the physics: Sprite = a thin physical rim, None = " +
                        "pure visual.")]
        [field: SerializeField] public TileBase FloorEdgeLeftTile { get; private set; }

        [field: Tooltip("Thin shoreline wall for a regular floor cell with WATER directly to its " +
                        "RIGHT (east). Same rules as Floor Edge Left Tile.")]
        [field: SerializeField] public TileBase FloorEdgeRightTile { get; private set; }

        [field: Tooltip("Thin shoreline wall for a regular floor cell with WATER directly BELOW it " +
                        "(south). Same rules as Floor Edge Left Tile. ALSO used for water directly " +
                        "ABOVE (north): the painter paints this tile rotated 180° there, so no " +
                        "separate top tile is needed (must not lock its transform).")]
        [field: SerializeField] public TileBase FloorEdgeBottomTile { get; private set; }

        [field: Tooltip("Thin wall painted ON a HIGH-GROUND floor cell (Collision layer) when the " +
                        "cell below it is NOT high ground — the high ground's bottom rim. Skipped " +
                        "on the cell right above the middle of a stairs patch, so the stair mouth " +
                        "stays open. More high-ground directions to follow. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundEdgeBottomTile { get; private set; }

        [field: Tooltip("High ground's FIRST surrounding wall layer, LEFT side: painted on the cell " +
                        "just LEFT of every high-ground floor tile that has no high-ground floor to " +
                        "its left — on the Collision3 layer (the neighbour cell, NOT the high-ground " +
                        "tile itself). Empty = off.")]
        [field: SerializeField] public TileBase HighGroundFirstLayerLeftTile { get; private set; }

        [field: Tooltip("High ground's FIRST surrounding wall layer, RIGHT side: painted on the cell " +
                        "just RIGHT of every high-ground floor tile that has no high-ground floor to " +
                        "its right — on the Collision3 layer. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundFirstLayerRightTile { get; private set; }

        [field: Tooltip("High ground's SECOND surrounding wall layer, LEFT side: stacked on the SAME " +
                        "cells as the first-layer left tile, on the Collision4 layer. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundSecondLayerLeftTile { get; private set; }

        [field: Tooltip("High ground's SECOND surrounding wall layer, RIGHT side: stacked on the SAME " +
                        "cells as the first-layer right tile, on the Collision4 layer. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundSecondLayerRightTile { get; private set; }

        [field: Tooltip("Second-layer wall corner, version 1: painted (on Collision4) on the cell " +
                        "that has a BOTTOM rim tile to its RIGHT and a RIGHT surround tile BELOW it " +
                        "— e.g. the band cell on a corridor junction's right shoulder. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundCornerRightRightTile { get; private set; }

        [field: Tooltip("Second-layer wall corner, version 2: bottom rim to its LEFT, LEFT surround " +
                        "below it — the corridor junction's left shoulder. On Collision4. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundCornerLeftLeftTile { get; private set; }

        [field: Tooltip("Second-layer wall corner, version 3: bottom rim to its RIGHT, LEFT surround " +
                        "ABOVE it — the bottom of a corridor's left surround column, beside its end " +
                        "rim (replaces the straight second-layer piece there). On Collision4. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundCornerRightLeftTile { get; private set; }

        [field: Tooltip("Second-layer wall corner, version 4: bottom rim to its LEFT, RIGHT surround " +
                        "ABOVE it — the bottom of a corridor's right surround column, beside its end " +
                        "rim. On Collision4. Empty = off.")]
        [field: SerializeField] public TileBase HighGroundCornerLeftRightTile { get; private set; }

        [field: Tooltip("Corner rim joining two shoreline walls across a diagonal. Authored for " +
                        "water diagonally BOTTOM-RIGHT of a regular floor cell (the right " +
                        "neighbour's bottom wall meets the bottom neighbour's right wall); the " +
                        "other diagonals paint it rotated — bottom-left 90° right, top-right 90° " +
                        "left, top-left 180°. Must not lock its transform. Empty = off.")]
        [field: SerializeField] public TileBase FloorEdgeCornerTile { get; private set; }

        [field: Tooltip("Stairs patch (Open/Anubis): 3×3 tile group (9 tiles row-major, TOP row " +
                        "first) linking the high-ground band to the field below — placed beneath " +
                        "the band seam, one per separated regular-floor area (so every area sealed " +
                        "off by moats is reachable through the high ground). LEFT and RIGHT columns " +
                        "paint on the Collision2 layer (the rails — give those tiles a collider " +
                        "type), the MIDDLE column on the Extras front layer (the steps, drawn over " +
                        "the walkable ground). Empty = off.")]
        [field: SerializeField] public TileBase[] StairsPatch { get; private set; }

        [field: Tooltip("High-ground decor patch A (Open/Anubis): 2 tiles authored TOP first. The " +
                        "BOTTOM tile paints on the Collision5 layer (a solid base — give it a " +
                        "collider type), the top one on the ExtrasFrontOfPlayer layer. Scattered " +
                        "along the north edge and just above corridor ends — never ON a corridor's " +
                        "end row (the rim walls live there). Empty = off.")]
        [field: SerializeField] public TileBase[] HighGroundDecorPatchA { get; private set; }

        [field: Tooltip("High-ground decor patch B (Open/Anubis): 3 tiles authored TOP first — " +
                        "bottom on Collision5, upper two on ExtrasFrontOfPlayer. Placed ONCE per " +
                        "room, on one random south corridor, exactly 1 tile behind its last middle " +
                        "floor tile. Empty = off.")]
        [field: SerializeField] public TileBase[] HighGroundDecorPatchB { get; private set; }

        [field: Tooltip("High-ground decor patch C (Open/Anubis): 2×3 block, 6 tiles row-major TOP " +
                        "row first, all painted on the Collision5 layer. Anchored on the high/low " +
                        "seam: the top row's 2 tiles sit ON high-ground floor, the lower 4 on " +
                        "regular floor. 0–2 per room depending on free seam space, never " +
                        "overlapping the stairs. Empty = off.")]
        [field: SerializeField] public TileBase[] HighGroundDecorPatchC { get; private set; }

        [field: Tooltip("Patch A: vertical 2-tile strip hung beneath the END of every south-running " +
                        "high-ground corridor (Open/Anubis), authored TOP tile first (fills the top " +
                        "2 of the 3 foot-moat rows). Painted on ExtrasBehind — over the moat water " +
                        "on ExtrasFullBehind, under everything else. Empty = off.")]
        [field: SerializeField] public TileBase[] HighGroundEndPatchA { get; private set; }

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
        [field: Tooltip("Unused in the Open layout (Anubis) — those doors paint a random floor " +
                        "variant instead, so they're visually just ground.")]
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

        /// <summary>High-ground floor tile (Open/Anubis) — picks from the high-ground pool, or falls
        /// back to the regular floor pool when it's empty (so the flag is invisible until tiles are
        /// assigned).</summary>
        public TileBase HighGroundTileAt(int cellHash) =>
            HighGroundTileVariants != null && HighGroundTileVariants.Length > 0
                ? PickVariant(HighGroundTileVariants, cellHash)
                : FloorTileAt(cellHash);

        /// <summary>Path floor tile (Plain/Lands) — picks from the path pool, or falls back to the
        /// regular floor pool when it's empty (path invisible until tiles are assigned).</summary>
        public TileBase PathTileAt(int cellHash) =>
            PathTileVariants != null && PathTileVariants.Length > 0
                ? PickVariant(PathTileVariants, cellHash)
                : FloorTileAt(cellHash);

        /// <summary>Grass tile for a gradient level (4 = full, 3 = half, 2 = few, 1 = the no-grass
        /// ring) — each pool falls back to the regular (plain) floor pool while unassigned.</summary>
        public TileBase GrassTileAt(int level, int cellHash)
        {
            var pool = level switch
            {
                4 => GrassFullTileVariants,
                3 => GrassHalfTileVariants,
                2 => GrassFewTileVariants,
                _ => GrassNoneTileVariants,
            };
            return pool != null && pool.Length > 0 ? PickVariant(pool, cellHash) : FloorTileAt(cellHash);
        }

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
