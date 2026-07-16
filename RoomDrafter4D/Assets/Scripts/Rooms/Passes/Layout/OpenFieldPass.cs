namespace TRV
{
    /// <summary>
    /// Anubis-style layout: the whole interior is one open walkable floor field — no corridors, no
    /// inner rooms (yet; Anubis structure passes come later). Only the NORTH wall band survives from
    /// BorderPass; the east/west/south border ring becomes WATER (solid, painted from the biome's
    /// water variants — the field just ends at the shoreline, no wall sprites). DoorPass still cuts
    /// all 4 doors into the ring. Downstream passes are naturally inert here: ConnectivityPass finds
    /// one region with nothing to carve, the 1-wide ring is too thin for islands/water decor, and
    /// BuildingPass is gated to Corridors (it would otherwise eat the whole north row).
    /// </summary>
    public class OpenFieldPass : IRoomPass
    {
        public void Apply(RoomGrid grid, System.Random rng, BiomeConfig config)
        {
            int t = config.WallThickness;

            // Interior: one solid walkable field.
            for (int x = t; x < grid.Width - t; x++)
                for (int y = t; y < grid.Height - t; y++)
                    grid[x, y] = CellType.Floor;

            // East/west/south border ring: water instead of BorderPass's walls. The north band
            // (y >= Height - t, top corners included) keeps its walls.
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height - t; y++)
                    if (x < t || x >= grid.Width - t || y < t)
                        grid[x, y] = CellType.Water;
        }
    }
}
