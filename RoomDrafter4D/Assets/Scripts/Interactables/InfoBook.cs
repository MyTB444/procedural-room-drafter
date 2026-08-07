using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A HELP/INFO book: a world object that TOGGLES its own assigned UI panel when the player
    /// presses E next to it (the shared "E" prompt appears via the normal <see cref="Interactable"/>
    /// proximity system — press once to open, again to close). NON-BLOCKING — the player's controls
    /// stay enabled while the panel is up, and it isn't part of the <see cref="UIGate"/>
    /// exclusivity. The panel hides by itself the moment the player walks out of the interact range
    /// (or dies). Never consumed — usable any number of times.
    ///
    /// STARTING ROOM ONLY: the book exists only while the player is in the start room (layer 0) —
    /// everywhere else its visuals, prompt and panel all disappear (the scene object persists
    /// across room repaints, so it hides itself instead).
    ///
    /// Setup: put this on the book object (with its sprite), assign the book's OWN info panel to
    /// <see cref="infoUI"/> (a UI object that starts INACTIVE). Each book instance can point at a
    /// different panel.
    /// </summary>
    public class InfoBook : Interactable
    {
        [Tooltip("THIS book's help/info panel — TOGGLED by E, hidden when the player walks away. " +
                 "Starts inactive; each book assigns its own.")]
        [SerializeField] private GameObject infoUI;

        private SpriteRenderer[] _visuals; // hidden outside the starting room
        private bool _inStartRoom = true;

        private void Awake() => _visuals = GetComponentsInChildren<SpriteRenderer>(true);

        protected override bool CanInteract() => infoUI != null && _inStartRoom;

        /// <summary>E toggles the panel: open when closed, closed when open.</summary>
        public override void Interact(TRVController player)
        {
            if (infoUI != null) infoUI.SetActive(!infoUI.activeSelf);
        }

        private void Update()
        {
            // The book only EXISTS in the starting room (layer 0) — the scene object survives room
            // repaints, so it hides its own visuals (and panel) elsewhere.
            bool inStart = RoomManager.Instance == null || RoomManager.Instance.CurrentLayer == 0;
            if (inStart != _inStartRoom)
            {
                _inStartRoom = inStart;
                foreach (var visual in _visuals)
                    if (visual != null) visual.enabled = inStart;
                if (!inStart && infoUI != null) infoUI.SetActive(false);
            }

            if (infoUI == null || !infoUI.activeSelf) return;

            // Auto-hide once the player leaves the interact area (or is gone/dead).
            if (!PlayerLocator.TryGetPosition(out var playerPos) ||
                ((Vector2)transform.position - playerPos).sqrMagnitude > interactRange * interactRange)
                infoUI.SetActive(false);
        }

        // A hidden panel should not linger across room changes / pooling.
        protected override void OnDisable()
        {
            base.OnDisable();
            if (infoUI != null) infoUI.SetActive(false);
        }
    }
}
