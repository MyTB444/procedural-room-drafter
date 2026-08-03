using System;
using UnityEngine;

namespace TRV
{
    /// <summary>The key colours that drop in the world. Values are array indices — extend at the
    /// END only.</summary>
    public enum KeyType
    {
        Red = 0,
        Blue = 1,
        Purple = 2,
    }

    /// <summary>
    /// The player's held collectables — a KEY count per <see cref="KeyType"/> colour. Each starts
    /// at zero and fires <see cref="KeysChanged"/> whenever it changes (and once per colour on
    /// Start, so UI can sync to the initial values). Key pickups call <see cref="AddKeys"/>;
    /// <see cref="PlayerKeysUI"/> reads/subscribes. Lives on the player, so the counts persist
    /// across room transitions (the player isn't destroyed).
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        private readonly int[] _keys = new int[3];

        public event Action<KeyType, int> KeysChanged; // (colour, new total)

        /// <summary>How many keys of a colour the player is holding.</summary>
        public int Keys(KeyType type) => _keys[(int)type];

        private void Start() // start at zero — let listeners show it
        {
            for (int i = 0; i < _keys.Length; i++)
                KeysChanged?.Invoke((KeyType)i, _keys[i]);
        }

        /// <summary>Add keys of a colour (negative = spend); clamped at zero. Fires <see cref="KeysChanged"/>.</summary>
        public void AddKeys(KeyType type, int amount)
        {
            if (amount == 0) return;
            _keys[(int)type] = Mathf.Max(0, _keys[(int)type] + amount);
            KeysChanged?.Invoke(type, _keys[(int)type]);
        }
    }
}
