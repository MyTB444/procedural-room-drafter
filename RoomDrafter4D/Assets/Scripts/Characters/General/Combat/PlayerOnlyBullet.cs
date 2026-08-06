using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A projectile that can ONLY hit the player — no Rigidbody2D, no Collider2D, no physics at
    /// all. It flies straight and each frame checks its DISTANCE to the living player
    /// (<see cref="PlayerLocator"/>); within <see cref="hitRadius"/> it deals damage (+ optional
    /// knockback) through the shared combat pipeline and self-destructs. Because there is no
    /// collider it needs no layer setup and passes through walls/water/decor by definition —
    /// the bullet for flying kiters like Big Head.
    ///
    /// Setup: a prefab with just a SpriteRenderer + this component (sprite drawn facing East).
    /// </summary>
    public class PlayerOnlyBullet : MonoBehaviour, IProjectile
    {
        [Header("Movement")]
        [Tooltip("Travel speed in units/second.")]
        [SerializeField, Min(0f)] private float speed = 8f;

        [Tooltip("Auto-destroy after this many seconds if it never reaches the player.")]
        [SerializeField, Min(0.1f)] private float lifetime = 5f;

        [Header("Impact")]
        [Tooltip("Damage dealt to the player on hit.")]
        [SerializeField, Min(0f)] private float damage = 10f;

        [Tooltip("Knockback force applied to the player (0 = none).")]
        [SerializeField, Min(0f)] private float knockback = 0f;

        [Tooltip("How close to the player's position counts as a hit.")]
        [SerializeField, Min(0.05f)] private float hitRadius = 0.4f;

        private Vector2 _direction;
        private GameObject _source;
        private float _timeLeft;
        private bool _launched;

        /// <summary>Multiply the per-prefab damage before launch — e.g. scaled by the room layer's
        /// <see cref="EnemyController.DamageScale"/>.</summary>
        public void ScaleDamage(float multiplier) => damage *= Mathf.Max(0f, multiplier);

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
        }

        private void Update()
        {
            if (!_launched) return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f) { Destroy(gameObject); return; }

            transform.position += (Vector3)(_direction * (speed * Time.deltaTime));

            // Proximity hit test against the LIVING player — no collider involved.
            if (!PlayerLocator.TryGetPosition(out var playerPos)) return;
            if (((Vector2)playerPos - (Vector2)transform.position).sqrMagnitude > hitRadius * hitRadius)
                return;

            var player = PlayerLocator.Player;
            if (player != null)
                CombatHit.Apply(player.gameObject, new DamageInfo(damage, _direction, _source), knockback);
            Destroy(gameObject);
        }
    }
}
