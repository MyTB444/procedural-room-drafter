namespace TRV
{
    /// <summary>
    /// Which wall tile the painter stamps for a Wall cell. Used both for the outer ring and for
    /// interior walls (Halls igrooms). The biome's <see cref="BiomeConfig.RotateSouthWalls"/> decides
    /// whether South/SW/SE get their own tiles or reuse the north ones rotated.
    /// </summary>
    public enum WallKind
    {
        Fill = 0,   // not in the outer ring — shouldn't occur (all walls live in the ring)
        North,
        South,
        East,
        West,
        NorthWest,
        NorthEast,
        SouthWest,
        SouthEast,
    }

    public static class WallKindUtil
    {
        /// <summary>
        /// Grid-aware classification. Walls flanking a door face the opening: the tile direction
        /// points TOWARD the door — left of a north door the door is to the east, so it gets the
        /// East wall tile; right of it gets West; below a side door gets North; above it gets
        /// South (the painter renders that as the North tile rotated 180°). Everything else falls
        /// back to positional classification.
        /// </summary>
        public static WallKind Classify(RoomGrid grid, int x, int y, int thickness)
        {
            if (grid.Get(x + 1, y) == CellType.Door) return WallKind.East;
            if (grid.Get(x - 1, y) == CellType.Door) return WallKind.West;
            if (grid.Get(x, y + 1) == CellType.Door) return WallKind.North;
            if (grid.Get(x, y - 1) == CellType.Door) return WallKind.South;

            // Framing wall closing a building run's end (BuildingPass places it on the water tile
            // the run stops against): right end → East wall, left end → West wall.
            if (grid.Get(x - 1, y) == CellType.Building) return WallKind.East;
            if (grid.Get(x + 1, y) == CellType.Building) return WallKind.West;

            var positional = Classify(x, y, grid.Width, grid.Height, thickness);
            if (positional != WallKind.Fill) return positional;

            // Interior wall (not on the room ring) — e.g. a Halls igroom wall. Face the adjacent
            // floor, the same relationship the room ring has to its interior, so it reuses the same
            // directional tiles: floor below → north wall, floor right → west wall, etc.
            return ClassifyByAdjacentFloor(grid, x, y);
        }

        private static WallKind ClassifyByAdjacentFloor(RoomGrid grid, int x, int y)
        {
            if (grid.Get(x, y - 1) == CellType.Floor) return WallKind.North; // floor below → top wall
            if (grid.Get(x, y + 1) == CellType.Floor) return WallKind.South; // floor above → bottom wall
            if (grid.Get(x + 1, y) == CellType.Floor) return WallKind.West;  // floor right → left wall
            if (grid.Get(x - 1, y) == CellType.Floor) return WallKind.East;  // floor left → right wall

            // No orthogonal floor → corner: the floor sits diagonally inward.
            if (grid.Get(x + 1, y - 1) == CellType.Floor) return WallKind.NorthWest; // floor SE
            if (grid.Get(x - 1, y - 1) == CellType.Floor) return WallKind.NorthEast; // floor SW
            if (grid.Get(x + 1, y + 1) == CellType.Floor) return WallKind.SouthWest; // floor NE
            if (grid.Get(x - 1, y + 1) == CellType.Floor) return WallKind.SouthEast; // floor NW
            return WallKind.Fill;
        }

        /// <summary>
        /// Classify a cell of the outer wall ring by position. Corners are the
        /// <paramref name="thickness"/>×<paramref name="thickness"/> blocks where two edge bands
        /// overlap; the rest of each band is its edge. Cells inside the ring return Fill.
        /// </summary>
        public static WallKind Classify(int x, int y, int width, int height, int thickness)
        {
            bool west = x < thickness;
            bool east = x >= width - thickness;
            bool south = y < thickness;
            bool north = y >= height - thickness;

            if (north && west) return WallKind.NorthWest;
            if (north && east) return WallKind.NorthEast;
            if (south && west) return WallKind.SouthWest;
            if (south && east) return WallKind.SouthEast;
            if (north) return WallKind.North;
            if (south) return WallKind.South;
            if (west) return WallKind.West;
            if (east) return WallKind.East;
            return WallKind.Fill;
        }
    }
}
