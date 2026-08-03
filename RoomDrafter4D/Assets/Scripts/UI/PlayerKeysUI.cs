using TMPro;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shows the player's key count for ONE <see cref="KeyType"/> colour from
    /// <see cref="PlayerInventory"/> — drop one instance per colour on the canvas. Subscribes to
    /// <see cref="PlayerInventory.KeysChanged"/> so it refreshes the instant a key is collected
    /// (or spent); it also syncs once on enable. Inventory is auto-found when left unassigned.
    /// </summary>
    public class PlayerKeysUI : MonoBehaviour
    {
        [Tooltip("Which key colour this text shows.")]
        [SerializeField] private KeyType type = KeyType.Red;

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
            OnKeysChanged(type, inventory.Keys(type)); // sync now (inventory may have started already)
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.KeysChanged -= OnKeysChanged;
        }

        private void OnKeysChanged(KeyType changed, int keys)
        {
            if (changed != type || text == null) return;
            text.text = prefix + keys;
        }
    }
}
