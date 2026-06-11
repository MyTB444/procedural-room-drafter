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
