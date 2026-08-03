using UnityEngine;

namespace TRV
{
    /// <summary>A key pickup: adds to the player's <see cref="PlayerInventory"/> count for its
    /// colour on collect. One prefab per <see cref="KeyType"/> (red/blue/purple); the existing red
    /// key prefab keeps working since Red is the default.</summary>
    public class KeyPickup : Collectable
    {
        [Tooltip("Which key colour this pickup grants.")]
        [SerializeField] private KeyType type = KeyType.Red;

        [Tooltip("How many keys this pickup grants.")]
        [SerializeField, Min(1)] private int keys = 1;

        protected override void OnCollected(TRVController player)
        {
            if (player != null && player.TryGetComponent<PlayerInventory>(out var inventory))
                inventory.AddKeys(type, keys);
        }
    }
}
