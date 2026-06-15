using System.Collections.Generic;

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

        /// <summary>Island decor placements (1×1 or 1×2) — X/Y = bottom cell, PatchIndex into
        /// <see cref="BiomeConfig.IslandDecorPatches"/> (filled by IslandDecorPass).</summary>
        public List<PatchPlacement> IslandDecor { get; } = new List<PatchPlacement>();

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
