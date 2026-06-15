using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A generic projectile usable by the player AND enemies: once launched it flies straight along
    /// its direction at a fixed speed, and on contact deals damage (+ optional knockback) through
    /// the shared combat interfaces. Point it and call <see cref="Launch"/>.
    ///
    /// Setup: put this on a prefab with a Rigidbody2D (added automatically) and a Collider2D set to
    /// <b>Is Trigger</b>. Tune damage/speed per prefab; spawners only supply direction + owner.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour, IProjectile
    {
        [Header("Movement")]
        [Tooltip("Travel speed in units/second.")]
        [SerializeField, Min(0f)] private float speed = 12f;

        [Tooltip("Auto-destroy after this many seconds if it hits nothing.")]
        [SerializeField, Min(0f)] private float lifetime = 5f;

        [Header("Impact")]
        [Tooltip("Damage dealt to the first damageable it hits.")]
        [SerializeField, Min(0f)] private float damage = 10f;

        [Tooltip("Knockback force applied to the target (0 = none).")]
        [SerializeField, Min(0f)] private float knockback = 0f;

        [Tooltip("Layers the projectile reacts to (walls, characters). Its own shooter is always ignored.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Tooltip("Destroy the projectile when it hits something on the mask.")]
        [SerializeField] private bool destroyOnHit = true;

        private Rigidbody2D _body;
        private Vector2 _direction;
        private GameObject _source;
        private float _timeLeft;
        private bool _launched;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
        }

        /// <inheritdoc />
        public void Launch(Vector2 direction, GameObject source)
        {
            _source = source;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _timeLeft = lifetime;
            _launched = true;

            // Point the sprite along travel (sprite drawn facing East = 0°).
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _body.linearVelocity = _direction * speed;
        }

        private void FixedUpdate()
        {
            if (!_launched) return;

            _body.linearVelocity = _direction * speed; // hold a constant heading & speed

            _timeLeft -= Time.fixedDeltaTime;
            if (_timeLeft <= 0f) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_launched) return;
            if (_source != null && other.transform.IsChildOf(_source.transform)) return; // never hit the shooter
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0) return;

            CombatHit.Apply(other.gameObject, new DamageInfo(damage, _direction, _source), knockback);

            if (destroyOnHit) Destroy(gameObject);
        }
    }
}
