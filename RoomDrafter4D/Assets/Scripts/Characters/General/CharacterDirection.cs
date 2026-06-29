using UnityEngine;

namespace TRV
{
    /// <summary>
    /// The 8 facing directions for a top-down character, ordered counter-clockwise
    /// starting at East. The integer value equals the "octant" index (0..7), which
    /// keeps the conversion math below trivial.
    /// </summary>
    public enum CharacterDirection
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
    public static class CharacterDirectionUtil
    {
        private const float Diagonal = 0.70710677f; // 1 / sqrt(2)

        // Unit vector for each direction, indexed by (int)CharacterDirection.
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
        public static CharacterDirection FromVector(Vector2 v)
        {
            if (v.sqrMagnitude < 0.0001f)
                return CharacterDirection.South;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // East = 0, CCW positive
            int octant = Mathf.RoundToInt(angle / 45f);
            octant = ((octant % 8) + 8) % 8; // wrap into 0..7
            return (CharacterDirection)octant;
        }

        /// <summary>Unit vector pointing along a direction.</summary>
        public static Vector2 ToVector(CharacterDirection direction) => Vectors[(int)direction];

        /// <summary>Snap an arbitrary input vector onto the nearest 8-way unit vector (magnitude 1).</summary>
        public static Vector2 Snap(Vector2 v) => ToVector(FromVector(v));

        /// <summary>Snap a facing vector to the 4-way Horizontal/Vertical animation params, with diagonals
        /// resolving to HORIZONTAL (|x| ≥ |y| → ±1 on horizontal else ±1 on vertical; one is ±1, other 0).
        /// ~Zero input → both 0. Shared by the enemy animators and the directional collider.</summary>
        public static void ToFourWay(Vector2 facing, out float horizontal, out float vertical)
        {
            horizontal = 0f;
            vertical = 0f;
            if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y))
            {
                if (Mathf.Abs(facing.x) > 0.0001f) horizontal = Mathf.Sign(facing.x);
            }
            else
            {
                vertical = Mathf.Sign(facing.y);
            }
        }

        /// <summary>True for the four diagonal directions (NE, NW, SW, SE). Their enum values are odd.</summary>
        public static bool IsDiagonal(CharacterDirection d) => ((int)d & 1) == 1;

        /// <summary>True if the two directions are neighbours on the compass (45° apart).</summary>
        public static bool AreAdjacent(CharacterDirection a, CharacterDirection b)
        {
            int diff = Mathf.Abs((int)a - (int)b) % 8;
            return diff == 1 || diff == 7;
        }
    }
}
