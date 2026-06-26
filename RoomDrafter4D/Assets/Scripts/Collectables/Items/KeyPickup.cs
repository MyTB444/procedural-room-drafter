using UnityEngine;

namespace TRV
{
    /// <summary>A key pickup: adds to the player's <see cref="PlayerInventory"/> key count on collect.</summary>
    public class KeyPickup : Collectable
    {
        [Header("Effect")]
        [Tooltip("How many keys this grants when collected.")]
        [SerializeField, Min(1)] private int keys = 1;

        protected override void OnCollected(TRVController player)
        {
            if (player != null && player.TryGetComponent<PlayerInventory>(out var inventory))
                inventory.AddKeys(keys);
        }
    }
}
