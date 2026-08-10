namespace TRV
{
    /// <summary>
    /// What a single room cell is. Drives which Tilemap layer the painter draws it on.
    /// </summary>
    public enum CellType
    {
        Wall = 0,     // perimeter / solid wall                 → Walls layer (collider)
        Floor = 1,    // walkable floor                         → Map layer
        Water = 2,    // impassable water                       → Walls layer (collider)
        Door = 3,     // walkable doorway (4/room)              → Door layer (trigger)
        Building = 4, // north-wall extension (BuildingPass)    → Walls layer (collider, floor under)
    }

    public static class CellTypeExtensions
    {
        /// <summary>Cells the player can stand on.</summary>
        public static bool IsWalkable(this CellType c) => c == CellType.Floor || c == CellType.Door;
    }
}
