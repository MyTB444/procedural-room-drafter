using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Runs an ordered list of <see cref="IRoomPass"/> over a fresh grid to build one room.
    /// Edit the pass list to define new biomes; later add LandmarkPass / DecorPass here.
    /// </summary>
    public class RoomGenerator
    {
        private readonly BiomeConfig _config;
        private readonly IReadOnlyList<IRoomPass> _passes;

        public RoomGenerator(BiomeConfig config)
        {
            _config = config;
            _passes = new IRoomPass[]
            {
                new BorderPass(),
                new CavePass(),
                new DoorPass(),
                new ConnectivityPass(),
                // new LandmarkPass(),  // added once landmark stamps exist
                // new DecorPass(),
            };
        }

        public RoomGrid Generate(System.Random rng)
        {
            var grid = new RoomGrid(_config.Width, _config.Height);
            foreach (var pass in _passes)
                pass.Apply(grid, rng, _config);
            return grid;
        }
    }
}
