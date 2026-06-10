using UnityEngine;

namespace TRV
{
    /// <summary>
    /// The 8 facing directions for a top-down character, ordered counter-clockwise
    /// starting at East. The integer value equals the "octant" index (0..7), which
    /// keeps the conversion math below trivial.
    /// </summary>
    public enum TRVDirection
    {
        East = 0,
        NorthEast = 1,
        North = 2,
        NorthWest = 3,
        West = 4,
        SouthWest = 5,
        South = 6,
        SouthEast = 7,
    }

    /// <summary>
    /// Pure, stateless helpers for converting between movement vectors and the 8
    /// discrete directions. Kept separate so movement, animation, aiming, etc. can
    /// all share one source of truth for "which way is the character facing".
    /// </summary>
    public static class TRVDirectionUtil
    {
        private const float Diagonal = 0.70710677f; // 1 / sqrt(2)

        // Unit vector for each direction, indexed by (int)TRVDirection.
        private static readonly Vector2[] Vectors =
        {
            new Vector2( 1f,         0f),        // East
            new Vector2( Diagonal,   Diagonal),  // NorthEast
            new Vector2( 0f,         1f),        // North
            new Vector2(-Diagonal,   Diagonal),  // NorthWest
            new Vector2(-1f,         0f),        // West
            new Vector2(-Diagonal,  -Diagonal),  // SouthWest
            new Vector2( 0f,        -1f),        // South
            new Vector2( Diagonal,  -Diagonal),  // SouthEast
        };

        /// <summary>Nearest of the 8 directions to an arbitrary vector. Returns South for ~zero input.</summary>
        public static TRVDirection FromVector(Vector2 v)
        {
            if (v.sqrMagnitude < 0.0001f)
                return TRVDirection.South;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // East = 0, CCW positive
            int octant = Mathf.RoundToInt(angle / 45f);
            octant = ((octant % 8) + 8) % 8; // wrap into 0..7
            return (TRVDirection)octant;
        }

        /// <summary>Unit vector pointing along a direction.</summary>
        public static Vector2 ToVector(TRVDirection direction) => Vectors[(int)direction];

        /// <summary>Snap an arbitrary input vector onto the nearest 8-way unit vector (magnitude 1).</summary>
        public static Vector2 Snap(Vector2 v) => ToVector(FromVector(v));

        /// <summary>True for the four diagonal directions (NE, NW, SW, SE). Their enum values are odd.</summary>
        public static bool IsDiagonal(TRVDirection d) => ((int)d & 1) == 1;

        /// <summary>True if the two directions are neighbours on the compass (45° apart).</summary>
        public static bool AreAdjacent(TRVDirection a, TRVDirection b)
        {
            int diff = Mathf.Abs((int)a - (int)b) % 8;
            return diff == 1 || diff == 7;
        }
    }
}
