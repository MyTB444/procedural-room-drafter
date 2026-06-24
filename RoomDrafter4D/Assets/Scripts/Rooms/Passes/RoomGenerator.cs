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
                LayoutPass(config.Layout),
                new DoorPass(),
                new ConnectivityPass(),
                new DeadEndPrunePass(), // Halls: drop stray 1-tile dead-end corridors (connect nothing)
                new IslandPass(),       // after connectivity: all floor is connected, any hook-in works
                new BuildingPass(),     // north-wall extensions over finished floor (islands included)
                // new LandmarkPass(),  // added once landmark stamps exist
                new WaterDecorPass(),   // after buildings: water layout is final by now
                new IslandDecorPass(),  // after buildings: only decorates cells still island floor
                new FloorDecorPass(),   // last: respects patches pre-placed by IslandPass
            };
        }

        /// <summary>The interior-shaping pass for a biome's layout (runs after BorderPass).</summary>
        private static IRoomPass LayoutPass(BiomeLayout layout) => layout switch
        {
            BiomeLayout.Halls => new HallPass(),
            _ => new CorridorPass(),
        };

        public RoomGrid Generate(System.Random rng)
        {
            var grid = new RoomGrid(_config.Width, _config.Height);
            grid.VariantSeed = rng.Next(); // for paint-time per-cell tile variants

            // Roll each door's position along its edge (corner-padded) up front — every pass that
            // needs door geometry (DoorPass, CorridorPass nodes, ConnectivityPass) reads these.
            grid.DoorStarts[(int)Cardinal.North] = RollDoorStart(rng, _config.Width, _config);
            grid.DoorStarts[(int)Cardinal.South] = RollDoorStart(rng, _config.Width, _config);
            grid.DoorStarts[(int)Cardinal.East] = RollDoorStart(rng, _config.Height, _config);
            grid.DoorStarts[(int)Cardinal.West] = RollDoorStart(rng, _config.Height, _config);

            foreach (var pass in _passes)
                pass.Apply(grid, rng, _config);
            return grid;
        }

        /// <summary>Random start of a door band along an edge of the given length, keeping
        /// <see cref="BiomeConfig.DoorCornerPadding"/> cells clear of both corners. Falls back to
        /// centred when the edge is too short for the padding.</summary>
        private static int RollDoorStart(System.Random rng, int edgeLength, BiomeConfig config)
        {
            int min = config.WallThickness + config.DoorCornerPadding;
            int max = edgeLength - config.WallThickness - config.DoorCornerPadding - config.DoorWidth;
            return max < min ? (edgeLength - config.DoorWidth) / 2 : rng.Next(min, max + 1);
        }
    }
}
