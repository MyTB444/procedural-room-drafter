namespace TRV
{
    /// <summary>
    /// "Buildings": wherever a horizontal run of at least <see cref="MinRunLength"/> floor cells
    /// touches the north wall band, the wall thickens one row inward — that floor row becomes
    /// solid <see cref="CellType.Building"/> (painted left cap / middle / right cap). Each
    /// converted column also grows one floor cell below the bottom of its floor run, so the
    /// corridor underneath keeps its size. A run end that borders WATER follows the water along
    /// the north wall, but only when that stretch dead-ends at the room edge — water leading back
    /// to floor stays open; followed cells stand in water, so nothing below changes.
    /// Cells under the north DOOR are never converted (the mouth stays open), which naturally
    /// breaks runs around it.
    /// </summary>
    public class BuildingPass : IRoomPass
    {
        private const int MinRunLength = 3;

        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;
            int y = grid.Height - t - 1; // the interior row touching the north wall band

            int x = t;
            while (x < grid.Width - t)
            {
                if (!Eligible(grid, x, y)) { x++; continue; }

                int start = x;
                while (x < grid.Width - t && Eligible(grid, x, y)) x++;
                if (x - start < MinRunLength) continue;

                for (int cx = start; cx < x; cx++)
                {
                    bool onFloor = grid[cx, y] == CellType.Floor;
                    grid[cx, y] = CellType.Building;
                    if (onFloor) // water-gap cells stand in water — no corridor cell was consumed
                        ExtendFloorBelow(grid, cx, y, t);
                }

                // A run end facing open water follows the water — but only when that stretch
                // dead-ends at the room edge; water leading back to floor stays open. Wherever
                // the building stops, it also claims a buffer cell under the door-flank wall so
                // it runs right up to the door frame (the painter caps it with the flank tile).
                TryClaimDoorBuffer(grid, FollowWaterToEdge(grid, start - 1, -1, y, t), y, t);
                TryClaimDoorBuffer(grid, FollowWaterToEdge(grid, x, +1, y, t), y, t);
            }
        }

        /// <summary>
        /// A cell the building row can claim, directly under a solid stretch of north wall:
        /// either floor touching the band, or water with floor right below it (e.g. an island
        /// separated from the north wall by a single water tile — the building bridges that gap,
        /// standing in the water). Excluded: under the Door itself (the mouth must stay open) and
        /// under the walls right NEXT to the door (the door's flanking frame stays clear).
        /// </summary>
        private static bool Eligible(RoomGrid grid, int x, int y) =>
            (grid[x, y] == CellType.Floor ||
             (grid[x, y] == CellType.Water && grid.Get(x, y - 1) == CellType.Floor)) &&
            grid.Get(x, y + 1) == CellType.Wall &&
            grid.Get(x - 1, y + 1) != CellType.Door &&
            grid.Get(x + 1, y + 1) != CellType.Door;

        /// <summary>
        /// Walk the water stretch beside a run end (stepping by <paramref name="step"/>) and
        /// convert it to Building ONLY if it dead-ends at the room edge (the ring, checked by
        /// position so interior framing walls don't count). Water leading back to floor stays
        /// open — instead the single water tile the building stops against becomes a framing
        /// Wall, rendered East/West via the Building-adjacency rule in WallKindUtil.
        /// Returns the first cell beyond the building on that side, for the door-buffer claim.
        /// </summary>
        private static int FollowWaterToEdge(RoomGrid grid, int from, int step, int y, int margin)
        {
            int wx = from;
            while (grid.Get(wx, y) == CellType.Water) wx += step;

            bool reachedEdge = wx < margin || wx >= grid.Width - margin;
            if (!reachedEdge)
            {
                if (grid.Get(from, y) == CellType.Water) // close the end with a framing wall
                    grid[from, y] = CellType.Wall;
                return from;
            }

            for (int cx = from; cx != wx; cx += step)
                grid[cx, y] = CellType.Building;
            return wx;
        }

        /// <summary>
        /// A floor cell directly below the door-flank wall (the buffer column floor runs exclude)
        /// joins the building when it stops there, so the building meets the door frame. Consumes
        /// a floor cell → the corridor below is compensated like any other building column. Mouth
        /// columns (Door above) never qualify, so the doorway itself stays open.
        /// </summary>
        private static void TryClaimDoorBuffer(RoomGrid grid, int x, int y, int margin)
        {
            if (grid.Get(x, y) != CellType.Floor) return;
            if (grid.Get(x, y + 1) != CellType.Wall) return;
            if (grid.Get(x - 1, y + 1) != CellType.Door && grid.Get(x + 1, y + 1) != CellType.Door) return;

            grid[x, y] = CellType.Building;
            ExtendFloorBelow(grid, x, y, margin);
        }

        /// <summary>
        /// Walk down the column's floor run and turn the first water cell below it into floor —
        /// the corridor keeps the same number of floor cells it had before the building took one.
        /// </summary>
        private static void ExtendFloorBelow(RoomGrid grid, int x, int yTop, int margin)
        {
            int y = yTop - 1;
            while (y >= margin && grid[x, y] == CellType.Floor) y--;
            if (y >= margin && grid[x, y] == CellType.Water)
                grid[x, y] = CellType.Floor;
        }
    }
}
