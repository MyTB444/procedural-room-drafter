namespace TRV
{
    /// <summary>
    /// Where a Wall cell sits in the outer wall ring — drives which wall tile the painter stamps.
    /// South is intentionally absent as a tile slot (it reuses the North tile rotated 180°),
    /// but it still exists here so the painter knows when to apply that rotation.
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

            return Classify(x, y, grid.Width, grid.Height, thickness);
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
