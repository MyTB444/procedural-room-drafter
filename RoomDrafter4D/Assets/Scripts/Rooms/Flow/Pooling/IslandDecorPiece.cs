using System.Collections;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Makes a pooled island-decor object hittable. On a hit it turns its collider OFF immediately,
    /// fires the break animation (Animator "break" param), and after the animation returns itself
    /// to its <see cref="IslandDecorPool"/> (disabled) with the collider re-enabled for the next
    /// reuse. The pool attaches and (re)configures one of these on every pooled instance, so decor
    /// prefabs only need a Collider2D (on a hittable layer) and an Animator with a "Break" trigger.
    /// </summary>
    public class IslandDecorPiece : MonoBehaviour, IDamageable
    {
        [Tooltip("Animator trigger fired when the piece is broken.")]
        [SerializeField] private string breakParam = "Break";

        [Tooltip("Length of the break animation (seconds); the piece returns to the pool after this.")]
        [SerializeField, Min(0f)] private float breakDuration = 1f;

        private PrefabPool _pool;
        private bool _alive;
        private Collider2D _collider;
        private Animator _animator;
        private Coroutine _breaking;
        private System.Action _onDestroyed; // forgets this placement so it doesn't respawn on revisit

        public bool IsAlive => _alive;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _animator = GetComponentInChildren<Animator>(true);
        }

        /// <summary>
        /// Re-arm the piece when the pool shows it (called each time it's placed). Also recovers a
        /// break that got interrupted by a room change: collider back on, alive again.
        /// <paramref name="onDestroyed"/> is invoked once if this piece is broken, so the placement can
        /// be forgotten (a destroyed piece stays destroyed when the player returns to the room).
        /// </summary>
        public void Activate(PrefabPool pool, System.Action onDestroyed = null)
        {
            _pool = pool;
            _onDestroyed = onDestroyed;
            _alive = true;

            if (_breaking != null) { StopCoroutine(_breaking); _breaking = null; }
            if (_collider != null) _collider.enabled = true;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!_alive) return;
            _alive = false;

            _onDestroyed?.Invoke(); // drop the placement so a revisited room won't respawn it
            _onDestroyed = null;    // fire once

            // Maybe drop a collectable (optional component on the decor prefab; rolls its own chance).
            if (TryGetComponent<CollectableDropper>(out var dropper)) dropper.TryDrop(transform.position);

            if (_collider != null) _collider.enabled = false;   // stop being hit / blocking at once
            if (_animator != null) _animator.SetTrigger(breakParam);

            _breaking = StartCoroutine(BreakThenRelease());
        }

        private IEnumerator BreakThenRelease()
        {
            yield return new WaitForSeconds(breakDuration);

            if (_collider != null) _collider.enabled = true;    // ready for the next reuse
            _breaking = null;
            if (_pool != null) _pool.Release(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
