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
        [SerializeField] private Tilemap extrasTilemap;

        [Header("Placement")]
        [Tooltip("Cell coordinate the grid's (0,0) maps to, so rooms paint onto your existing footprint. " +
                 "For the current scene that's the bottom-left of your room, e.g. (-13, -8, 0).")]
        [SerializeField] private Vector3Int originCell = Vector3Int.zero;

        /// <summary>World position of the centre of a grid cell — for placing the player on a landing.</summary>
        public Vector3 CellCenterWorld(int gridX, int gridY)
        {
            var cell = new Vector3Int(originCell.x + gridX, originCell.y + gridY, 0);
            return mapTilemap != null ? mapTilemap.GetCellCenterWorld(cell) : (Vector3)cell;
        }

        public void Paint(RoomGrid grid, BiomeConfig config)
        {
            Clear();

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var pos = new Vector3Int(originCell.x + x, originCell.y + y, 0);
                    switch (grid[x, y])
                    {
                        case CellType.Wall:
                            wallsTilemap.SetTile(pos, config.WallTile);
                            break;
                        case CellType.Water:
                            wallsTilemap.SetTile(pos, config.WaterTile);
                            break;
                        case CellType.Floor:
                            mapTilemap.SetTile(pos, config.FloorTile);
                            break;
                        case CellType.Door:
                            mapTilemap.SetTile(pos, config.FloorTile); // walkable floor under the door
                            doorTilemap.SetTile(pos, config.DoorTile);
                            break;
                    }
                }
            }
        }

        public void Clear()
        {
            if (wallsTilemap) wallsTilemap.ClearAllTiles();
            if (mapTilemap) mapTilemap.ClearAllTiles();
            if (doorTilemap) doorTilemap.ClearAllTiles();
            if (extrasTilemap) extrasTilemap.ClearAllTiles();
        }

        /// <summary>Capture the room's current tiles + per-cell transforms (all 4 layers) for later restore.</summary>
        public RoomSnapshot Capture(int width, int height)
        {
            var bounds = new BoundsInt(originCell, new Vector3Int(width, height, 1));
            return new RoomSnapshot
            {
                Bounds = bounds,
                Walls = CaptureLayer(wallsTilemap, bounds),
                Map = CaptureLayer(mapTilemap, bounds),
                Door = CaptureLayer(doorTilemap, bounds),
                Extras = CaptureLayer(extrasTilemap, bounds),
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
