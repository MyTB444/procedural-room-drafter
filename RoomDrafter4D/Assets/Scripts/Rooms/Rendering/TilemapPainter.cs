using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TRV
{
    /// <summary>
    /// Renders a finished <see cref="RoomGrid"/> onto the 4 Tilemap layers. Pure view — it reads
    /// cell types and stamps the matching tile: Wall/Water → Walls layer, Floor → Map layer,
    /// Door → Door layer (plus floor underneath so it's walkable). Extras handled by a later pass.
    /// </summary>
    public class TilemapPainter : MonoBehaviour
    {
        [Header("Tilemap Layers")]
        [Tooltip("Walls layer (your scene's 'Collision' tilemap) — draws Wall + Water, blocks movement.")]
        [SerializeField] private Tilemap wallsTilemap;
        [Tooltip("UnseenCollision — in Halls the water-boundary collision blocks (NOT igroom walls) go " +
                 "here instead of the Walls layer. Needs a TilemapCollider2D; assign for Halls.")]
        [SerializeField] private Tilemap unseenCollisionTilemap;
        [SerializeField] private Tilemap mapTilemap;
        [SerializeField] private Tilemap doorTilemap;
        [Tooltip("Front decor layer (your 'ExtrasFront') — igroom doors, leading tiles, UnderWall, " +
                 "rendered above the water but behind the player.")]
        [SerializeField] private Tilemap extrasTilemap;
        [Tooltip("ExtrasFrontOfPlayer — renders ABOVE the player. Gets the 2×3 water decor groups.")]
        [SerializeField] private Tilemap extrasFrontOfPlayerTilemap;
        [Tooltip("FrontOfEverything — the topmost layer (renders above all others). Gets the waterfalls.")]
        [SerializeField] private Tilemap frontOfEverythingTilemap;
        [Tooltip("ExtrasBehind — renders under the Walls layer. Gets a water tile beneath every wall " +
                 "cell so wall sprites with transparency show water behind them.")]
        [SerializeField] private Tilemap extrasBehindTilemap;
        [Tooltip("ExtrasFullBehind — the backmost layer. Gets the Halls north→south banded background.")]
        [SerializeField] private Tilemap extrasFullBehindTilemap;

        [Header("Placement")]
        [Tooltip("Cell coordinate the grid's (0,0) maps to, so rooms paint onto your existing footprint. " +
                 "For the current scene that's the bottom-left of your room, e.g. (-13, -8, 0).")]
        [SerializeField] private Vector3Int originCell = Vector3Int.zero;

        // The south side has no tile slots of its own — it reuses the north tiles rotated:
        // South = North 180°, SouthWest = NorthWest 90° left (CCW), SouthEast = NorthEast 90° right (CW).
        private static readonly Matrix4x4 Rot180 = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 180f));
        private static readonly Matrix4x4 RotLeft90 = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
        private static readonly Matrix4x4 RotRight90 = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -90f));

        // Solid wall cells painted just OUTSIDE each door so the player can't walk into the void
        // through the opening (the door still transitions on step — that's a trigger, not a walk-through).
        private const int DoorBackingDepth = 2;

        // How many waterfall columns to drop from the north edge (Halls), inclusive range.
        private const int MinWaterfalls = 1, MaxWaterfalls = 2;

        // How many floor decor columns to scatter on the field floor (Halls), inclusive range.
        private const int MinFloorDecor = 2, MaxFloorDecor = 3;
        // Keep a (solid-based) floor decor at least this many cells from any door landing / other decor.
        private const int FloorDecorDoorClearance = 3, FloorDecorSpacing = 4;

        // 3×3 cube patches per room (Halls), split across two layers (never all on one).
        private const int CubeSize = 3;
        // 2–3 cubes per room: 2 → 1 behind + 1 front, 3 → 2/1.
        private const int MinCubeCount = 2, MaxCubeCount = 3;
        // Behind cubes can't anchor on a south corner in the bottom this-many rows (keeps them off the
        // front cubes at the south edge while leaving most of the map available).
        private const int CubeBehindMinRow = 5;

        /// <summary>World position of the centre of a grid cell — for placing the player on a landing.</summary>
        public Vector3 CellCenterWorld(int gridX, int gridY)
        {
            var cell = new Vector3Int(originCell.x + gridX, originCell.y + gridY, 0);
            return mapTilemap != null ? mapTilemap.GetCellCenterWorld(cell) : (Vector3)cell;
        }

        /// <summary>Grid cell (room space) a world position falls in — inverse of CellCenterWorld.</summary>
        public Vector2Int WorldToGridCell(Vector3 world)
        {
            var cell = mapTilemap != null ? mapTilemap.WorldToCell(world) : Vector3Int.RoundToInt(world);
            return new Vector2Int(cell.x - originCell.x, cell.y - originCell.y);
        }

        /// <summary>True if a grid cell holds a solid tile — Wall/Building (+ Aqua water) live on the
        /// Walls/Collision layer, Halls water lives on the UnseenCollision layer — i.e. NOT walkable.
        /// Used to build the nav grid for pathfinding, so it must see BOTH collision layers.</summary>
        public bool HasSolidAt(int gridX, int gridY)
        {
            var pos = new Vector3Int(originCell.x + gridX, originCell.y + gridY, 0);
            return (wallsTilemap != null && wallsTilemap.HasTile(pos))
                || (unseenCollisionTilemap != null && unseenCollisionTilemap.HasTile(pos));
        }

        /// <summary>Where a water-collision tile goes: the UnseenCollision layer in Halls (so the water
        /// boundary isn't on the visible Walls layer), the Walls layer otherwise (or if it's unassigned).</summary>
        private Tilemap WaterCollisionTilemap(BiomeConfig config) =>
            config.Layout == BiomeLayout.Halls && unseenCollisionTilemap != null ? unseenCollisionTilemap : wallsTilemap;

        public void Paint(RoomGrid grid, BiomeConfig config)
        {
            Clear();

            // Halls puts everything "behind" (the per-wall water backdrop AND the banded background) on
            // the backmost ExtrasFullBehind layer; other biomes use the regular ExtrasBehind.
            var behindTilemap = config.Layout == BiomeLayout.Halls ? extrasFullBehindTilemap : extrasBehindTilemap;
            // Halls water collision goes on the UnseenCollision layer (not the visible Walls layer).
            var waterTilemap = WaterCollisionTilemap(config);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var pos = new Vector3Int(originCell.x + x, originCell.y + y, 0);
                    int cellHash = RoomSeed.CellHash(grid.VariantSeed, x, y);
                    bool northEdge = y >= grid.Height - config.WallThickness; // top water-border row(s)
                    switch (grid[x, y])
                    {
                        case CellType.Wall:
                        {
                            var kind = grid.TryGetWallKind(x, y, out var k)
                                ? k
                                : WallKindUtil.Classify(grid, x, y, config.WallThickness);
                            var wallTile = config.WallTileFor(kind, cellHash);
                            // A building run's end FRAMING wall (an interior wall with a Building beside it,
                            // placed where the run stops against water) gets the north-door flank tiles
                            // MIRRORED — matching the door-frame ends. Interior-only (positional Fill) so a
                            // ring wall that merely sits next to a building keeps its edge tile.
                            bool buildingW = grid.Get(x - 1, y) == CellType.Building;
                            bool buildingE = grid.Get(x + 1, y) == CellType.Building;
                            if ((buildingW || buildingE) &&
                                WallKindUtil.Classify(x, y, grid.Width, grid.Height, config.WallThickness) == WallKind.Fill)
                            {
                                if (buildingW && config.NorthDoorLeftWallTile != null) wallTile = config.NorthDoorLeftWallTile;
                                else if (buildingE && config.NorthDoorRightWallTile != null) wallTile = config.NorthDoorRightWallTile;
                            }
                            wallsTilemap.SetTile(pos, wallTile);
                            if (TryGetWallRotation(kind, config, out var rotation))
                                wallsTilemap.SetTransformMatrix(pos, rotation);
                            // South-facing walls sit on floor (the sprite's base shows ground, not void).
                            // Only for biomes with explicit south tiles (Halls); when RotateSouthWalls
                            // is on (Aqua) the south side is north tiles over the water backdrop instead.
                            // In Halls every south wall is an igroom's, so use the igroom floor set —
                            // the base matches the room interior just above it.
                            if (IsSouthFacing(kind) && !config.RotateSouthWalls)
                                mapTilemap.SetTile(pos, config.RoomFloorTileAt(cellHash));
                            // Water backdrop under the wall sprite — an igroom wall sitting on the room's
                            // north edge gets the north-edge water behind it, so it blends with the border.
                            if (behindTilemap)
                                behindTilemap.SetTile(pos, northEdge
                                    ? config.NorthWaterTileAt(cellHash)
                                    : config.WaterTileAt(cellHash));
                            break;
                        }
                        case CellType.Water:
                            // Open (Anubis) INTERIOR water — the moat around the high-ground
                            // corridors: the water sprite goes on the BACKMOST layer (so high-ground
                            // edge sprites on Map draw over it), while the blocking collider comes
                            // from the same tile on UnseenCollision (the Halls water pattern —
                            // HasSolidAt reads that layer, so physics AND nav both block).
                            if (config.Layout == BiomeLayout.Open &&
                                grid.IsInterior(x, y, config.WallThickness))
                            {
                                if (extrasFullBehindTilemap)
                                    extrasFullBehindTilemap.SetTile(pos, config.WaterTileAt(cellHash));
                                if (unseenCollisionTilemap)
                                    unseenCollisionTilemap.SetTile(pos, config.WaterTileAt(cellHash));
                                break;
                            }
                            // North-edge water (the top of the room's water border) gets its own tile.
                            // In Halls this lands on the UnseenCollision layer instead of Walls.
                            waterTilemap.SetTile(pos, northEdge
                                ? config.NorthWaterTileAt(cellHash)
                                : config.WaterTileAt(cellHash));
                            break;
                        case CellType.Floor:
                            // High ground (Open/Anubis) wins; then igroom interiors get the distinct
                            // room-floor tiles; everything else the regular floor.
                            mapTilemap.SetTile(pos, grid.IsHighGround(x, y)
                                ? config.HighGroundTileAt(cellHash)
                                : grid.IsRoomFloor(x, y)
                                    ? config.RoomFloorTileAt(cellHash)
                                    : config.FloorTileAt(cellHash));
                            break;
                        case CellType.Door:
                        {
                            // A door bordering high ground (Open/Anubis north band) shows the
                            // high-ground look, so it blends with the ground it opens onto.
                            var groundTile = TouchesHighGround(grid, x, y)
                                ? config.HighGroundTileAt(cellHash)
                                : config.FloorTileAt(cellHash);
                            mapTilemap.SetTile(pos, groundTile); // walkable floor under the door
                            if (config.LeftRoomDoorTile != null) // Halls: 2-wide left/right door, rotated to its edge
                            {
                                var edge = NearestEdge(x, y, grid.Width, grid.Height);
                                int doorStart = grid.DoorStarts[(int)edge];
                                bool firstCell = edge is Cardinal.North or Cardinal.South ? x == doorStart : y == doorStart;
                                bool isLeft = firstCell == (edge is Cardinal.South or Cardinal.East);
                                var rot = EntranceRotation(edge);
                                doorTilemap.SetTile(pos, isLeft ? config.LeftRoomDoorTile : config.RightRoomDoorTile);
                                doorTilemap.SetTransformMatrix(pos, rot);

                                // A matching 'leading' tile one cell in front of the door (inward), on Extras
                                // front. It faces the opposite way to the door — a full 180° flip of the
                                // 2-wide pair, so the rotation is +180° AND the left/right halves swap cells.
                                if (config.LeftLeadingTile != null && extrasTilemap != null)
                                {
                                    var step = InwardStep(edge);
                                    var lead = new Vector3Int(pos.x + step.x, pos.y + step.y, 0);
                                    extrasTilemap.SetTile(lead, isLeft ? config.RightLeadingTile : config.LeftLeadingTile);
                                    extrasTilemap.SetTransformMatrix(lead, rot * Rot180);
                                }
                            }
                            else
                            {
                                // Open (Anubis): the door is VISUALLY just ground — the same variant
                                // as painted beneath it. Trigger comes from ForceDoorColliders.
                                doorTilemap.SetTile(pos, config.Layout == BiomeLayout.Open
                                    ? groundTile
                                    : config.DoorTile);
                            }
                            break;
                        }
                        case CellType.Building:
                            // Corridor buildings sit on floor; water-following extensions stand in
                            // water (their underlay shows through any sprite transparency).
                            if (grid.Get(x, y - 1) == CellType.Water)
                            {
                                if (behindTilemap)
                                    behindTilemap.SetTile(pos, config.WaterTileAt(cellHash));
                            }
                            else
                            {
                                mapTilemap.SetTile(pos, config.FloorTileAt(cellHash));
                            }
                            wallsTilemap.SetTile(pos, BuildingTile(grid, x, y, config));
                            break;
                    }
                }
            }

            PaintExtrasBehindBands(grid, config);
            PaintFloorPatches(grid, config);
            PaintWaterDecor(grid, config);
            var floorDecor = PaintFloorDecor(grid, config);
            PaintUnderWalls(grid, config); // after water decor so the underside shadow wins on overlap
            PaintDoorBackingWalls(grid, config);
            PaintNorthDoorWalls(grid, config);
            PaintIgroomWallUnderlay(grid, config);
            PaintStairs(grid, config);
            PaintDoors(grid, config);
            PaintWaterfalls(grid, config);
            PaintCubePatches(grid, config, floorDecor); // after floor decor so cubes stay clear of it
            ForceDoorColliders(new BoundsInt(originCell, new Vector3Int(grid.Width, grid.Height, 1)));
            // Island decor is no longer painted — it's spawned as pooled GameObjects by
            // IslandDecorPool (driven by RoomManager from grid.IslandDecor).
        }

        /// <summary>True when any 4-neighbour of the cell is flagged high ground — used so a door
        /// cell (in the ring, never flagged itself) paints the ground look it opens onto.</summary>
        private static bool TouchesHighGround(RoomGrid grid, int x, int y) =>
            grid.IsHighGround(x - 1, y) || grid.IsHighGround(x + 1, y) ||
            grid.IsHighGround(x, y - 1) || grid.IsHighGround(x, y + 1);

        /// <summary>
        /// Force a full-cell (Grid) collider on every painted Door-layer cell — the door trigger must
        /// fire regardless of the tile's own collider type (Anubis doors are plain floor tiles, which
        /// carry none). Re-applied after Restore too, because SetTilesBlock resets per-cell overrides.
        /// </summary>
        private void ForceDoorColliders(BoundsInt bounds)
        {
            if (doorTilemap == null) return;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var pos = new Vector3Int(x, y, bounds.zMin);
                    if (doorTilemap.HasTile(pos))
                        doorTilemap.SetColliderType(pos, Tile.ColliderType.Grid);
                }
        }

        /// <summary>
        /// Drop 1–2 waterfall columns (Halls) on the FrontOfEverything layer (above all others). EVERY
        /// fall leads INTO a room — none are stray: each pours down a column that lands on a 3×3
        /// <see cref="BiomeConfig.WaterfallBasePatch"/> placed fully INSIDE an igroom (off its walls), as
        /// far as possible from that room's stairs (see <see cref="PaintIgroomWaterfall"/> +
        /// <see cref="IgroomPatchTargets"/>). 1–2 per room, each into a DIFFERENT room (capped by how many
        /// rooms can host a patch). Use an AnimatedTile for the cycling look. Deterministic per room
        /// (seeded by VariantSeed); off unless assigned (Halls), and no falls if no room can host one.
        /// </summary>
        private void PaintWaterfalls(RoomGrid grid, BiomeConfig config)
        {
            if (frontOfEverythingTilemap == null || config.WaterfallTile == null) return;

            var rng = new System.Random(grid.VariantSeed);
            var targets = IgroomPatchTargets(grid); // (patch-centre column, patch-centre row) per hostable igroom
            if (targets.Count == 0) return;         // nothing to lead into → no waterfalls (never stray)

            rng.Shuffle(targets);
            int count = Mathf.Min(rng.Next(MinWaterfalls, MaxWaterfalls + 1), targets.Count);
            for (int i = 0; i < count; i++)
                PaintIgroomWaterfall(grid, config, targets[i].x, targets[i].y);
        }

        /// <summary>
        /// The patch-leading waterfall: pour down column <paramref name="cx"/> from the north edge and
        /// STOP just above the patch centre row <paramref name="pc"/>. Below the stop, stamp the
        /// <see cref="BiomeConfig.WaterfallEnderTile"/> on the Map layer at the next 2 tiles, then a 3×3
        /// <see cref="BiomeConfig.WaterfallBasePatch"/> on the Collision layer (Walls tilemap) centred on
        /// (cx, pc) — fully inside the igroom interior — so the player can't walk through the fall's base.
        /// </summary>
        private void PaintIgroomWaterfall(RoomGrid grid, BiomeConfig config, int cx, int pc)
        {
            int stopY = pc + 1; // last waterfall tile = the patch's top row
            for (int y = grid.Height - 1; y >= stopY; y--) // fall, stopping AT the stop tile
                frontOfEverythingTilemap.SetTile(new Vector3Int(originCell.x + cx, originCell.y + y, 0), config.WaterfallTile);

            if (mapTilemap != null && config.WaterfallEnderTile != null)
                for (int d = 1; d <= 2; d++) // the 2 tiles just below the stop (= the patch's lower rows)
                {
                    int y = stopY - d;
                    if (y < 0) break;
                    mapTilemap.SetTile(new Vector3Int(originCell.x + cx, originCell.y + y, 0), config.WaterfallEnderTile);
                }

            // 3×3 collision patch on the Collision layer, centred on (cx, pc) → bottom-left (cx-1, pc-1).
            // Tiles carry their own collider so this blocks the fall's base.
            var pad = config.WaterfallBasePatch;
            if (wallsTilemap != null && pad != null && pad.Length >= 9)
                StampGroup(wallsTilemap, new PatchPlacement(cx - 1, pc - 1, 0), 3, 3, pad);
        }

        /// <summary>
        /// One patch target per hostable igroom: the (column, row) CENTRE of a 3×3 patch placed fully
        /// inside the igroom interior (off its walls), as FAR as possible from that room's stairs, on a
        /// column a north-edge fall can actually reach (no other igroom above it). Reconstructs each
        /// igroom's rect from its matched corner walls (NW + nearest NE on the top row + nearest SW down
        /// the left column); igrooms with an interior smaller than 3×3 can't host a patch and are skipped.
        /// </summary>
        private static List<Vector2Int> IgroomPatchTargets(RoomGrid grid)
        {
            var nw = new List<Vector2Int>();
            var ne = new List<Vector2Int>();
            var sw = new List<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.TryGetWallKind(x, y, out var k))
                    {
                        if (k == WallKind.NorthWest) nw.Add(new Vector2Int(x, y));
                        else if (k == WallKind.NorthEast) ne.Add(new Vector2Int(x, y));
                        else if (k == WallKind.SouthWest) sw.Add(new Vector2Int(x, y));
                    }

            var targets = new List<Vector2Int>();
            foreach (var a in nw) // a = NW corner (x0, y1 = top)
            {
                int x0 = a.x, y1 = a.y;
                int x1 = int.MaxValue; // nearest NE on the same top row → right edge
                foreach (var b in ne)
                    if (b.y == y1 && b.x > x0 && b.x < x1) x1 = b.x;
                if (x1 == int.MaxValue) continue;

                int y0 = int.MinValue; // nearest SW down the left column → bottom row
                foreach (var c in sw)
                    if (c.x == x0 && c.y < y1 && c.y > y0) y0 = c.y;
                if (y0 == int.MinValue) continue;

                // A 3×3 patch centre must stay 1 cell inside the interior (interior = walls + 1), so it
                // never paints over a wall: centre column in [x0+2, x1-2], centre row in [y0+2, y1-2].
                int cxLo = x0 + 2, cxHi = x1 - 2, pcLo = y0 + 2, pcHi = y1 - 2;
                if (cxLo > cxHi || pcLo > pcHi) continue; // interior smaller than 3×3 → can't host a patch

                // Stairs reference: average the igroom's entrance cells (fall back to the SW corner).
                int sx = 0, sy = 0, n = 0;
                foreach (var (gx, gy, _, _) in grid.Entrances)
                    if (gx >= x0 && gx <= x1 && gy >= y0 && gy <= y1) { sx += gx; sy += gy; n++; }
                float refX = n > 0 ? sx / (float)n : x0;
                float refY = n > 0 ? sy / (float)n : y0;

                // Pick the valid patch centre FARTHEST from the stairs whose column the fall can reach.
                Vector2Int best = default;
                float bestD = -1f;
                for (int cx = cxLo; cx <= cxHi; cx++)
                {
                    if (!ColumnReachable(grid, cx, y1)) continue; // an igroom above would catch the fall first
                    for (int pc = pcLo; pc <= pcHi; pc++)
                    {
                        float d = Sq(cx - refX) + Sq(pc - refY);
                        if (d > bestD) { bestD = d; best = new Vector2Int(cx, pc); }
                    }
                }
                if (bestD >= 0f) targets.Add(best);
            }
            return targets;
        }

        /// <summary>True if a north-edge fall down column <paramref name="cx"/> reaches an igroom whose top
        /// wall is at <paramref name="topRow"/> — i.e. no other igroom (room-floor or recorded wall) sits
        /// above it on that column.</summary>
        private static bool ColumnReachable(RoomGrid grid, int cx, int topRow)
        {
            for (int y = topRow + 1; y < grid.Height; y++)
                if (grid.IsRoomFloor(cx, y) || grid.TryGetWallKind(cx, y, out _)) return false;
            return true;
        }

        /// <summary>
        /// Paint <see cref="DoorBackingDepth"/> solid wall cells directly behind (outward of) each
        /// door, so the open doorway doesn't expose the void. Rendered as the matching edge wall.
        /// </summary>
        private void PaintDoorBackingWalls(RoomGrid grid, BiomeConfig config)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid[x, y] != CellType.Door) continue;

                    var edge = NearestEdge(x, y, grid.Width, grid.Height);
                    var inward = InwardStep(edge);
                    var kind = EdgeWallKind(edge);
                    // Open (Anubis): the east/west/south boundary is water, not walls — back those
                    // doors with more water so the doorway reads as a crossing, not a wall gap.
                    bool waterBacked = config.Layout == BiomeLayout.Open && edge != Cardinal.North;
                    for (int d = 1; d <= DoorBackingDepth; d++)
                    {
                        int gx = x - inward.x * d, gy = y - inward.y * d; // step outward
                        var pos = new Vector3Int(originCell.x + gx, originCell.y + gy, 0);
                        int cellHash = RoomSeed.CellHash(grid.VariantSeed, gx, gy);
                        if (waterBacked)
                        {
                            wallsTilemap.SetTile(pos, config.WaterTileAt(cellHash));
                            continue;
                        }
                        wallsTilemap.SetTile(pos, config.WallTileFor(kind, cellHash));
                        if (TryGetWallRotation(kind, config, out var rotation))
                            wallsTilemap.SetTransformMatrix(pos, rotation);
                    }
                }
            }
        }

        /// <summary>
        /// Paint the two unique flank-wall tiles on the collision layer at the cells immediately LEFT
        /// (west) and RIGHT (east) of the NORTH room door — the door's frame, replacing the regular
        /// wall/border there. On when the tiles are assigned (Aqua's wall-ring flanks, or Halls' water
        /// border). The 4 doors are corner-padded so both flank cells are always inside the grid.
        /// </summary>
        private void PaintNorthDoorWalls(RoomGrid grid, BiomeConfig config)
        {
            if (wallsTilemap == null || config.NorthDoorLeftWallTile == null) return;

            int c = grid.DoorStarts[(int)Cardinal.North];     // door's first (west) column
            int row = grid.Height - config.WallThickness;     // the north door's row
            int left = c - 1, right = c + config.DoorWidth;   // first cell each side of the 2-wide door

            wallsTilemap.SetTile(new Vector3Int(originCell.x + left, originCell.y + row, 0),
                config.NorthDoorLeftWallTile);
            wallsTilemap.SetTile(new Vector3Int(originCell.x + right, originCell.y + row, 0),
                config.NorthDoorRightWallTile);
        }

        /// <summary>
        /// Paint a water collision tile on the UnseenCollision layer directly UNDER every igroom wall
        /// sprite (the wall's OWN cell) EXCEPT the STRAIGHT south wall (`WallKind.South`, which gets a
        /// floor tile beneath it instead) — the SW/SE corners still get the underlay. The underlay sits on the wall cell, NOT the
        /// cell below, so the interior row just inside the north wall stays walkable (placing it below
        /// would shrink the room by a row on its north edge). Halls only (igroom walls carry a recorded
        /// WallKind; the water collision routes to the UnseenCollision layer there).
        /// </summary>
        private void PaintIgroomWallUnderlay(RoomGrid grid, BiomeConfig config)
        {
            var collision = WaterCollisionTilemap(config);
            if (collision == null) return;

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid[x, y] != CellType.Wall) continue;           // actual igroom wall cells only
                    if (!grid.TryGetWallKind(x, y, out var k)) continue; // need the kind to skip south walls
                    if (k == WallKind.South) continue;                   // straight south walls only (corners keep it)
                    collision.SetTile(new Vector3Int(originCell.x + x, originCell.y + y, 0),
                        config.WaterTileAt(RoomSeed.CellHash(grid.VariantSeed, x, y)));
                }
        }

        private static WallKind EdgeWallKind(Cardinal edge) => edge switch
        {
            Cardinal.North => WallKind.North,
            Cardinal.South => WallKind.South,
            Cardinal.East => WallKind.East,
            _ => WallKind.West,
        };

        /// <summary>
        /// Stamp the UnderWall tile on the Extras front layer over every water cell sitting directly
        /// BENEATH a Building or Floor tile — the shadowed underside of the wall/floor edge. Off unless
        /// <see cref="BiomeConfig.UnderWallTile"/> is assigned.
        /// </summary>
        private void PaintUnderWalls(RoomGrid grid, BiomeConfig config)
        {
            if (extrasTilemap == null || config.UnderWallTile == null) return;

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid[x, y] != CellType.Water) continue;
                    var above = grid.Get(x, y + 1);
                    if (above == CellType.Building || above == CellType.Floor)
                        extrasTilemap.SetTile(new Vector3Int(originCell.x + x, originCell.y + y, 0), config.UnderWallTile);
                }
        }

        /// <summary>
        /// Scatter 2–3 floor decor columns (Halls) on the ExtrasFrontOfPlayer layer: a vertical tile
        /// strip (<see cref="BiomeConfig.FloorDecorColumn"/>, authored TOP→BOTTOM) stood on a FIELD floor
        /// cell (bottom on the floor, rising up). The bottom cell also gets a water tile on the collision
        /// layer (UnseenCollision in Halls), so the base BLOCKS movement. Valid spots are scanned
        /// exhaustively (the constraints make them sparse, so random sampling would often come up short),
        /// shuffled deterministically (VariantSeed), then placed greedily up to the target with spacing.
        /// Off unless the column is assigned (Halls).
        /// </summary>
        private List<Vector2Int> PaintFloorDecor(RoomGrid grid, BiomeConfig config)
        {
            var placed = new List<Vector2Int>();
            var col = config.FloorDecorColumn;
            if (extrasFrontOfPlayerTilemap == null || col == null || col.Length == 0) return placed;

            int h = col.Length;
            if (h > grid.Height) return placed;
            var rng = new System.Random(unchecked(grid.VariantSeed * 31 + 17)); // decoupled from the waterfall rng

            // The 4 room door landings — keep the (solid-based) decor clear of them so it can't block a door.
            var landings = new Vector2Int[4];
            for (int d = 0; d < 4; d++) landings[d] = RoomDoors.Landing(config, grid, (Cardinal)d);

            // Every valid bottom cell: field floor with FLOOR all around (its solid base can't pinch a
            // corridor), with room above for the column, clear of the doors.
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y <= grid.Height - h; y++)
                    if (IsFieldFloor(grid, x, y) && SurroundedByFloor(grid, x, y)
                        && !WithinChebyshev(landings, x, y, FloorDecorDoorClearance))
                        candidates.Add(new Vector2Int(x, y));
            rng.Shuffle(candidates);

            int target = rng.Next(MinFloorDecor, MaxFloorDecor + 1);
            var collision = WaterCollisionTilemap(config);

            foreach (var c in candidates)
            {
                if (placed.Count >= target) break;
                if (WithinChebyshev(placed, c.x, c.y, FloorDecorSpacing)) continue; // keep decors apart
                placed.Add(c);

                for (int t = 0; t < h; t++) // col[0] = top → highest cell, col[h-1] = bottom on the floor
                {
                    if (col[t] == null) continue;
                    extrasFrontOfPlayerTilemap.SetTile(
                        new Vector3Int(originCell.x + c.x, originCell.y + c.y + (h - 1 - t), 0), col[t]);
                }

                if (collision != null) // base blocks movement: a water collision tile under the bottom cell
                    collision.SetTile(new Vector3Int(originCell.x + c.x, originCell.y + c.y, 0),
                        config.WaterTileAt(RoomSeed.CellHash(grid.VariantSeed, c.x, c.y)));
            }
            return placed;
        }

        /// <summary>
        /// Place 2–3 3×3 cube patches (<see cref="BiomeConfig.CubePatch"/>) split across two layers —
        /// never all on one (2 → 1 behind + 1 front, 3 → 2/1). Behind cubes (ExtrasBehind) anchor their
        /// bottom-middle on an igroom SOUTH corner (the one farthest from that room's stairs); front
        /// cubes (ExtrasFrontOfPlayer) sit with their bottom edge on the room's south edge (y = 0),
        /// never on an igroom or a door. Cubes only need to not OVERLAP each other or the floor decor
        /// columns — they may sit right next to one another. <see cref="MinCubeCount"/> is guaranteed:
        /// if the split placement comes up short, a fallback fills from ANY igroom south corner.
        /// Deterministic (VariantSeed). Off unless the cube patch is assigned (Halls).
        /// </summary>
        private void PaintCubePatches(RoomGrid grid, BiomeConfig config, List<Vector2Int> floorDecor)
        {
            var tiles = config.CubePatch;
            if (tiles == null || tiles.Length < CubeSize * CubeSize) return;
            if (extrasBehindTilemap == null && extrasFrontOfPlayerTilemap == null) return;

            var rng = new System.Random(unchecked(grid.VariantSeed * 131 + 7)); // decoupled from the other passes

            // 2–3 cubes total, split across the two layers but NEVER all on one: 2 → 1 behind + 1 front,
            // 3 → 2/1 (randomly which layer gets the pair).
            int total = rng.Next(MinCubeCount, MaxCubeCount + 1);
            int behindTarget = total == 3 ? (rng.Next(0, 2) == 0 ? 2 : 1) : 1;
            int frontTarget = total - behindTarget;

            // Cubes must not OVERLAP each other or the floor decor columns (close/adjacent is fine).
            var occupied = new HashSet<Vector2Int>(floorDecor);
            int placed = 0;

            // Mode 1 (ExtrasBehind): bottom-middle on the igroom south corner farthest from its stairs.
            int behindPlaced = 0;
            if (extrasBehindTilemap != null && behindTarget > 0)
            {
                var corners = BehindCubeCorners(grid);
                rng.Shuffle(corners);
                foreach (var c in corners)
                {
                    if (behindPlaced >= behindTarget) break;
                    if (CubeHitsDoor(grid, c.x - 1, c.y)) continue;          // never on a door
                    if (TryPlaceCube(extrasBehindTilemap, c.x - 1, c.y, grid, tiles, occupied))
                        behindPlaced++;
                }
            }

            // Mode 2 (ExtrasFrontOfPlayer): bottom edge on the south edge; absorbs any unplaced behind cubes.
            int frontGoal = frontTarget + (behindTarget - behindPlaced);
            if (extrasFrontOfPlayerTilemap != null && frontGoal > 0)
            {
                var xs = new List<int>();
                for (int x = 0; x <= grid.Width - CubeSize; x++) xs.Add(x);
                rng.Shuffle(xs);
                int frontPlaced = 0;
                foreach (int x in xs)
                {
                    if (frontPlaced >= frontGoal) break;
                    if (CubeHitsDoor(grid, x, 0) || CubeHitsIgroom(grid, x, 0)) continue; // never on a door/igroom
                    if (TryPlaceCube(extrasFrontOfPlayerTilemap, x, 0, grid, tiles, occupied))
                        frontPlaced++;
                }
                placed = behindPlaced + frontPlaced;
            }
            else
            {
                placed = behindPlaced;
            }

            // Guarantee at least MinCubeCount: if we came up short (e.g. the south edge was all
            // igrooms/doors), fill the rest from ANY igroom south corner on the behind layer.
            if (placed < MinCubeCount && extrasBehindTilemap != null)
            {
                var corners = AllSouthCorners(grid);
                rng.Shuffle(corners);
                foreach (var c in corners)
                {
                    if (placed >= MinCubeCount) break;
                    if (CubeHitsDoor(grid, c.x - 1, c.y)) continue;
                    if (TryPlaceCube(extrasBehindTilemap, c.x - 1, c.y, grid, tiles, occupied))
                        placed++;
                }
            }
        }

        /// <summary>Stamp a 3×3 cube at bottom-left (bx,by) on <paramref name="map"/> if it fits in the
        /// grid and none of its footprint cells are already occupied (so cubes never overlap each other
        /// or the floor decor); marks the footprint occupied and returns true on success.</summary>
        private bool TryPlaceCube(Tilemap map, int bx, int by, RoomGrid grid, TileBase[] tiles,
                                  HashSet<Vector2Int> occupied)
        {
            if (bx < 0 || by < 0 || bx + CubeSize > grid.Width || by + CubeSize > grid.Height) return false;
            for (int dx = 0; dx < CubeSize; dx++)
                for (int dy = 0; dy < CubeSize; dy++)
                    if (occupied.Contains(new Vector2Int(bx + dx, by + dy))) return false; // would overlap

            StampGroup(map, new PatchPlacement(bx, by, 0), CubeSize, CubeSize, tiles);
            for (int dx = 0; dx < CubeSize; dx++)
                for (int dy = 0; dy < CubeSize; dy++)
                    occupied.Add(new Vector2Int(bx + dx, by + dy));
            return true;
        }

        /// <summary>Every igroom SOUTH corner (SW/SE), any row — the broad candidate pool for the
        /// minimum-count fallback.</summary>
        private static List<Vector2Int> AllSouthCorners(RoomGrid grid)
        {
            var corners = new List<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.TryGetWallKind(x, y, out var k)
                        && (k == WallKind.SouthWest || k == WallKind.SouthEast))
                        corners.Add(new Vector2Int(x, y));
            return corners;
        }

        /// <summary>
        /// One behind-cube anchor per igroom: the SOUTH corner (SW or SE) FARTHEST from that igroom's
        /// stairs (its doorway). Reconstructs each igroom's rect from its corner wall kinds (SW + nearest
        /// SE on the same bottom row + nearest NW up the same left column), then averages the entrance
        /// cells that fall inside it for the stairs position and keeps the farther of the two south
        /// corners. Only igrooms whose south corner is off the bottom <see cref="CubeBehindMinRow"/> rows
        /// qualify (so behind cubes stay away from the front ones at the south edge, with most of the map
        /// still available). If a room has no recorded stairs, its SW corner.
        /// </summary>
        private static List<Vector2Int> BehindCubeCorners(RoomGrid grid)
        {
            var sw = new List<Vector2Int>();
            var se = new List<Vector2Int>();
            var nw = new List<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.TryGetWallKind(x, y, out var k))
                    {
                        if (k == WallKind.SouthWest) sw.Add(new Vector2Int(x, y));
                        else if (k == WallKind.SouthEast) se.Add(new Vector2Int(x, y));
                        else if (k == WallKind.NorthWest) nw.Add(new Vector2Int(x, y));
                    }

            var result = new List<Vector2Int>();
            foreach (var a in sw) // a = SW corner (x0, y0 = bottom)
            {
                if (a.y < CubeBehindMinRow) continue; // keep the south corner off the bottom rows (away from front cubes)

                int x1 = int.MaxValue; // SE corner: nearest to the right on the same bottom row
                foreach (var b in se) if (b.y == a.y && b.x > a.x && b.x < x1) x1 = b.x;
                if (x1 == int.MaxValue) continue;

                int y1 = int.MaxValue; // NW corner: nearest above on the same left column → top row
                foreach (var c in nw) if (c.x == a.x && c.y > a.y && c.y < y1) y1 = c.y;
                if (y1 == int.MaxValue) continue;

                var swCorner = a;
                var seCorner = new Vector2Int(x1, a.y);

                // Stairs centroid: average the entrance cells inside this igroom's rect.
                int sx = 0, sy = 0, n = 0;
                foreach (var (gx, gy, _, _) in grid.Entrances)
                    if (gx >= a.x && gx <= x1 && gy >= a.y && gy <= y1) { sx += gx; sy += gy; n++; }

                if (n == 0) { result.Add(swCorner); continue; }
                float cx = sx / (float)n, cy = sy / (float)n;
                float dSW = Sq(swCorner.x - cx) + Sq(swCorner.y - cy);
                float dSE = Sq(seCorner.x - cx) + Sq(seCorner.y - cy);
                result.Add(dSE > dSW ? seCorner : swCorner);
            }
            return result;
        }

        private static float Sq(float v) => v * v;

        /// <summary>True if any cell of the 3×3 cube footprint (bottom-left bx,by) is a Door cell.</summary>
        private static bool CubeHitsDoor(RoomGrid grid, int bx, int by)
        {
            for (int dx = 0; dx < CubeSize; dx++)
                for (int dy = 0; dy < CubeSize; dy++)
                    if (grid.Get(bx + dx, by + dy) == CellType.Door) return true;
            return false;
        }

        /// <summary>True if any cell of the footprint belongs to an igroom — its interior room-floor or
        /// its recorded wall border. Keeps the FRONT cubes off igrooms entirely.</summary>
        private static bool CubeHitsIgroom(RoomGrid grid, int bx, int by)
        {
            for (int dx = 0; dx < CubeSize; dx++)
                for (int dy = 0; dy < CubeSize; dy++)
                {
                    int x = bx + dx, y = by + dy;
                    if (grid.IsRoomFloor(x, y) || grid.TryGetWallKind(x, y, out _)) return true;
                }
            return false;
        }

        /// <summary>True if (x, y) is within <paramref name="radius"/> cells (Chebyshev) of any of the
        /// given points.</summary>
        private static bool WithinChebyshev(IReadOnlyList<Vector2Int> points, int x, int y, int radius)
        {
            for (int i = 0; i < points.Count; i++)
                if (Mathf.Abs(x - points[i].x) <= radius && Mathf.Abs(y - points[i].y) <= radius) return true;
            return false;
        }

        /// <summary>True if the cell and all 8 neighbours are Floor — a fully open spot, so making the
        /// cell solid can't pinch a 1-wide corridor.</summary>
        private static bool SurroundedByFloor(RoomGrid grid, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (grid.Get(x + dx, y + dy) != CellType.Floor) return false;
            return true;
        }

        /// <summary>Stamp the 2×3 water decor groups over open water (ExtrasFrontOfPlayer — in front
        /// of the player).</summary>
        private void PaintWaterDecor(RoomGrid grid, BiomeConfig config)
        {
            foreach (var placement in grid.WaterDecor)
            {
                var decor = At(config.WaterDecorPatches, placement.PatchIndex);
                if (decor != null)
                    StampGroup(extrasFrontOfPlayerTilemap, placement, WaterDecorPatch.Width, WaterDecorPatch.Height, decor.Tiles);
            }
        }

        /// <summary>
        /// Fill the ExtrasFullBehind layer (whole width) with fixed horizontal bands from beneath the
        /// north edge down to the south: one Transition1 row directly beneath the north edge, one
        /// Transition2 row, then Blank fills the rest to the bottom. The top north-edge row keeps its
        /// water backdrop (this starts one cell below it). Off unless <see cref="BiomeConfig.Transition1Tile"/>
        /// is assigned (so it only affects Halls). At WATER cells the band tile goes on the collision
        /// layer instead (water would otherwise hide the backmost tile) — it replaces the water tile and
        /// carries that cell's collision, so the band tiles used over water need a collider type.
        /// </summary>
        private void PaintExtrasBehindBands(RoomGrid grid, BiomeConfig config)
        {
            if (extrasFullBehindTilemap == null || config.Transition1Tile == null) return;

            int total = grid.Height - config.WallThickness; // banded rows: y in [0, total)
            if (total <= 0) return;

            var collision = WaterCollisionTilemap(config); // Halls water collision → UnseenCollision

            int topY = grid.Height - 1 - config.WallThickness; // first row beneath the north edge
            for (int depth = 0; depth < total; depth++)
            {
                int y = topY - depth;
                for (int x = 0; x < grid.Width; x++)
                {
                    var tile = BandTile(config, grid, x, y, depth);
                    if (tile == null) continue;
                    var pos = new Vector3Int(originCell.x + x, originCell.y + y, 0);
                    if (grid[x, y] == CellType.Water && collision != null)
                        collision.SetTile(pos, tile); // over water → collision layer (keeps it solid)
                    else
                        extrasFullBehindTilemap.SetTile(pos, tile);
                }
            }
        }

        private static TileBase BandTile(BiomeConfig config, RoomGrid grid, int x, int y, int depth)
        {
            if (depth == 0) // Transition1, directly beneath the north edge
                return BandEnd(config.Transition1Tile, config.Transition1LeftEndTile, config.Transition1RightEndTile, grid, x, y);
            if (depth == 1) // Transition2
                return config.Transition2Tile;
            return config.BlankBehindTile; // fills the rest to the bottom
        }

        /// <summary>The band tile, swapped for its left/right end-cap when the cell meets a FIELD floor
        /// tile horizontally — floor to the WEST → left end, floor to the EAST → right end. Only the
        /// room's field/corridor floor counts; an igroom's INTERIOR floor (room-floor) is ignored, so a
        /// band next to a room interior isn't mistaken for the field edge.</summary>
        private static TileBase BandEnd(TileBase baseTile, TileBase leftEnd, TileBase rightEnd,
                                        RoomGrid grid, int x, int y)
        {
            if (leftEnd != null && IsFieldFloor(grid, x - 1, y)) return leftEnd;
            if (rightEnd != null && IsFieldFloor(grid, x + 1, y)) return rightEnd;
            return baseTile;
        }

        /// <summary>True for the room's walkable field/corridor floor — Floor that is NOT an igroom's
        /// interior (room-floor) and NOT an igroom entrance cell (those are Floor but carry stairs, so
        /// the band caps shouldn't treat them as the field edge).</summary>
        private static bool IsFieldFloor(RoomGrid grid, int x, int y) =>
            grid.Get(x, y) == CellType.Floor && !grid.IsRoomFloor(x, y) && !grid.IsEntrance(x, y);

        /// <summary>Safe array lookup — never trust a placement index against the live config.</summary>
        private static T At<T>(T[] array, int index) where T : class =>
            array != null && index >= 0 && index < array.Length ? array[index] : null;

        /// <summary>
        /// Stamp one authored tile group at a placement. <paramref name="tiles"/> is row-major
        /// with the TOP row first (as authored), so rows are flipped onto the y-up grid.
        /// </summary>
        private void StampGroup(Tilemap map, PatchPlacement placement, int width, int height, TileBase[] tiles)
        {
            if (map == null || tiles == null) return;

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int i = row * width + col;
                    if (i >= tiles.Length || tiles[i] == null) continue;
                    var pos = new Vector3Int(
                        originCell.x + placement.X + col,
                        originCell.y + placement.Y + (height - 1 - row), 0);
                    map.SetTile(pos, tiles[i]);
                }
            }
        }

        /// <summary>
        /// Building run cells: ends get the cap tiles, everything between the middle tile.
        /// Exception: an end whose tile ABOVE is right next to a door (i.e. the end sits directly
        /// below the door-flank wall) gets the door-frame wall so the building continues the frame —
        /// the north-door flank tiles MIRRORED when assigned (left end → door's RIGHT flank, right end
        /// → door's LEFT flank; Aqua), else the East/West wall (East left of the door, West right).
        /// </summary>
        private static TileBase BuildingTile(RoomGrid grid, int x, int y, BiomeConfig config)
        {
            bool leftEnd = grid.Get(x - 1, y) != CellType.Building;
            bool rightEnd = grid.Get(x + 1, y) != CellType.Building;

            if (rightEnd && grid.Get(x + 1, y + 1) == CellType.Door)
                return config.NorthDoorLeftWallTile != null ? config.NorthDoorLeftWallTile : config.EastWallTile;
            if (leftEnd && grid.Get(x - 1, y + 1) == CellType.Door)
                return config.NorthDoorRightWallTile != null ? config.NorthDoorRightWallTile : config.WestWallTile;

            if (leftEnd) return config.BuildingLeftTile;
            if (rightEnd) return config.BuildingRightTile;
            return config.BuildingMiddleTile;
        }

        /// <summary>
        /// Stamp the 2-wide stairs (left + right halves) on the 2 doorway cells of each igroom entrance
        /// (Map layer, in the wall line), rotated to the direction the entrance opens. Tiles are
        /// authored facing NORTH: a South-opening entrance keeps them (base faces north, back toward
        /// the igroom), West → rotate right, East → rotate left, North → 180°. The halves swap cells
        /// with the rotation — HallPass already tagged each cell with which half it holds.
        /// </summary>
        private void PaintStairs(RoomGrid grid, BiomeConfig config)
        {
            if (mapTilemap == null) return;

            foreach (var (gx, gy, facing, left) in grid.Entrances)
            {
                var tile = left ? config.LeftStairTile : config.RightStairTile;
                if (tile == null) continue;
                var pos = new Vector3Int(originCell.x + gx, originCell.y + gy, 0);
                mapTilemap.SetTile(pos, tile);
                mapTilemap.SetTransformMatrix(pos, EntranceRotation(facing));
            }
        }

        /// <summary>
        /// Stamp the 2-wide igroom door (left + right halves) on the same 2 doorway cells as the stairs,
        /// on the Extras front layer (above the floor/stairs) and rotated to the entrance direction —
        /// exactly like <see cref="PaintStairs"/>. Decorative only (front layer, no collider).
        /// </summary>
        private void PaintDoors(RoomGrid grid, BiomeConfig config)
        {
            if (extrasTilemap == null) return;

            foreach (var (gx, gy, facing, left) in grid.Entrances)
            {
                var tile = left ? config.LeftDoorTile : config.RightDoorTile;
                if (tile == null) continue;
                var pos = new Vector3Int(originCell.x + gx, originCell.y + gy, 0);
                extrasTilemap.SetTile(pos, tile);
                extrasTilemap.SetTransformMatrix(pos, EntranceRotation(facing));
            }
        }

        /// <summary>Rotation for a 2-wide entrance decoration authored facing NORTH: a South-opening
        /// entrance keeps it (base faces north, back toward the igroom), West → rotate right, East →
        /// rotate left, North → 180°. Shared by the stairs and the door.</summary>
        private static Matrix4x4 EntranceRotation(Cardinal facing) => facing switch
        {
            Cardinal.North => Rot180,    // entrance opens north → faces south
            Cardinal.East => RotLeft90,  // opens east  → faces west
            Cardinal.West => RotRight90, // opens west  → faces east
            _ => Matrix4x4.identity,     // opens south → faces north (authored default)
        };

        /// <summary>Stamp the decorative floor patch groups over the base floor (Map layer).</summary>
        private void PaintFloorPatches(RoomGrid grid, BiomeConfig config)
        {
            foreach (var placement in grid.FloorPatches)
            {
                var patch = At(config.FloorPatches, placement.PatchIndex);
                if (patch != null)
                    StampGroup(mapTilemap, placement, patch.Size, patch.Size, patch.Tiles);
            }
        }

        /// <summary>
        /// Rotation for south-side wall cells that borrow a north tile — only when the biome has
        /// <see cref="BiomeConfig.RotateSouthWalls"/> on (else the south tiles are assigned explicitly
        /// and need no rotation), and the borrowed slot actually has tiles.
        /// </summary>
        /// <summary>The straight south edge gets floor painted beneath it (so the sprite's base shows
        /// ground). NOT the SW/SE corners — under an igroom those corner tiles sit over water and a
        /// floor patch beneath them just pokes out.</summary>
        private static bool IsSouthFacing(WallKind kind) => kind == WallKind.South;

        private static bool TryGetWallRotation(WallKind kind, BiomeConfig config, out Matrix4x4 rotation)
        {
            rotation = Matrix4x4.identity;
            if (!config.RotateSouthWalls) return false;

            switch (kind)
            {
                case WallKind.South when config.HasNorthWallTile:
                    rotation = Rot180;
                    return true;
                case WallKind.SouthWest when config.NorthWestCornerTile != null:
                    rotation = RotLeft90;
                    return true;
                case WallKind.SouthEast when config.NorthEastCornerTile != null:
                    rotation = RotRight90;
                    return true;
                default:
                    return false;
            }
        }

        public void Clear()
        {
            if (wallsTilemap) wallsTilemap.ClearAllTiles();
            if (unseenCollisionTilemap) unseenCollisionTilemap.ClearAllTiles();
            if (mapTilemap) mapTilemap.ClearAllTiles();
            if (doorTilemap) doorTilemap.ClearAllTiles();
            if (extrasTilemap) extrasTilemap.ClearAllTiles();
            if (extrasBehindTilemap) extrasBehindTilemap.ClearAllTiles();
            if (extrasFullBehindTilemap) extrasFullBehindTilemap.ClearAllTiles();
            if (extrasFrontOfPlayerTilemap) extrasFrontOfPlayerTilemap.ClearAllTiles();
            if (frontOfEverythingTilemap) frontOfEverythingTilemap.ClearAllTiles();
        }

        /// <summary>Capture the room's current tiles + per-cell transforms (all layers) for later
        /// restore. The bounds are padded by <see cref="DoorBackingDepth"/> so the door-backing
        /// walls (painted just outside the room) are captured/restored too.</summary>
        public RoomSnapshot Capture(int width, int height)
        {
            int pad = DoorBackingDepth;
            var bounds = new BoundsInt(
                originCell - new Vector3Int(pad, pad, 0),
                new Vector3Int(width + 2 * pad, height + 2 * pad, 1));
            return new RoomSnapshot
            {
                Bounds = bounds,
                Walls = CaptureLayer(wallsTilemap, bounds),
                UnseenCollision = CaptureLayer(unseenCollisionTilemap, bounds),
                Map = CaptureLayer(mapTilemap, bounds),
                Door = CaptureLayer(doorTilemap, bounds),
                Extras = CaptureLayer(extrasTilemap, bounds),
                ExtrasBehind = CaptureLayer(extrasBehindTilemap, bounds),
                ExtrasFullBehind = CaptureLayer(extrasFullBehindTilemap, bounds),
                ExtrasFrontOfPlayer = CaptureLayer(extrasFrontOfPlayerTilemap, bounds),
                FrontOfEverything = CaptureLayer(frontOfEverythingTilemap, bounds),
            };
        }

        /// <summary>Repaint a previously captured snapshot, replacing whatever is currently painted.</summary>
        public void Restore(RoomSnapshot snap)
        {
            Clear();
            RestoreLayer(wallsTilemap, snap.Bounds, snap.Walls);
            RestoreLayer(unseenCollisionTilemap, snap.Bounds, snap.UnseenCollision);
            RestoreLayer(mapTilemap, snap.Bounds, snap.Map);
            RestoreLayer(doorTilemap, snap.Bounds, snap.Door);
            RestoreLayer(extrasTilemap, snap.Bounds, snap.Extras);
            RestoreLayer(extrasBehindTilemap, snap.Bounds, snap.ExtrasBehind);
            RestoreLayer(extrasFullBehindTilemap, snap.Bounds, snap.ExtrasFullBehind);
            RestoreLayer(extrasFrontOfPlayerTilemap, snap.Bounds, snap.ExtrasFrontOfPlayer);
            RestoreLayer(frontOfEverythingTilemap, snap.Bounds, snap.FrontOfEverything);
            ForceDoorColliders(snap.Bounds); // SetTilesBlock reset the per-cell collider overrides
        }

        private static LayerSnapshot CaptureLayer(Tilemap tilemap, BoundsInt bounds)
        {
            if (tilemap == null) return default;

            var tiles = tilemap.GetTilesBlock(bounds);
            var transforms = new Matrix4x4[tiles.Length];
            int i = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    transforms[i++] = tilemap.GetTransformMatrix(new Vector3Int(x, y, bounds.zMin));

            return new LayerSnapshot { Tiles = tiles, Transforms = transforms };
        }

        private static void RestoreLayer(Tilemap tilemap, BoundsInt bounds, LayerSnapshot layer)
        {
            if (tilemap == null || layer.Tiles == null) return;

            tilemap.SetTilesBlock(bounds, layer.Tiles); // this resets per-cell transforms to identity
            if (layer.Transforms == null) return;

            int i = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var m = layer.Transforms[i++];
                    if (m != Matrix4x4.identity) // only re-apply rotated/scaled cells
                        tilemap.SetTransformMatrix(new Vector3Int(x, y, bounds.zMin), m);
                }
        }

        /// <summary>
        /// Find the door currently painted on the given edge and return a world position
        /// <paramref name="inset"/> cells in FRONT of it (toward the room interior). Wherever the
        /// door sits on that edge, the spawn follows it — so doors don't have to be at fixed cells.
        /// Returns false if there's no door tile on that edge.
        /// </summary>
        public bool TryGetDoorSpawn(Cardinal edge, int width, int height, int inset, out Vector3 world)
        {
            world = default;
            if (doorTilemap == null) return false;

            int sumX = 0, sumY = 0, count = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (NearestEdge(x, y, width, height) != edge) continue;
                    if (doorTilemap.HasTile(new Vector3Int(originCell.x + x, originCell.y + y, 0)))
                    {
                        sumX += x; sumY += y; count++;
                    }
                }
            }
            if (count == 0) return false;

            var inward = InwardStep(edge);
            int cx = Mathf.RoundToInt(sumX / (float)count) + inward.x * inset;
            int cy = Mathf.RoundToInt(sumY / (float)count) + inward.y * inset;
            world = CellCenterWorld(cx, cy);
            return true;
        }

        /// <summary>
        /// Which room edge a world position is nearest. Used to classify the door the player is
        /// standing on — doors can be anywhere along an edge, so position-vs-room-centre would
        /// misclassify off-centre doors; the door cell itself is always nearest its own edge.
        /// </summary>
        public Cardinal NearestEdgeWorld(Vector3 world, int width, int height)
        {
            var cell = mapTilemap.WorldToCell(world);
            return NearestEdge(cell.x - originCell.x, cell.y - originCell.y, width, height);
        }

        /// <summary>Which edge a cell is closest to (doors live near one edge, so this is unambiguous).</summary>
        private static Cardinal NearestEdge(int x, int y, int w, int h)
        {
            int left = x, right = w - 1 - x, bottom = y, top = h - 1 - y;
            int min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
            if (min == bottom) return Cardinal.South;
            if (min == top) return Cardinal.North;
            if (min == left) return Cardinal.West;
            return Cardinal.East;
        }

        /// <summary>Step from a door toward the room interior.</summary>
        private static Vector2Int InwardStep(Cardinal edge) => edge switch
        {
            Cardinal.South => new Vector2Int(0, 1),
            Cardinal.North => new Vector2Int(0, -1),
            Cardinal.West => new Vector2Int(1, 0),
            _ => new Vector2Int(-1, 0),
        };
    }
}
