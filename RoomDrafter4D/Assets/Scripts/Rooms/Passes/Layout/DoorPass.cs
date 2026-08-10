namespace TRV
{
    /// <summary>
    /// Places the 4 doorways as openings in the wall ring — each door takes the INNER-most wall
    /// cell of its edge, so the layout reads <c>map → door → wall → edge</c> with
    /// <c>WallThickness-1</c> wall(s) behind it. A floor landing is carved inward (the actual
    /// door-lock-until-cleared lives in RoomManager/DoorPortal).
    /// Door positions are rolled per room edge (anywhere along it, corner-padded) by
    /// RoomGenerator and read from <see cref="RoomGrid.DoorStarts"/>; the entry spawn finds the
    /// door by scanning the painted Door layer, so rooms don't need matching door positions.
    /// </summary>
    public class DoorPass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int w = grid.Width, h = grid.Height;
            int dw = config.DoorWidth;
            int depth = config.LandingDepth;
            int d0 = config.WallThickness - 1; // door distance from the edge → keeps t-1 walls behind it

            int north = grid.DoorStarts[(int)Cardinal.North];
            int south = grid.DoorStarts[(int)Cardinal.South];
            int east = grid.DoorStarts[(int)Cardinal.East];
            int west = grid.DoorStarts[(int)Cardinal.West];

            for (int i = 0; i < dw; i++)
            {
                // Bottom & Top.
                grid[south + i, d0] = CellType.Door;
                grid[north + i, h - 1 - d0] = CellType.Door;
                for (int d = 1; d <= depth; d++)
                {
                    grid[south + i, d0 + d] = CellType.Floor;
                    grid[north + i, h - 1 - d0 - d] = CellType.Floor;
                }

                // Left & Right.
                grid[d0, west + i] = CellType.Door;
                grid[w - 1 - d0, east + i] = CellType.Door;
                for (int d = 1; d <= depth; d++)
                {
                    grid[d0 + d, west + i] = CellType.Floor;
                    grid[w - 1 - d0 - d, east + i] = CellType.Floor;
                }
            }
        }
    }
}
