using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A small WORLD-SPACE attack-cooldown indicator hovering above the player. A <see cref="marker"/>
    /// image slides along a <see cref="track"/> image: it starts at the LEFT the instant the player
    /// attacks and travels to the RIGHT as the cooldown elapses, reaching the track's right end exactly
    /// when the player can attack again. Driven by <see cref="TRVController.AttackCooldownRemaining01"/>,
    /// so it reflects the CURRENT cooldown length even when an effect changes it. Optionally follows the
    /// player and hides while ready.
    ///
    /// Editor: the marker should be a CHILD of the track with a CENTRED pivot (0.5, 0.5) — its anchors
    /// don't matter, the script slides it in the track's local space across the track's width. Put this
    /// script on the world Canvas / holder (not the marker it toggles); parent the holder under the player
    /// or assign Follow Target.
    /// </summary>
    public class PlayerAttackCooldownUI : MonoBehaviour
    {
        [Tooltip("The moving marker image's RectTransform — a CHILD of the track with a centred pivot " +
                 "(0.5, 0.5). Slides left→right across the track as the cooldown elapses (anchors don't matter).")]
        [SerializeField] private RectTransform marker;

        [Tooltip("The track image's RectTransform — the line the marker slides along. Its width defines the travel.")]
        [SerializeField] private RectTransform track;

        [Tooltip("Player. Optional — auto-found from the scene's TRVController when left empty.")]
        [SerializeField] private TRVController player;

        [Tooltip("If set, the indicator follows this transform (the player) at World Offset each frame. " +
                 "Leave empty if the indicator is parented under the player instead.")]
        [SerializeField] private Transform followTarget;

        [Tooltip("World-space offset above the follow target (e.g. (0, 1, 0)).")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1f, 0f);

        [Tooltip("Hide the WHOLE indicator (track + marker) while the player is ready (cooldown elapsed); " +
                 "it reappears the instant the player attacks. On by default.")]
        [SerializeField] private bool hideWhenReady = true;

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<TRVController>();
        }

        private void LateUpdate()
        {
            if (player == null || marker == null || track == null) return;

            float remaining = player.AttackCooldownRemaining01; // 1 = just attacked, 0 = ready
            float t = 1f - remaining;                            // 0 = left (just attacked), 1 = right (ready)

            // Slide the marker's centre across the track (track-local), kept fully inside it. Uses
            // localPosition + the track's rect bounds, so it works regardless of the marker's anchors —
            // the marker just needs to be a CHILD of the track with a centred pivot.
            float half = marker.rect.width * 0.5f;
            var lp = marker.localPosition;
            lp.x = Mathf.Lerp(track.rect.xMin + half, track.rect.xMax - half, t);
            marker.localPosition = lp;

            if (hideWhenReady)
            {
                bool show = remaining > 0f; // visible only during the cooldown
                if (track.gameObject.activeSelf != show) track.gameObject.SetActive(show);
                if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            }

            if (followTarget != null) transform.position = followTarget.position + worldOffset;
        }
    }
}
