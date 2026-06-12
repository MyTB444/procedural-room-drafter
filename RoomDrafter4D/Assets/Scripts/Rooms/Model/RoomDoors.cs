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
        /// Uses the room's rolled door positions (<see cref="RoomGrid.DoorStarts"/>).
        /// </summary>
        public static Vector2Int Landing(BiomeConfig c, RoomGrid grid, Cardinal d) =>
            Landing(c, d, grid.DoorStarts[(int)d]);

        /// <summary>
        /// CENTRED-door fallback — only for callers without a grid (RoomManager's spawn fallback
        /// when no door tile is found on the entry edge). Generation passes use the grid overload.
        /// </summary>
        public static Vector2Int Landing(BiomeConfig c, Cardinal d) =>
            Landing(c, d, d is Cardinal.North or Cardinal.South
                ? (c.Width - c.DoorWidth) / 2
                : (c.Height - c.DoorWidth) / 2);

        private static Vector2Int Landing(BiomeConfig c, Cardinal d, int doorStart)
        {
            int inset = c.WallThickness - 1 + c.LandingDepth; // door cell (at WallThickness-1) + landing

            return d switch
            {
                Cardinal.South => new Vector2Int(doorStart, inset),
                Cardinal.North => new Vector2Int(doorStart, c.Height - 1 - inset),
                Cardinal.West => new Vector2Int(inset, doorStart),
                _ => new Vector2Int(c.Width - 1 - inset, doorStart), // East
            };
        }
    }
}
