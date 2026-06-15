using System.Collections;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Fades a full-screen overlay to/from black for room transitions. Put this on a UI Canvas
    /// (Screen Space - Overlay) that has a black full-rect Image and a CanvasGroup.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("Seconds for a single fade (out or in).")]
        [SerializeField] private float fadeDuration = 0.25f;

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        /// <summary>Fade to fully black.</summary>
        public IEnumerator FadeOut() => Fade(1f);

        /// <summary>Fade back to fully transparent.</summary>
        public IEnumerator FadeIn() => Fade(0f);

        private IEnumerator Fade(float target)
        {
            float start = _group.alpha;
            _group.blocksRaycasts = true;

            // Unscaled so transitions still run if the game is paused (timeScale = 0).
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }

            _group.alpha = target;
            _group.blocksRaycasts = target > 0.5f;
        }
    }
}
