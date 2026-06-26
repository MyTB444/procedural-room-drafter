using TMPro;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shows the player's key count from <see cref="PlayerInventory"/>. Subscribes to
    /// <see cref="PlayerInventory.KeysChanged"/> so it refreshes the instant a key is collected (or
    /// spent); it also syncs once on enable. Inventory is auto-found from the scene when left unassigned.
    /// </summary>
    public class PlayerKeysUI : MonoBehaviour
    {
        [Tooltip("The key-count text (e.g. 'x0').")]
        [SerializeField] private TMP_Text text;

        [Tooltip("Optional prefix before the number (e.g. 'x' or 'Keys: ').")]
        [SerializeField] private string prefix = "x";

        [Tooltip("Player inventory. Optional — auto-found from the scene when left empty.")]
        [SerializeField] private PlayerInventory inventory;

        private void Awake()
        {
            if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.KeysChanged += OnKeysChanged;
            OnKeysChanged(inventory.Keys); // sync now (inventory may have started before we subscribed)
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.KeysChanged -= OnKeysChanged;
        }

        private void OnKeysChanged(int keys)
        {
            if (text != null) text.text = prefix + keys;
        }
    }
}
