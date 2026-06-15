using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Briefly tints the character's sprite(s) a flash colour when hit, then restores them.
    /// Reusable on any character — its damage handler calls <see cref="Flash"/>. Collects every
    /// SpriteRenderer in children at Awake, so multi-part sprites (e.g. layered character art)
    /// all flash together.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [Tooltip("Colour to tint to on hit.")]
        [SerializeField] private Color flashColor = Color.red;

        [Tooltip("How long the flash lasts (seconds).")]
        [SerializeField, Min(0f)] private float duration = 0.1f;

        private SpriteRenderer[] _renderers;
        private Color[] _original;
        private float _timeLeft;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _original = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _original[i] = _renderers[i].color;
        }

        /// <summary>Start (or refresh) the hit flash.</summary>
        public void Flash()
        {
            _timeLeft = duration;
            Tint(flashColor);
        }

        private void Update()
        {
            if (_timeLeft <= 0f) return;
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f) Restore();
        }

        private void Tint(Color c)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = c;
        }

        private void Restore()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = _original[i];
        }
    }
}
