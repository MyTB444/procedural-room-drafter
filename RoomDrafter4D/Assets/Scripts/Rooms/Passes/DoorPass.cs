namespace TRV
{
    /// <summary>
    /// Places the 4 doorways as openings in the wall ring — the door takes the INNER-most wall cell
    /// (centre of each edge), so the layout reads <c>map → door → wall → edge</c>: the door is the
    /// last visible tile before the outer wall, with <c>WallThickness-1</c> wall(s) behind it (1 by
    /// default) sealing the exit until the room is cleared (future). A floor landing is carved inward.
    /// Door positions are fixed, so the east door of one room lines up with the west door of the next.
    /// </summary>
    public class DoorPass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int w = grid.Width, h = grid.Height;
            int t = config.WallThickness;
            int dw = config.DoorWidth;
            int depth = config.LandingDepth;
            int d0 = t - 1;              // door distance from the edge → keeps t-1 walls behind it
            int colStart = (w - dw) / 2; // top/bottom doors
            int rowStart = (h - dw) / 2; // left/right doors

            for (int i = 0; i < dw; i++)
            {
                // Bottom & Top.
                int x = colStart + i;
                grid[x, d0] = CellType.Door;
                grid[x, h - 1 - d0] = CellType.Door;
                for (int d = 1; d <= depth; d++)
                {
                    grid[x, d0 + d] = CellType.Floor;
                    grid[x, h - 1 - d0 - d] = CellType.Floor;
                }

                // Left & Right.
                int y = rowStart + i;
                grid[d0, y] = CellType.Door;
                grid[w - 1 - d0, y] = CellType.Door;
                for (int d = 1; d <= depth; d++)
                {
                    grid[d0 + d, y] = CellType.Floor;
                    grid[w - 1 - d0 - d, y] = CellType.Floor;
                }
            }
        }
    }
}
