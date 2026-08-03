using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Sits on the Door tilemap (its TilemapCollider2D set to "Is Trigger"). While the player is on
    /// any door tile it tells the <see cref="RoomManager"/>, which works out which of the 4 doors it
    /// was and transitions to that neighbour room. Uses Stay (not Enter) so a door that's locked
    /// (room not yet cleared) opens the instant the last enemy dies while the player waits on it.
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

        private void OnTriggerStay2D(Collider2D other)
        {
            if (roomManager == null) return;
            if (other.GetComponentInParent<TRVController>() == null) return; // players only
            roomManager.OnPlayerEnteredDoor(other.transform.position);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (roomManager == null) return;
            if (other.GetComponentInParent<TRVController>() == null) return; // players only
            roomManager.OnPlayerLeftDoor(); // re-arms the draft UI after a cancel
        }
    }
}
