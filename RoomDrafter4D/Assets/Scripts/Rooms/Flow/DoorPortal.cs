using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Sits on the Door tilemap (its TilemapCollider2D set to "Is Trigger"). When the player steps
    /// onto any door tile it tells the <see cref="RoomManager"/>, which works out which of the 4
    /// doors it was from the player's position and transitions to that neighbour room.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DoorPortal : MonoBehaviour
    {
        [SerializeField] private RoomManager roomManager;

        private void Reset()
        {
            // Convenience: default the collider to trigger when first added.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (roomManager == null) return;
            if (other.GetComponentInParent<TRVController>() == null) return; // players only
            roomManager.OnPlayerEnteredDoor(other.transform.position);
        }
    }
}
