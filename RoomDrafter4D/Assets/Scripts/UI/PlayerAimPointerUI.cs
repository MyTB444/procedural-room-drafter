using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A WORLD-SPACE aim pointer that circles the player, always pointing at the cursor — so the
    /// player sees exactly where their next attack will go. Reads the live cursor aim from
    /// <see cref="TRVController.AimDirection"/> each LateUpdate, parks the <see cref="pointer"/>
    /// child at <see cref="radius"/> from the player along it and rotates it to face the cursor —
    /// full 360°, tracking the raw cursor angle (the arrow sprite is authored pointing NORTH/up;
    /// the attack itself still snaps 8-way on press).
    ///
    /// Like the other world-space indicators: put this on a holder object (or the world canvas) and
    /// let it move the pointer CHILD — it toggles the pointer's GameObject (hidden while the player
    /// is dead), so the script must NOT sit on the pointer object itself.
    /// </summary>
    public class PlayerAimPointerUI : MonoBehaviour
    {
        [Tooltip("The pointer visual moved/rotated around the player (arrow sprite drawn pointing " +
                 "NORTH/up). Auto-uses the first child when empty.")]
        [SerializeField] private Transform pointer;

        [Tooltip("Distance from the player's position to the pointer.")]
        [SerializeField, Min(0f)] private float radius = 0.9f;

        [Tooltip("Optional — auto-found when empty.")]
        [SerializeField] private TRVController player;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TRVController>();
            if (pointer == null && transform.childCount > 0) pointer = transform.GetChild(0);
        }

        private void LateUpdate()
        {
            if (player == null || pointer == null) return;

            bool show = player.IsAlive;
            if (pointer.gameObject.activeSelf != show) pointer.gameObject.SetActive(show);
            if (!show) return;

            Vector2 dir = player.AimDirection;
            pointer.position = player.transform.position + (Vector3)(dir * radius);
            // Sprite authored facing NORTH/up → -90° so "up" turns toward the aim. LOCAL rotation:
            // a tilted/scaled parent (e.g. under the perspective grid) won't skew the sprite.
            pointer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
