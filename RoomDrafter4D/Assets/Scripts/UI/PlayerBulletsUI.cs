using UnityEngine;
using UnityEngine.UI;

namespace TRV
{
    /// <summary>
    /// The per-room BULLET counter: a row of bullet images (author up to 8 — the firearm skill can
    /// level the count up to that), one enabled per bullet the player still has this room
    /// (<see cref="TRVController.BulletsLeft"/>, polled); firing turns them off one by one and
    /// entering a new room lights them up again. Everything is hidden until the FIRE ability is
    /// unlocked.
    ///
    /// Fully poll-based (no events): <see cref="PlayerUpgrades"/> is added to the player AT RUNTIME
    /// by the controller, so it's re-resolved lazily each frame until found. When <see cref="root"/>
    /// is a separate container it's SetActive-toggled; when it's this same GameObject the lock
    /// instead just disables all images (the object must stay active to keep polling).
    /// </summary>
    public class PlayerBulletsUI : MonoBehaviour
    {
        [Tooltip("The bullet images, one per possible bullet (author up to 8). Empty = auto-use " +
                 "the Image components found in this object's children, hierarchy order.")]
        [SerializeField] private Image[] bulletImages;

        [Tooltip("Optional container toggled with the Fire unlock. Leave empty (or set to this " +
                 "object) to hide by disabling the images instead.")]
        [SerializeField] private GameObject root;

        [Tooltip("Optional — auto-found when empty.")]
        [SerializeField] private TRVController player;

        private PlayerUpgrades _upgrades;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TRVController>();
            if (bulletImages == null || bulletImages.Length == 0)
                bulletImages = GetComponentsInChildren<Image>(true);
        }

        private void Update()
        {
            if (player == null) return;

            // PlayerUpgrades appears on the player during ITS Awake — resolve whenever it's there.
            if (_upgrades == null) _upgrades = player.GetComponent<PlayerUpgrades>();

            bool unlocked = _upgrades != null && _upgrades.IsUnlocked(PlayerUpgrades.Ability.Fire);

            // A separate container can be switched off wholesale; our own object must stay active
            // (disabling it would stop this Update and the counter could never come back).
            if (root != null && root != gameObject)
            {
                if (root.activeSelf != unlocked) root.SetActive(unlocked);
                if (!unlocked) return;
            }

            if (bulletImages == null) return;
            int left = player.BulletsLeft;
            for (int i = 0; i < bulletImages.Length; i++)
                if (bulletImages[i] != null) bulletImages[i].enabled = unlocked && i < left;
        }
    }
}
