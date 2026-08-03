using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TRV
{
    /// <summary>
    /// Drives the two full-screen overlays — the pause MENU and the DEATH screen — and freezes the
    /// player while either is up. The pause menu is OPEN on game start (the game begins paused).
    /// Escape toggles the pause menu, but NOT while the death screen is
    /// showing (a dead run can't be un-paused, only restarted). The death screen turns on by itself
    /// when the player's <see cref="Health"/> dies. Both screens have a "Restart" button: wire it to
    /// <see cref="RestartScene"/>.
    ///
    /// Each overlay is a single root GameObject holding all its UI; this script just toggles the root.
    /// </summary>
    public class GameMenuUI : MonoBehaviour
    {
        [Header("Overlays (one root GameObject each)")]
        [Tooltip("Root object holding all pause-menu UI. Toggled by Escape; starts hidden.")]
        [SerializeField] private GameObject menuRoot;

        [Tooltip("Root object holding all death UI. Enabled automatically when the player dies; starts hidden.")]
        [SerializeField] private GameObject deathRoot;

        [Header("Player")]
        [Tooltip("The player to freeze while an overlay is up. Auto-found if left empty.")]
        [SerializeField] private TRVController player;

        private Health _health;
        private bool _menuOpen;
        private bool _dead;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TRVController>();
            if (player != null) _health = player.GetComponent<Health>();

            // Clean slate; the menu is opened in Start (after every Awake has run).
            if (menuRoot != null) menuRoot.SetActive(false);
            if (deathRoot != null) deathRoot.SetActive(false);
        }

        private void Start()
        {
            SetMenu(true); // the game opens on the pause menu (paused + player frozen)
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
            // Escape toggles the pause menu — but never once the death screen has taken over.
            if (!_dead && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetMenu(!_menuOpen);
        }

        private void SetMenu(bool open)
        {
            _menuOpen = open;
            if (menuRoot != null) menuRoot.SetActive(open);
            ApplyPauseState();
        }

        private void OnPlayerDied()
        {
            _dead = true;
            if (_menuOpen) SetMenu(false); // the death screen replaces the pause menu
            if (deathRoot != null) deathRoot.SetActive(true);
            ApplyPauseState();
        }

        // Only the pause MENU stops time; on death the world keeps running (timeScale stays 1) so the
        // game plays on behind the death screen. The player's controls are frozen while EITHER is up.
        // Escape/UI buttons still work even paused, because Update + the UI event system run unscaled.
        private void ApplyPauseState()
        {
            Time.timeScale = _menuOpen ? 0f : 1f;
            if (player != null) player.SetControlsEnabled(!_menuOpen && !_dead);
        }

        /// <summary>Reload the current scene from scratch. Bind this to the Restart buttons on both the
        /// pause menu and the death screen.</summary>
        public void RestartScene()
        {
            Time.timeScale = 1f; // defensive: in case anything paused time
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
