using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TRV
{
    /// <summary>
    /// The ROOM DRAFT panel, opened by <see cref="RoomManager"/> when the player steps on a door
    /// (instead of transitioning immediately). While open the player's controls are frozen.
    /// Buttons (wired in code — assign the Button references, no OnClick setup needed):
    /// • CANCEL — closes the panel; the player keeps playing, and the draft won't reopen until
    ///   they step OFF the door trigger and back on.
    /// • SKIP — no keys spent; transitions into the DEFAULT biome (Lands).
    /// • RED / BLUE / PURPLE — each shows how many keys of that colour the player holds; pressing
    ///   spends ONE key and drafts red → Anubis, blue → Aqua, purple → Halls. A button is only
    ///   interactable while the player holds a key of that colour (and the biome exists).
    /// Put this component on an ACTIVE object (e.g. the canvas) and let it toggle the panel root —
    /// the panel itself starts hidden.
    /// </summary>
    public class RoomDraftUI : MonoBehaviour
    {
        [Tooltip("The panel object shown/hidden (holds all the buttons). Starts hidden.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Buttons (wired in code)")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button redButton;
        [SerializeField] private Button blueButton;
        [SerializeField] private Button purpleButton;

        [Header("Per-key parents (the key image holding button + count) — hidden at 0 keys")]
        [SerializeField] private GameObject redRoot;
        [SerializeField] private GameObject blueRoot;
        [SerializeField] private GameObject purpleRoot;

        [Header("Per-key count texts (under the image parents)")]
        [SerializeField] private TMP_Text redCountText;
        [SerializeField] private TMP_Text blueCountText;
        [SerializeField] private TMP_Text purpleCountText;
        [SerializeField] private string countPrefix = "x";

        [Header("References (auto-found when empty)")]
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TRVController player;

        /// <summary>True while the panel is up (controls frozen, waiting for a choice).</summary>
        public bool IsOpen { get; private set; }

        private bool _initialized;

        private void Awake() => EnsureInit();

        /// <summary>Idempotent setup — also run from <see cref="Open"/>, so everything works even
        /// when this component sits on an object that starts INACTIVE (Awake never ran).</summary>
        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include);
            if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (player == null) player = FindFirstObjectByType<TRVController>(FindObjectsInactive.Include);

            // Unassigned count texts self-heal: use the TMP text found under each key parent.
            if (redCountText == null && redRoot != null) redCountText = redRoot.GetComponentInChildren<TMP_Text>(true);
            if (blueCountText == null && blueRoot != null) blueCountText = blueRoot.GetComponentInChildren<TMP_Text>(true);
            if (purpleCountText == null && purpleRoot != null) purpleCountText = purpleRoot.GetComponentInChildren<TMP_Text>(true);

            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
            if (skipButton != null) skipButton.onClick.AddListener(() => Confirm(null));
            if (redButton != null) redButton.onClick.AddListener(() => ConfirmWithKey(KeyType.Red));
            if (blueButton != null) blueButton.onClick.AddListener(() => ConfirmWithKey(KeyType.Blue));
            if (purpleButton != null) purpleButton.onClick.AddListener(() => ConfirmWithKey(KeyType.Purple));

            if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.KeysChanged -= OnKeysChanged;
        }

        /// <summary>Open the panel and freeze the player. Called by <see cref="RoomManager"/>.</summary>
        public void Open()
        {
            EnsureInit();
            if (IsOpen) return;
            IsOpen = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true); // component may live on the panel itself
            if (inventory != null) inventory.KeysChanged += OnKeysChanged;
            RefreshKeys();
            if (panelRoot != null) panelRoot.SetActive(true);
            if (player != null) player.SetControlsEnabled(false);
        }

        private void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (inventory != null) inventory.KeysChanged -= OnKeysChanged;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (player != null) player.SetControlsEnabled(true);
        }

        private void Cancel()
        {
            Close();
            if (roomManager != null) roomManager.CancelDraft();
        }

        private void Confirm(BiomeConfig biome)
        {
            Close();
            if (roomManager != null) roomManager.ConfirmDraft(biome);
        }

        /// <summary>Spend one key of the colour and draft its biome (red → Anubis, blue → Aqua,
        /// purple → Halls). No-op without a key or without that biome in the manager's list.</summary>
        private void ConfirmWithKey(KeyType key)
        {
            if (roomManager == null || inventory == null || inventory.Keys(key) <= 0) return;
            var biome = roomManager.BiomeForKey(key);
            if (biome == null) return;
            inventory.AddKeys(key, -1);
            Confirm(biome);
        }

        private void OnKeysChanged(KeyType type, int total) => RefreshKeys();

        private void RefreshKeys()
        {
            RefreshKey(redRoot, redButton, redCountText, KeyType.Red);
            RefreshKey(blueRoot, blueButton, blueCountText, KeyType.Blue);
            RefreshKey(purpleRoot, purpleButton, purpleCountText, KeyType.Purple);
        }

        private void RefreshKey(GameObject root, Button button, TMP_Text countText, KeyType key)
        {
            int count = inventory != null ? inventory.Keys(key) : 0;
            if (root != null) root.SetActive(count > 0); // no keys of this colour → whole option hidden
            if (countText != null) countText.text = countPrefix + count;
            if (button != null)
                button.interactable = count > 0 && roomManager != null && roomManager.BiomeForKey(key) != null;
        }
    }
}
