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
        [SerializeField] private Tilemap mapTilemap;
        [SerializeField] private Tilemap doorTilemap;
        [Tooltip("Front decor layer (your 'ExtrasFront') — gets the 2×3 water decor groups, " +
                 "rendered above the water.")]
        [SerializeField] private Tilemap extrasTilemap;
        [Tooltip("ExtrasBehind — renders under the Walls layer. Gets a water tile beneath every wall " +
                 "cell so wall sprites with transparency show water behind them.")]
        [SerializeField] private Tilemap extrasBehindTilemap;

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

        /// <summary>True if a grid cell holds a solid tile (Wall/Water/Building all live on the
        /// Walls/Collision layer) — i.e. NOT walkable. Used to build the nav grid for pathfinding.</summary>
        public bool HasSolidAt(int gridX, int gridY)
        {
            if (wallsTilemap == null) return false;
            return wallsTilemap.HasTile(new Vector3Int(originCell.x + gridX, originCell.y + gridY, 0));
        }

        public void Paint(RoomGrid grid, BiomeConfig config)
        {
            Clear();

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var pos = new Vector3Int(originCell.x + x, originCell.y + y, 0);
                    int cellHash = RoomSeed.CellHash(grid.VariantSeed, x, y);
                    switch (grid[x, y])
                    {
                        case CellType.Wall:
                        {
                            var kind = grid.TryGetWallKind(x, y, out var k)
                                ? k
                                : WallKindUtil.Classify(grid, x, y, config.WallThickness);
                            wallsTilemap.SetTile(pos, config.WallTileFor(kind, cellHash));
                            if (TryGetWallRotation(kind, config, out var rotation))
                                wallsTilemap.SetTransformMatrix(pos, rotation);
                            // South-facing walls sit on floor (the sprite's base shows ground, not void).
                            // Only for biomes with explicit south tiles (Halls); when RotateSouthWalls
                            // is on (Aqua) the south side is north tiles over the water backdrop instead.
                            if (IsSouthFacing(kind) && !config.RotateSouthWalls)
                                mapTilemap.SetTile(pos, config.FloorTileAt(cellHash));
                            if (extrasBehindTilemap) // water backdrop under the wall sprite
                                extrasBehindTilemap.SetTile(pos, config.WaterTileAt(cellHash));
                            break;
                        }
                        case CellType.Water:
                            wallsTilemap.SetTile(pos, config.WaterTileAt(cellHash));
                            break;
                        case CellType.Floor:
                            mapTilemap.SetTile(pos, config.FloorTileAt(cellHash));
                            break;
                        case CellType.Door:
                            mapTilemap.SetTile(pos, config.FloorTileAt(cellHash)); // walkable floor under the door
                            doorTilemap.SetTile(pos, config.DoorTile);
                            break;
                        case CellType.Building:
                            // Corridor buildings sit on floor; water-following extensions stand in
                            // water (their underlay shows through any sprite transparency).
                            if (grid.Get(x, y - 1) == CellType.Water)
                            {
                                if (extrasBehindTilemap)
                                    extrasBehindTilemap.SetTile(pos, config.WaterTileAt(cellHash));
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

            PaintFloorPatches(grid, config);
            PaintWaterDecor(grid, config);
            PaintDoorBackingWalls(grid, config);
            // Island decor is no longer painted — it's spawned as pooled GameObjects by
            // IslandDecorPool (driven by RoomManager from grid.IslandDecor).
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
                    for (int d = 1; d <= DoorBackingDepth; d++)
                    {
                        int gx = x - inward.x * d, gy = y - inward.y * d; // step outward
                        var pos = new Vector3Int(originCell.x + gx, originCell.y + gy, 0);
                        int cellHash = RoomSeed.CellHash(grid.VariantSeed, gx, gy);
                        wallsTilemap.SetTile(pos, config.WallTileFor(kind, cellHash));
                        if (TryGetWallRotation(kind, config, out var rotation))
                            wallsTilemap.SetTransformMatrix(pos, rotation);
                    }
                }
            }
        }

        private static WallKind EdgeWallKind(Cardinal edge) => edge switch
        {
            Cardinal.North => WallKind.North,
            Cardinal.South => WallKind.South,
            Cardinal.East => WallKind.East,
            _ => WallKind.West,
        };

        /// <summary>Stamp the 2×3 water decor groups over open water (Extras front layer).</summary>
        private void PaintWaterDecor(RoomGrid grid, BiomeConfig config)
        {
            foreach (var placement in grid.WaterDecor)
            {
                var decor = At(config.WaterDecorPatches, placement.PatchIndex);
                if (decor != null)
                    StampGroup(extrasTilemap, placement, WaterDecorPatch.Width, WaterDecorPatch.Height, decor.Tiles);
            }
        }

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
        /// below the door-flank wall) copies that flank's tile — East left of the door, West right
        /// of it — so the building visually continues the door frame.
        /// </summary>
        private static TileBase BuildingTile(RoomGrid grid, int x, int y, BiomeConfig config)
        {
            bool leftEnd = grid.Get(x - 1, y) != CellType.Building;
            bool rightEnd = grid.Get(x + 1, y) != CellType.Building;

            if (rightEnd && grid.Get(x + 1, y + 1) == CellType.Door) return config.EastWallTile;
            if (leftEnd && grid.Get(x - 1, y + 1) == CellType.Door) return config.WestWallTile;

            if (leftEnd) return config.BuildingLeftTile;
            if (rightEnd) return config.BuildingRightTile;
            return config.BuildingMiddleTile;
        }

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
            if (mapTilemap) mapTilemap.ClearAllTiles();
            if (doorTilemap) doorTilemap.ClearAllTiles();
            if (extrasTilemap) extrasTilemap.ClearAllTiles();
            if (extrasBehindTilemap) extrasBehindTilemap.ClearAllTiles();
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
                Map = CaptureLayer(mapTilemap, bounds),
                Door = CaptureLayer(doorTilemap, bounds),
                Extras = CaptureLayer(extrasTilemap, bounds),
                ExtrasBehind = CaptureLayer(extrasBehindTilemap, bounds),
            };
        }

        /// <summary>Repaint a previously captured snapshot, replacing whatever is currently painted.</summary>
        public void Restore(RoomSnapshot snap)
        {
            Clear();
            RestoreLayer(wallsTilemap, snap.Bounds, snap.Walls);
            RestoreLayer(mapTilemap, snap.Bounds, snap.Map);
            RestoreLayer(doorTilemap, snap.Bounds, snap.Door);
            RestoreLayer(extrasTilemap, snap.Bounds, snap.Extras);
            RestoreLayer(extrasBehindTilemap, snap.Bounds, snap.ExtrasBehind);
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
