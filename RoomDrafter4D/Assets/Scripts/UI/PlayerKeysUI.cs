using TMPro;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shows the player's key count for ONE <see cref="KeyType"/> colour from
    /// <see cref="PlayerInventory"/> — drop one instance per colour on the canvas. The whole
    /// counter (icon + text, the <see cref="root"/>) stays HIDDEN until the first key of its
    /// colour is collected, then appears and stays for the rest of the run (even if spent back
    /// to 0). Subscribes in Awake and toggles the root, so it keeps receiving updates while
    /// hidden — the object must start ACTIVE in the scene. Inventory auto-found when unassigned.
    /// </summary>
    public class PlayerKeysUI : MonoBehaviour
    {
        [Tooltip("Which key colour this counter shows.")]
        [SerializeField] private KeyType type = KeyType.Red;

        [Tooltip("The key-count text (e.g. 'x0').")]
        [SerializeField] private TMP_Text text;

        [Tooltip("Optional prefix before the number (e.g. 'x' or 'Keys: ').")]
        [SerializeField] private string prefix = "x";

        [Tooltip("Player inventory. Optional — auto-found from the scene when left empty.")]
        [SerializeField] private PlayerInventory inventory;

        [Tooltip("What is hidden until the first key of this colour is collected (the icon + " +
                 "text container). Defaults to this GameObject.")]
        [SerializeField] private GameObject root;

        private bool _revealed;

        private void Awake()
        {
            if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
            if (root == null) root = gameObject;
            if (inventory != null) inventory.KeysChanged += OnKeysChanged;
            Refresh(inventory != null ? inventory.Keys(type) : 0);
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.KeysChanged -= OnKeysChanged;
        }

        private void OnKeysChanged(KeyType changed, int keys)
        {
            if (changed == type) Refresh(keys);
        }

        private void Refresh(int keys)
        {
            if (!_revealed && keys > 0) _revealed = true;
            if (root != null && root.activeSelf != _revealed) root.SetActive(_revealed);
            if (text != null) text.text = prefix + keys;
        }
    }
}
