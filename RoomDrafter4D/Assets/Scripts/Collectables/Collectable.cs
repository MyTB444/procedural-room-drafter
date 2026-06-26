using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Base for a pooled pickup that DROPS from something (decor, enemies…), rests on the ground for a
    /// short delay, then flies to the player and is collected — at which point it applies its effect
    /// (<see cref="OnCollected"/>) and returns to its <see cref="CollectablePool"/>. The pool adds/reuses
    /// these; a concrete item (e.g. <see cref="HealthPotion"/>) only defines what collecting does.
    /// Collection is by proximity to a LIVING player, so no collider is required.
    /// </summary>
    public abstract class Collectable : MonoBehaviour
    {
        [Header("Drop → ground → fly")]
        [Tooltip("How long the pickup rests on the ground before flying to the player (seconds).")]
        [SerializeField, Min(0f)] private float groundDelay = 0.4f;

        [Tooltip("Starting fly speed toward the player (units/sec).")]
        [SerializeField, Min(0f)] private float flySpeed = 8f;

        [Tooltip("Fly speed gain so it homes in faster over time (units/sec²). 0 = constant speed.")]
        [SerializeField, Min(0f)] private float flyAcceleration = 30f;

        [Tooltip("Collected when this close to the player (world units).")]
        [SerializeField, Min(0.01f)] private float collectRadius = 0.4f;

        private CollectablePool _pool;
        private float _groundLeft; // time left resting on the ground
        private float _speed;      // current fly speed
        private bool _collected;

        /// <summary>Called by the pool when (re)spawned: drop at a position, reset state, start the timer.</summary>
        public void Drop(CollectablePool pool, Vector3 position)
        {
            _pool = pool;
            transform.position = position;
            _groundLeft = groundDelay;
            _speed = flySpeed;
            _collected = false;
            OnDropped();
        }

        private void Update()
        {
            if (_collected) return;

            if (_groundLeft > 0f)
            {
                _groundLeft -= Time.deltaTime; // resting on the ground
                return;
            }

            if (!PlayerLocator.TryGetPosition(out var target)) return; // no living player → keep waiting

            _speed += flyAcceleration * Time.deltaTime;
            var pos = transform.position;
            var goal = new Vector3(target.x, target.y, pos.z);
            pos = Vector3.MoveTowards(pos, goal, _speed * Time.deltaTime);
            transform.position = pos;

            if ((goal - pos).sqrMagnitude <= collectRadius * collectRadius) Collect();
        }

        private void Collect()
        {
            _collected = true;
            OnCollected(PlayerLocator.Player);
            if (_pool != null) _pool.Release(gameObject);
            else gameObject.SetActive(false);
        }

        /// <summary>Apply the pickup's effect to the player. Called once when collected.</summary>
        protected abstract void OnCollected(TRVController player);

        /// <summary>Optional hook fired when (re)dropped — e.g. reset a spawn animation. No-op by default.</summary>
        protected virtual void OnDropped() { }
    }
}
