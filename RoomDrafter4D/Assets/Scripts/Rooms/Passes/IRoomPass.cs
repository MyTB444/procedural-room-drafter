namespace TRV
{
    /// <summary>
    /// One step of room generation. Passes run in order, each mutating the same grid.
    /// New biomes = different ordered lists of passes.
    /// </summary>
    public interface IRoomPass
    {
        void Apply(RoomGrid grid, System.Random rng, BiomeConfig config);
    }
}
