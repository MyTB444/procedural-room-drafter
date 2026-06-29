using UnityEngine;

namespace TRV
{
    /// <summary>
    /// An interactable placed in front of a room door. Spending ONE key on it makes THAT door always
    /// lead to a Halls room (<see cref="RoomManager.SetDoorToHalls"/>). Available only while: not already
    /// used, the room is cleared of enemies (<see cref="EnemyPool.RoomHasEnemies"/> false), and the
    /// player holds at least one key — so the "E" prompt only appears when the interaction can succeed.
    /// <see cref="RoomManager"/> positions one per door each room and calls <see cref="Configure"/>.
    /// </summary>
    public class KeyHolder : Interactable
    {
        [Tooltip("Keys this consumes when used.")]
        [SerializeField, Min(1)] private int keyCost = 1;

        private Cardinal _edge; // which door this sits in front of
        private bool _used;

        /// <summary>Place it for a new room: tie it to a door edge and re-arm it.</summary>
        public void Configure(Cardinal edge)
        {
            _edge = edge;
            _used = false;
        }

        protected override bool CanInteract() =>
            !_used
            && !EnemyPool.RoomHasEnemies
            && HeldKeys() >= keyCost
            && RoomManager.Instance != null
            && !RoomManager.Instance.NeighborRoomExists(_edge); // can't change an already-generated room

        public override void Interact(TRVController player)
        {
            if (_used || player == null) return;
            if (!player.TryGetComponent<PlayerInventory>(out var inventory) || inventory.Keys < keyCost) return;

            inventory.AddKeys(-keyCost);                          // spend the key(s)
            if (RoomManager.Instance != null)
                RoomManager.Instance.SetDoorToHalls(_edge);       // this door now leads to Halls
            _used = true;
        }

        private static int HeldKeys()
        {
            var player = PlayerLocator.Player;
            return player != null && player.TryGetComponent<PlayerInventory>(out var inv) ? inv.Keys : 0;
        }
    }
}
