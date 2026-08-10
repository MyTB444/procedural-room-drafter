using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// In-memory store of visited rooms, keyed by room coord. A room is captured on leave and
    /// restored on return so it comes back exactly — including the hand-made starting room. This is
    /// the single seam where cross-session save/load will live later.
    /// </summary>
    public class RoomCache
    {
        private readonly Dictionary<Vector2Int, RoomSnapshot> _rooms = new Dictionary<Vector2Int, RoomSnapshot>();

        public bool TryGet(Vector2Int coord, out RoomSnapshot snapshot) => _rooms.TryGetValue(coord, out snapshot);

        public void Save(Vector2Int coord, RoomSnapshot snapshot) => _rooms[coord] = snapshot;
    }
}
