using UnityEngine;

namespace TRV
{
    /// <summary>
    /// An interactable UPGRADE pickup (like <see cref="KeyHolder"/>): the shared "E" prompt shows when the
    /// player is near, and interacting applies the upgrade to the player's <see cref="PlayerUpgrades"/>,
    /// then CONSUMES the object (disables it). One-time. Concrete upgrades define their effect in
    /// <see cref="Apply"/> — reuses the <see cref="Interactable"/> proximity/prompt system.
    /// </summary>
    public abstract class Upgrade : Interactable
    {
        [Tooltip("Which one-per-run upgrade this is — once collected, it won't be offered again.")]
        [SerializeField] private PlayerUpgrades.UpgradeId id;

        private bool _used;

        /// <summary>The one-per-run identity of this upgrade.</summary>
        public PlayerUpgrades.UpgradeId Id => id;

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
            gameObject.SetActive(false); // consumed
        }

        /// <summary>Apply this upgrade's effect to the player's runtime upgrade state.</summary>
        protected abstract void Apply(PlayerUpgrades upgrades);
    }
}
