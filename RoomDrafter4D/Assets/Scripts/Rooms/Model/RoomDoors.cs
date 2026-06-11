using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Door geometry for a room: the walkable landing cell just inside each door. Shared by
    /// <see cref="ConnectivityPass"/> (carve toward landings) and <see cref="RoomManager"/>
    /// (formula fallback when the entry door can't be found in the painted Door layer).
    /// </summary>
    public static class RoomDoors
    {
        /// <summary>
        /// Deepest floor landing cell just inside the given door — guaranteed walkable and far
        /// enough from the door tile to be a safe spawn (won't instantly re-trigger the portal).
        /// </summary>
        public static Vector2Int Landing(BiomeConfig c, Cardinal d)
        {
            int colStart = (c.Width - c.DoorWidth) / 2;  // first door column (top/bottom doors)
            int rowStart = (c.Height - c.DoorWidth) / 2;  // first door row (left/right doors)
            int inset = c.WallThickness - 1 + c.LandingDepth; // door cell (at WallThickness-1) + landing

            return d switch
            {
                Cardinal.South => new Vector2Int(colStart, inset),
                Cardinal.North => new Vector2Int(colStart, c.Height - 1 - inset),
                Cardinal.West => new Vector2Int(inset, rowStart),
                _ => new Vector2Int(c.Width - 1 - inset, rowStart), // East
            };
        }
    }
}
