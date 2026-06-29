using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A single WORLD-SPACE "E" prompt (like <see cref="PlayerAttackCooldownUI"/>): each frame it finds
    /// the interactable the player is currently next to (<see cref="Interactable.FindNearestAvailable"/>)
    /// and parks the prompt on top of it; when none is available it hides the prompt. One of these on the
    /// world Canvas serves every <see cref="Interactable"/> in the scene.
    ///
    /// Editor: put this on the world Canvas / a holder, with the "E" image/text as the <see cref="prompt"/>
    /// child (the script moves this holder and toggles that child — don't put the script on the child).
    /// </summary>
    public class InteractPromptUI : MonoBehaviour
    {
        [Tooltip("The 'E' image/text shown over the available interactable. Toggled on/off.")]
        [SerializeField] private GameObject prompt;

        [Tooltip("World-space offset above the interactable (e.g. (0, 1, 0)).")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1f, 0f);

        private void Awake()
        {
            if (prompt == null && transform.childCount > 0) prompt = transform.GetChild(0).gameObject;
        }

        private void LateUpdate()
        {
            var target = PlayerLocator.TryGetPosition(out var p)
                ? Interactable.FindNearestAvailable(p)
                : null;

            bool show = target != null;
            if (prompt != null && prompt.activeSelf != show) prompt.SetActive(show);
            if (show) transform.position = target.transform.position + worldOffset; // follow + sit on top
        }
    }
}
