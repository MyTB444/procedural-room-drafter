using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A generic projectile usable by the player AND enemies: once launched it flies straight along
    /// its direction at a fixed speed, and on contact deals damage (+ optional knockback) through
    /// the shared combat interfaces. Point it and call <see cref="Launch"/>.
    ///
    /// Hit detection is a physics overlap QUERY of its collider's shape (like <see cref="AttackHitbox"/>),
    /// NOT trigger callbacks — so a target body's collision/exclude-layer setup can't silently
    /// block the hit; only <see cref="hitMask"/> decides what it can touch.
    ///
    /// Setup: put this on a prefab with a Rigidbody2D (added automatically). A Collider2D set to
    /// <b>Is Trigger</b> defines the hit shape; with NO collider the bullet instead hits by a
    /// circle query of <see cref="noColliderHitRadius"/> around itself (zero setup). Tune
    /// damage/speed per prefab; spawners only supply direction + owner.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour, IProjectile
    {
        private static readonly Collider2D[] QueryHits = new Collider2D[8];
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

        [Tooltip("Angle the ART is drawn facing: 0 = East/right, 90 = North/up. Used to align the " +
                 "sprite with the travel direction.")]
        [SerializeField] private float spriteForwardAngle = 0f;

        [Tooltip("Hit radius used when the prefab has NO Collider2D — the bullet then hits by a " +
                 "circle query around itself. Ignored when a collider exists (its shape is used).")]
        [SerializeField, Min(0.05f)] private float noColliderHitRadius = 0.3f;

        private Rigidbody2D _body;
        private Collider2D _collider;
        private Vector2 _direction;
        private GameObject _source;
        private float _timeLeft;
        private bool _launched;
        private readonly System.Collections.Generic.HashSet<Collider2D> _alreadyHit =
            new System.Collections.Generic.HashSet<Collider2D>(); // piercing bullets hit each target once

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            // Physics steps at 50 Hz; without interpolation the sprite visibly stutters on faster
            // displays. Always interpolate — projectiles are pure visual movers.
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _collider = GetComponent<Collider2D>();
        }

        /// <summary>Override the per-prefab damage before launch — e.g. the player's bullet dealing
        /// the CURRENT attack stat (upgrades included) instead of a fixed number.</summary>
        public void SetDamage(float amount) => damage = amount;

        /// <summary>Multiply the per-prefab damage before launch — e.g. an enemy bullet scaled by
        /// the room layer's <see cref="EnemyController.DamageScale"/>.</summary>
        public void ScaleDamage(float multiplier) => damage *= Mathf.Max(0f, multiplier);

        /// <inheritdoc />
        public void Launch(Vector2 direction, GameObject source)
        {
            _source = source;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _timeLeft = lifetime;
            _launched = true;
            _alreadyHit.Clear();

            // Point the sprite along travel, compensating for the direction the art is drawn facing.
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg - spriteForwardAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _body.linearVelocity = _direction * speed;
        }

        private void FixedUpdate()
        {
            if (!_launched) return;

            _body.linearVelocity = _direction * speed; // hold a constant heading & speed

            CheckHits();

            _timeLeft -= Time.fixedDeltaTime;
            if (_timeLeft <= 0f) Destroy(gameObject);
        }

        // Query-based contact test (immune to collision-matrix / exclude-layer setups — only the
        // hitMask matters): overlap the collider's shape — or, with no collider on the prefab, a
        // circle of noColliderHitRadius around the bullet — and strike what it covers.
        private void CheckHits()
        {
            int count;
            if (_collider != null)
            {
                count = HitboxQuery.Overlap(_collider, hitMask, QueryHits);
            }
            else
            {
                var filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(hitMask);
                count = Physics2D.OverlapCircle(transform.position, noColliderHitRadius, filter, QueryHits);
            }
            for (int i = 0; i < count; i++)
            {
                var other = QueryHits[i];
                if (other == null || !_alreadyHit.Add(other)) continue; // once per target
                if (_source != null && other.transform.IsChildOf(_source.transform)) continue; // never hit the shooter

                CombatHit.Apply(other.gameObject, new DamageInfo(damage, _direction, _source), knockback);

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
