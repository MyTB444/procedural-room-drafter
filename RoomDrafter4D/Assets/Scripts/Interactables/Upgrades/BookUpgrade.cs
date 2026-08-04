using UnityEngine;

namespace TRV
{
    /// <summary>
    /// An upgrade BOOK: interacting doesn't apply anything directly — it opens the single shared
    /// <see cref="UpgradeChoiceUI"/> listing THIS book's <see cref="choices"/> (one button + text
    /// per choice), and the player picks ONE. Only the chosen upgrade is applied; then the book
    /// object is consumed (the one-per-run id is NOT used — books are repeatable).
    ///
    /// Choices LEVEL: each choice (keyed by its Label, shared across all books offering it) can be
    /// taken <see cref="PlayerUpgrades.MaxChoiceLevel"/> (10) times per run — the UI shows
    /// "Label lvlN" and disables maxed choices; a book whose every choice is maxed stops being
    /// interactable. Each choice bundles stat modifiers (same data as <see cref="StatUpgrade"/>)
    /// and/or an ability unlock. Different books just hold different choice lists — the UI adapts.
    /// </summary>
    public class BookUpgrade : Upgrade
    {
        [System.Serializable]
        public class Choice
        {
            [Tooltip("Button text shown on the choice UI, e.g. \"Max HP +10\" — also used as the " +
                     "pickup popup text after choosing.")]
            public string Label;

            [Tooltip("Flat +number stat bonuses this choice applies (same as a StatUpgrade's).")]
            public StatUpgrade.Modifier[] Modifiers;

            [Tooltip("Also unlock an ability?")]
            public bool UnlockAbility;

            [Tooltip("The ability unlocked when Unlock Ability is ticked.")]
            public PlayerUpgrades.Ability Ability = PlayerUpgrades.Ability.Dash;
        }

        [Tooltip("The upgrades this book OFFERS — the choice UI shows one button per entry and " +
                 "the player picks exactly one.")]
        [SerializeField] private Choice[] choices;

        /// <summary>The choices this book offers (read by the choice UI).</summary>
        public Choice[] Choices => choices;

        /// <summary>Books ignore the one-per-run id (they're repeatable — many books per run,
        /// choices levelling up to <see cref="PlayerUpgrades.MaxChoiceLevel"/>): interactable while
        /// unused AND at least one choice isn't maxed yet.</summary>
        protected override bool CanInteract()
        {
            if (Used) return false;
            if (choices == null || choices.Length == 0) return true;

            var player = PlayerLocator.Player;
            if (player == null || !player.TryGetComponent<PlayerUpgrades>(out var upgrades)) return true;
            foreach (var c in choices)
                if (c != null && upgrades.ChoiceLevel(c.Label) < PlayerUpgrades.MaxChoiceLevel)
                    return true;
            return false; // every choice maxed — nothing left to offer
        }

        /// <summary>Open the shared choice UI with this book's options instead of applying anything.</summary>
        public override void Interact(TRVController player)
        {
            if (Used || player == null) return;
            if (!player.TryGetComponent<PlayerUpgrades>(out var upgrades)) return;

            // A book with nothing to offer degrades to a plain (effect-less) pickup.
            if (choices == null || choices.Length == 0)
            {
                ConsumeWithoutId(Message);
                return;
            }

            var ui = FindAnyObjectByType<UpgradeChoiceUI>(FindObjectsInactive.Include);
            if (ui == null)
            {
                Debug.LogWarning($"[{nameof(BookUpgrade)}] No UpgradeChoiceUI in the scene — " +
                                 "the book can't offer its choices.", this);
                return; // not consumed — the player can retry once the UI exists
            }
            ui.Open(this, player, upgrades);
        }

        /// <summary>Called by the choice UI when the player picks option <paramref name="index"/>:
        /// apply just that choice, then consume the book.</summary>
        public void Choose(int index, PlayerUpgrades upgrades)
        {
            if (Used || upgrades == null || choices == null || index < 0 || index >= choices.Length) return;

            var choice = choices[index];
            if (choice == null || upgrades.ChoiceLevel(choice.Label) >= PlayerUpgrades.MaxChoiceLevel)
                return; // maxed — the UI disables the button, this is just the guard

            if (choice.Modifiers != null)
            {
                foreach (var m in choice.Modifiers)
                {
                    if (m == null) continue;
                    upgrades.AddStat(m.Stat, m.Amount);
                }
            }
            if (choice.UnlockAbility) upgrades.Unlock(choice.Ability);

            upgrades.IncrementChoiceLevel(choice.Label);
            ConsumeWithoutId(choice.Label);
        }

        // The effect is applied per-choice in Choose; the base apply path is never used.
        protected override void Apply(PlayerUpgrades upgrades) { }
    }
}
