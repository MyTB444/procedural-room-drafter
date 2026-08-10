using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Deterministic per-room seeding: the same (worldSeed, coord) always produces the same room,
    /// so rooms are reproducible and you can return to an identical room without storing it.
    /// </summary>
    public static class RoomSeed
    {
        private static int For(int worldSeed, Vector2Int coord)
        {
            unchecked
            {
                int h = worldSeed;
                h = (h * 73856093) ^ (coord.x * 19349663);
                h = (h * 83492791) ^ (coord.y * 50331653);
                return h;
            }
        }

        public static System.Random Rng(int worldSeed, Vector2Int coord) =>
            new System.Random(For(worldSeed, coord));

        /// <summary>
        /// Deterministic non-negative hash for one cell of one room — used to pick per-cell tile
        /// variants at paint time without storing anything (same seed + cell → same variant).
        /// </summary>
        public static int CellHash(int seed, int x, int y)
        {
            unchecked
            {
                int h = seed;
                h = (h * 73856093) ^ (x * 19349663);
                h = (h * 83492791) ^ (y * 50331653);
                return h & 0x7FFFFFFF;
            }
        }
    }
}
