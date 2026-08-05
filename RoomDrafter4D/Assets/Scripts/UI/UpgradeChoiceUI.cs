using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TRV
{
    /// <summary>
    /// The single shared UPGRADE CHOICE panel, opened by a <see cref="BookUpgrade"/> on interact:
    /// it shows one button (+ label text) per choice the book offers — slots beyond the book's
    /// choice count are hidden, so every book reuses the same UI. While open the player's controls
    /// are frozen; pressing a choice applies it, consumes the book and closes the panel.
    ///
    /// Setup: author up to N choice buttons under <see cref="panelRoot"/> and assign them to
    /// <see cref="choiceButtons"/> (wired in code — no OnClick setup). Each button's label text is
    /// auto-found in its children (or assign <see cref="choiceTexts"/> explicitly, same order).
    /// Put the component on an ACTIVE object (e.g. the canvas) and let it toggle the panel root —
    /// the panel itself starts hidden.
    /// </summary>
    public class UpgradeChoiceUI : MonoBehaviour
    {
        [Tooltip("The panel object shown/hidden (holds the choice buttons). Starts hidden.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("The authored choice button slots — a book with fewer choices hides the rest.")]
        [SerializeField] private Button[] choiceButtons;

        [Tooltip("Label text per button (same order). Empty entries auto-find the TMP text in the " +
                 "button's children.")]
        [SerializeField] private TMP_Text[] choiceTexts;

        /// <summary>True while the panel is up (controls frozen, waiting for a choice).</summary>
        public bool IsOpen { get; private set; }

        private BookUpgrade _book;
        private PlayerUpgrades _upgrades;
        private TRVController _player;
        private bool _initialized;

        private void Awake() => EnsureInit();

        /// <summary>Idempotent setup — also run from <see cref="Open"/>, so everything works even
        /// when this component sits on an object that starts INACTIVE (Awake never ran).</summary>
        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            if (choiceButtons == null) choiceButtons = new Button[0];
            if (choiceTexts == null || choiceTexts.Length != choiceButtons.Length)
                System.Array.Resize(ref choiceTexts, choiceButtons.Length);

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                if (choiceTexts[i] == null)
                    choiceTexts[i] = choiceButtons[i].GetComponentInChildren<TMP_Text>(true);
                int index = i; // capture per button
                choiceButtons[i].onClick.AddListener(() => Choose(index));
            }

            if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(false);
        }

        /// <summary>Show the panel with <paramref name="book"/>'s choices and freeze the player.
        /// Called by <see cref="BookUpgrade.Interact"/>.</summary>
        public void Open(BookUpgrade book, TRVController player, PlayerUpgrades upgrades)
        {
            EnsureInit();
            if (IsOpen || book == null || book.Choices == null) return;
            if (!UIGate.TryOpen(this)) return; // another blocking UI is up — the book stays usable

            IsOpen = true;
            _book = book;
            _player = player;
            _upgrades = upgrades;

            if (!gameObject.activeSelf) gameObject.SetActive(true); // component may live on the panel itself

            // One button per choice; hide the slots this book doesn't need. Text shows the choice's
            // current LEVEL (times taken, "Label lvlN"); a maxed choice stays visible but disabled.
            var choices = book.Choices;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                bool used = i < choices.Length && choices[i] != null;
                choiceButtons[i].gameObject.SetActive(used);
                if (!used) continue;

                int level = upgrades != null ? upgrades.ChoiceLevel(choices[i].Label) : 0;
                choiceButtons[i].interactable = level < BookUpgrade.MaxLevelOf(choices[i]);
                if (choiceTexts[i] != null) choiceTexts[i].text = $"{choices[i].Label} lvl {level}";
            }
            if (choices.Length > choiceButtons.Length)
                Debug.LogWarning($"[{nameof(UpgradeChoiceUI)}] Book '{book.name}' offers " +
                                 $"{choices.Length} choices but only {choiceButtons.Length} button " +
                                 "slots are assigned — the extras aren't shown.", this);

            if (panelRoot != null) panelRoot.SetActive(true);
            if (_player != null) _player.SetControlsEnabled(false);
        }

        private void Choose(int index)
        {
            if (!IsOpen || _book == null) return;
            _book.Choose(index, _upgrades); // applies the choice + consumes the book
            Close();
        }

        private void Close()
        {
            IsOpen = false;
            UIGate.Close(this);
            if (panelRoot != null) panelRoot.SetActive(false);
            if (_player != null) _player.SetControlsEnabled(true);
            _book = null;
            _upgrades = null;
            _player = null;
        }
    }
}
