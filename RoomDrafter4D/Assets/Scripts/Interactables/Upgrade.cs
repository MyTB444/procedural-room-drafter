using UnityEngine;

namespace TRV
{
    /// <summary>
    /// An interactable UPGRADE pickup: the shared "E" prompt shows when the
    /// player is near, and interacting applies the upgrade to the player's <see cref="PlayerUpgrades"/>,
    /// then CONSUMES the object (disables it). One-time. Concrete upgrades define their effect in
    /// <see cref="Apply"/> — reuses the <see cref="Interactable"/> proximity/prompt system.
    /// </summary>
    public abstract class Upgrade : Interactable
    {
        [Tooltip("Which one-per-run upgrade this is — once collected, it won't be offered again.")]
        [SerializeField] private PlayerUpgrades.UpgradeId id;

        [Tooltip("Message shown briefly on-screen when this upgrade is collected, e.g. \"Attack Speed +50%\".")]
        [SerializeField, TextArea] private string message = "Upgrade acquired!";

        private bool _used;

        /// <summary>The one-per-run identity of this upgrade.</summary>
        public PlayerUpgrades.UpgradeId Id => id;

        /// <summary>The message describing what this upgrade did (shown on pickup).</summary>
        public string Message => message;

        /// <summary>Fires when ANY upgrade is collected, passing its <see cref="Message"/> — a UI
        /// (e.g. <c>UpgradePickupUI</c>) shows it briefly to tell the player what was upgraded.</summary>
        public static event System.Action<string> Collected;

        protected override bool CanInteract()
        {
            if (_used) return false;
            var player = PlayerLocator.Player; // not interactable once its type has been collected
            return player == null || !player.TryGetComponent<PlayerUpgrades>(out var up) || !up.HasCollected(id);
        }

        public override void Interact(TRVController player)
        {
            if (_used || player == null) return;
            if (!player.TryGetComponent<PlayerUpgrades>(out var upgrades) || upgrades.HasCollected(id)) return;

            Apply(upgrades);
            upgrades.MarkCollected(id);
            _used = true;
            Collected?.Invoke(message); // tell the pickup UI what was upgraded
            gameObject.SetActive(false); // consumed
        }

        /// <summary>Apply this upgrade's effect to the player's runtime upgrade state.</summary>
        protected abstract void Apply(PlayerUpgrades upgrades);
    }
}
