using System.Collections.Generic;

namespace TRV
{
    /// <summary>
    /// Shared helpers for the room rng. Every pass that needs random ordering uses these, so the
    /// Fisher–Yates implementation (and its rng draw pattern, which determinism depends on) lives
    /// in exactly one place.
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>In-place Fisher–Yates shuffle (works on arrays and lists).</summary>
        public static void Shuffle<T>(this System.Random rng, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>The indices 0..count-1 in random order.</summary>
        public static int[] ShuffledIndices(this System.Random rng, int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            rng.Shuffle(order);
            return order;
        }
    }
}
