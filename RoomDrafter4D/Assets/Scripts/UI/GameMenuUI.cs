using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TRV
{
    /// <summary>
    /// The single MAIN MENU overlay — it doubles as the pause menu AND the death screen (one root,
    /// one set of buttons). Open on game start (the game begins paused), toggled with Escape, and
    /// forced open when the player dies. Freezes the player while up; pauses time while up EXCEPT
    /// when dead (the world keeps running behind a death menu).
    ///
    /// Buttons (wired in CODE — just assign the Button references):
    /// • PRIMARY — context-sensitive: "Play" on first open (closes the menu to start playing),
    ///   "Resume" once the game has begun (closes the menu), "Restart" when the player is DEAD
    ///   (reloads the scene; Escape is ignored while dead, so restarting is the only way on).
    /// • PANEL SWITCH — toggles between the INFO and CONTROLS panels: shows "Controls" while the
    ///   info panel is up (click → swap to controls), then "Info" (click → swap back). The menu
    ///   reopens on the info panel each time.
    /// </summary>
    public class GameMenuUI : MonoBehaviour
    {
        [Header("Overlay (one root for menu + death)")]
        [Tooltip("Root object holding ALL menu UI. Starts hidden; opened in Start.")]
        [SerializeField] private GameObject menuRoot;

        [Header("Primary button (Play / Resume / Restart)")]
        [SerializeField] private Button primaryButton;
        [Tooltip("The button's label. Auto-found in the button's children when empty.")]
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private string playText = "Play";
        [SerializeField] private string resumeText = "Resume";
        [SerializeField] private string restartText = "Restart";

        [Header("Panel switch button (Info <-> Controls)")]
        [SerializeField] private Button switchButton;
        [Tooltip("The switch button's label. Auto-found in the button's children when empty.")]
        [SerializeField] private TMP_Text switchLabel;
        [Tooltip("The INFO panel/text object — visible first.")]
        [SerializeField] private GameObject infoPanel;
        [Tooltip("The CONTROLS panel/text object — swapped in by the button.")]
        [SerializeField] private GameObject controlsPanel;
        [Tooltip("Button label while the INFO panel shows (i.e. what clicking switches TO).")]
        [SerializeField] private string controlsText = "Controls";
        [SerializeField] private string infoText = "Info";

        [Header("Player")]
        [Tooltip("The player to freeze while the menu is up. Auto-found if left empty.")]
        [SerializeField] private TRVController player;

        private Health _health;
        private bool _menuOpen;
        private bool _started;         // has the game begun (first Play pressed)?
        private bool _dead;
        private bool _showingControls; // panel switch state

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TRVController>();
            if (player != null) _health = player.GetComponent<Health>();

            if (primaryButton != null)
            {
                if (primaryLabel == null) primaryLabel = primaryButton.GetComponentInChildren<TMP_Text>(true);
                primaryButton.onClick.AddListener(OnPrimaryPressed);
            }
            if (switchButton != null)
            {
                if (switchLabel == null) switchLabel = switchButton.GetComponentInChildren<TMP_Text>(true);
                switchButton.onClick.AddListener(() => SetPanels(!_showingControls));
            }

            // Clean slate; the menu is opened in Start (after every Awake has run). The gate is
            // static, so clear any claim left over from a previous scene load.
            UIGate.Reset();
            if (menuRoot != null) menuRoot.SetActive(false);
        }

        private void Start()
        {
            SetMenu(true); // the game opens on the menu ("Play"), paused + player frozen
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnPlayerDied;
        }

        private void Update()
        {
            // Escape toggles the menu — but never once the player is dead (Restart is the only way).
            if (!_dead && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetMenu(!_menuOpen);
        }

        private void SetMenu(bool open)
        {
            // The menu is a blocking UI: it can't open OVER the draft/choice panel (Escape ignored
            // until that closes), but once open it holds the gate — nothing else (map included, via
            // MenuActive) can happen until it closes.
            if (open && !UIGate.TryOpen(this)) return;
            if (!open && !_dead) UIGate.Close(this);

            if (!open && _menuOpen && !_started) _started = true; // first close = the game begins

            _menuOpen = open;
            UIGate.MenuActive = _menuOpen || _dead;
            if (menuRoot != null) menuRoot.SetActive(open);
            if (open)
            {
                RefreshPrimaryLabel();
                SetPanels(false); // every open starts on the INFO panel
            }
            ApplyPauseState();
        }

        /// <summary>Play (first time) and Resume both just close the menu; Restart reloads.</summary>
        private void OnPrimaryPressed()
        {
            if (_dead) RestartScene();
            else SetMenu(false);
        }

        private void RefreshPrimaryLabel()
        {
            if (primaryLabel != null)
                primaryLabel.text = _dead ? restartText : (_started ? resumeText : playText);
        }

        /// <summary>Show the info OR the controls panel; the button's label names the OTHER one.</summary>
        private void SetPanels(bool showControls)
        {
            _showingControls = showControls;
            if (infoPanel != null) infoPanel.SetActive(!showControls);
            if (controlsPanel != null) controlsPanel.SetActive(showControls);
            if (switchLabel != null) switchLabel.text = showControls ? infoText : controlsText;
        }

        /// <summary>Death forces the menu open as the death screen: primary button = "Restart",
        /// Escape disabled, the world keeps RUNNING behind it (no time pause), controls frozen.</summary>
        private void OnPlayerDied()
        {
            _dead = true;
            UIGate.ForceOpen(this); // takes over no matter what was open
            UIGate.MenuActive = true;
            _menuOpen = true;
            if (menuRoot != null) menuRoot.SetActive(true);
            RefreshPrimaryLabel();
            SetPanels(false);
            ApplyPauseState();
        }

        // The menu pauses time — EXCEPT when dead (the world plays on behind the death menu).
        // Controls are frozen while the menu is up or the player is dead. Escape/UI still respond
        // while paused because Update + the UI event system run on unscaled time.
        private void ApplyPauseState()
        {
            Time.timeScale = _menuOpen && !_dead ? 0f : 1f;
            if (player != null) player.SetControlsEnabled(!_menuOpen && !_dead);
        }

        /// <summary>Reload the current scene from scratch (also bindable to a button OnClick).</summary>
        public void RestartScene()
        {
            Time.timeScale = 1f; // defensive: in case anything paused time
            UIGate.Reset();      // statics survive the reload
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
