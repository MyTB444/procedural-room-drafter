using UnityEngine;

namespace TRV
{
    /// <summary>A compass direction — which edge a door is on, and which way a neighbour room lies.</summary>
    public enum Cardinal { North, East, South, West }

    public static class CardinalExtensions
    {
        /// <summary>The side you arrive from after walking out this way (North ↔ South, East ↔ West).</summary>
        public static Cardinal Opposite(this Cardinal d) => d switch
        {
            Cardinal.North => Cardinal.South,
            Cardinal.South => Cardinal.North,
            Cardinal.East => Cardinal.West,
            _ => Cardinal.East,
        };

        /// <summary>Room-coordinate delta when walking out through a door on this side.</summary>
        public static Vector2Int Offset(this Cardinal d) => d switch
        {
            Cardinal.North => new Vector2Int(0, 1),
            Cardinal.South => new Vector2Int(0, -1),
            Cardinal.East => new Vector2Int(1, 0),
            _ => new Vector2Int(-1, 0),
        };
    }
}
