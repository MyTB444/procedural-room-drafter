using System;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// The player's held collectables — currently a KEY count. Starts at zero and fires
    /// <see cref="KeysChanged"/> whenever it changes (and once on Start, so UI can sync to the initial
    /// value). Key pickups call <see cref="AddKeys"/>; <see cref="PlayerKeysUI"/> reads/subscribes.
    /// Lives on the player, so the count persists across room transitions (the player isn't destroyed).
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        /// <summary>How many keys the player is holding.</summary>
        public int Keys { get; private set; }

        public event Action<int> KeysChanged; // new key total

        private void Start() => KeysChanged?.Invoke(Keys); // start at zero — let listeners show it

        /// <summary>Add keys (pass a negative amount to spend); clamped at zero. Fires <see cref="KeysChanged"/>.</summary>
        public void AddKeys(int amount)
        {
            if (amount == 0) return;
            Keys = Mathf.Max(0, Keys + amount);
            KeysChanged?.Invoke(Keys);
        }
    }
}
