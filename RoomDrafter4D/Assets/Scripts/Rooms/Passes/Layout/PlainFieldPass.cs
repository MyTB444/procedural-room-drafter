namespace TRV
{
    /// <summary>
    /// Lands-style layout: the plainest room — BorderPass's full wall ring on all 4 sides with the
    /// whole interior one walkable floor field. No water, no inner structure yet (Lands' floor
    /// detailing comes later). Downstream passes are naturally inert here: ConnectivityPass finds
    /// one region with nothing to carve, there's no water for islands/water decor, BuildingPass is
    /// gated to Corridors and HighGroundPass to Open.
    /// </summary>
    public class PlainFieldPass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;
            for (int x = t; x < grid.Width - t; x++)
                for (int y = t; y < grid.Height - t; y++)
                    grid[x, y] = CellType.Floor;
        }
    }
}
