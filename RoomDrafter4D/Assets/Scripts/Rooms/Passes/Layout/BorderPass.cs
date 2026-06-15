namespace TRV
{
    /// <summary>Frames the room: the outer <c>WallThickness</c> ring of cells becomes solid Wall.</summary>
    public class BorderPass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (x < t || y < t || x >= grid.Width - t || y >= grid.Height - t)
                        grid[x, y] = CellType.Wall;
        }
    }
}
