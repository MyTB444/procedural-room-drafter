using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Pure-data 2D grid of <see cref="CellType"/> — a room before it's painted to Tilemaps.
    /// No Unity tile dependency, so the generation logic stays testable in isolation.
    /// </summary>
    public class RoomGrid
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>Decorative floor patches (filled by FloorDecorPass, drawn by TilemapPainter).</summary>
        public List<PatchPlacement> FloorPatches { get; } = new List<PatchPlacement>();

        /// <summary>2×3 water decor placements — X/Y = bottom-left cell, PatchIndex into
        /// <see cref="BiomeConfig.WaterDecorPatches"/> (filled by WaterDecorPass).</summary>
        public List<PatchPlacement> WaterDecor { get; } = new List<PatchPlacement>();

        /// <summary>Bottom-left cell of every 4×4 island carved by IslandPass.</summary>
        public List<(int x, int y)> IslandAnchors { get; } = new List<(int x, int y)>();

        /// <summary>The end of every south-running high-ground corridor (Open/Anubis) — x = the
        /// corridor's centre column, y = its end (bottom) row. Recorded by HighGroundPass; the
        /// painter hangs the end patch beneath these.</summary>
        public List<(int x, int y)> HighGroundEnds { get; } = new List<(int x, int y)>();

        /// <summary>Bottom-left cell of every 3×3 STAIRS patch (Open/Anubis) linking the high-ground
        /// band to the field beneath — one per separated regular-floor area, placed at the band seam.
        /// Recorded by HighGroundPass; the painter stamps rails (left/right columns) on Collision2
        /// and the walkable steps (middle column) on Map.</summary>
        public List<(int x, int y)> StairPatches { get; } = new List<(int x, int y)>();

        /// <summary>Interior rects of the SMALL Halls igrooms (excludes the big main room) — where
        /// IslandDecorPass scatters decor objects (same field/count as islands). Filled by HallPass.</summary>
        public List<RectInt> IgroomDecorAreas { get; } = new List<RectInt>();

        /// <summary>Interior rect of the BIG main Halls igroom (width 0 = none) — RoomManager fills it with
        /// the rolled main-room content (an upgrade, or crates/vases). Filled by HallPass.</summary>
        public RectInt MainIgroomArea { get; set; }

        /// <summary>Decor placements for the BIG main igroom (same count/type as small igrooms, but kept
        /// clear of the upgrade's centre cell) — recorded by IslandDecorPass, but only USED by RoomManager
        /// when the room's main content is an UPGRADE (a filler room fills the interior itself instead).</summary>
        public List<PatchPlacement> MainIgroomDecor { get; } = new List<PatchPlacement>();

        /// <summary>Island decor object placements — X/Y = cell, PatchIndex into
        /// <see cref="BiomeConfig.IslandDecorObjects"/> (filled by IslandDecorPass, spawned by
        /// <see cref="IslandDecorPool"/>).</summary>
        public List<PatchPlacement> IslandDecor { get; } = new List<PatchPlacement>();

        /// <summary>The 2 doorway cells of each 2-wide igroom entrance — X/Y = cell, Facing = the
        /// direction the entrance opens (sets rotation), Left = whether this cell holds the LEFT half
        /// (else the right). Filled by <see cref="HallPass"/>; the painter stamps the 2-wide stairs
        /// (Map layer) and door halves (Extras front) on them, both rotated to Facing.</summary>
        public List<(int x, int y, Cardinal facing, bool left)> Entrances { get; } =
            new List<(int x, int y, Cardinal facing, bool left)>();

        /// <summary>True if (x, y) is an igroom entrance (doorway) cell — those are <see cref="CellType.Floor"/>
        /// but carry stairs/door decor, so callers that key off "real" floor (e.g. the ExtrasBehind band
        /// end-caps) must skip them.</summary>
        public bool IsEntrance(int x, int y)
        {
            foreach (var e in Entrances)
                if (e.x == x && e.y == y) return true;
            return false;
        }

        /// <summary>
        /// Per-room seed for paint-time per-cell tile variants (set by RoomGenerator from the room
        /// rng, hashed with cell coords via <see cref="RoomSeed.CellHash"/>).
        /// </summary>
        public int VariantSeed { get; set; }

        /// <summary>
        /// Where each door band starts, indexed by <see cref="Cardinal"/>: the first COLUMN of the
        /// door for North/South, the first ROW for East/West. Rolled once per room by
        /// RoomGenerator (anywhere along the edge, corner-padded) and shared by every pass.
        /// </summary>
        public int[] DoorStarts { get; } = new int[4];

        private readonly CellType[,] _cells;

        /// <summary>
        /// Optional per-cell wall-tile kind, set by a pass that KNOWS the wall's orientation
        /// (e.g. <see cref="HallPass"/> igroom borders), so the painter doesn't have to guess it from
        /// neighbours — guessing breaks when a path runs alongside a wall (floor on two sides). Sparse:
        /// only the overridden cells; everything else falls back to <see cref="WallKindUtil"/>.
        /// </summary>
        private Dictionary<(int x, int y), WallKind> _wallKinds;

        public void SetWallKind(int x, int y, WallKind kind)
        {
            _wallKinds ??= new Dictionary<(int x, int y), WallKind>();
            _wallKinds[(x, y)] = kind;
        }

        public bool TryGetWallKind(int x, int y, out WallKind kind)
        {
            kind = WallKind.Fill;
            return _wallKinds != null && _wallKinds.TryGetValue((x, y), out kind);
        }

        /// <summary>
        /// Cells that are the INTERIOR floor of a room-within-a-room (a Halls igroom), so the painter
        /// can pave them with the distinct room-floor tiles instead of the corridor floor. Sparse:
        /// recorded by <see cref="HallPass"/>; everything else paints as normal floor.
        /// </summary>
        private HashSet<(int x, int y)> _roomFloors;

        public void MarkRoomFloor(int x, int y)
        {
            _roomFloors ??= new HashSet<(int x, int y)>();
            _roomFloors.Add((x, y));
        }

        public bool IsRoomFloor(int x, int y) => _roomFloors != null && _roomFloors.Contains((x, y));

        /// <summary>
        /// Cells flagged as "high ground" (Anubis/Open): still ordinary walkable Floor, but the
        /// painter paves them with the biome's high-ground tile pool instead of the base floor.
        /// Sparse: recorded by <see cref="HighGroundPass"/>; everything else paints as base floor.
        /// </summary>
        private HashSet<(int x, int y)> _highGround;

        public void MarkHighGround(int x, int y)
        {
            _highGround ??= new HashSet<(int x, int y)>();
            _highGround.Add((x, y));
        }

        public bool IsHighGround(int x, int y) => _highGround != null && _highGround.Contains((x, y));

        /// <summary>
        /// Cells on the walking PATH (Lands/Plain): still ordinary walkable Floor, but the painter
        /// paves them with the biome's path tile pool instead of the base floor. Sparse: recorded by
        /// <see cref="PathPass"/>; everything else paints as base floor.
        /// </summary>
        private HashSet<(int x, int y)> _path;

        public void MarkPath(int x, int y)
        {
            _path ??= new HashSet<(int x, int y)>();
            _path.Add((x, y));
        }

        public bool IsPath(int x, int y) => _path != null && _path.Contains((x, y));

        /// <summary>Wipe every path flag — used by <see cref="GrassPass"/>'s last resort, which
        /// reroutes the paths when they leave no room for a grass patch.</summary>
        public void ClearPaths() => _path?.Clear();

        /// <summary>
        /// Grass gradient levels (Lands/Plain): 4 = full grass, 3 = half, 2 = very few, 1 = the
        /// no-grass ring closing the gradient; unrecorded = the plain floor. Sparse: recorded by
        /// <see cref="GrassPass"/> (overlapping patches keep the HIGHEST level); the painter picks
        /// the matching grass pool.
        /// </summary>
        private Dictionary<(int x, int y), int> _grass;

        public void MarkGrass(int x, int y, int level)
        {
            _grass ??= new Dictionary<(int x, int y), int>();
            if (!_grass.TryGetValue((x, y), out int current) || level > current)
                _grass[(x, y)] = level;
        }

        public int GrassLevelAt(int x, int y) =>
            _grass != null && _grass.TryGetValue((x, y), out int level) ? level : 0;

        /// <summary>Wipe every grass level — used by <see cref="GrassPass"/>'s full reshape, which
        /// re-reserves all patches from scratch before rerouting the paths around them.</summary>
        public void ClearGrass() => _grass?.Clear();

        public RoomGrid(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new CellType[width, height];
        }

        public CellType this[int x, int y]
        {
            get => _cells[x, y];
            set => _cells[x, y] = value;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Read a cell, treating out-of-bounds as solid Wall (handy for neighbour counting).</summary>
        public CellType Get(int x, int y) => InBounds(x, y) ? _cells[x, y] : CellType.Wall;

        /// <summary>True for cells at least <paramref name="margin"/> cells in from every edge.</summary>
        public bool IsInterior(int x, int y, int margin) =>
            x >= margin && y >= margin && x < Width - margin && y < Height - margin;

        public void Fill(CellType type)
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    _cells[x, y] = type;
        }
    }
}
