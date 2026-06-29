using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A reusable melee hitbox: a child object whose Collider2D defines the swing's SHAPE.
    /// <see cref="Strike"/> aims it along a direction and arms it for a brief window; while live it
    /// runs a physics OVERLAP QUERY over that shape each frame and hits every <see cref="IDamageable"/>
    /// once (deduped per swing) through <see cref="CombatHit"/>. Querying (rather than relying on
    /// trigger callbacks) means the hitbox is immune to the attacker body's collision settings —
    /// e.g. a Rigidbody2D that excludes the player layer so it doesn't shove them still lets the
    /// attack land. Drop it on the player AND every enemy; each shapes/sizes its own collider.
    ///
    /// Setup: put this on a CHILD of the attacker, with a Collider2D (Box/Circle/Capsule) shaped
    /// like the swing — authored around the child's local origin reaching out along +X (the
    /// component rotates +X toward the strike). The collider stays disabled; only its shape is read.
    /// Set <c>hitMask</c> to the layers this attacker should hit.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AttackHitbox : MonoBehaviour
    {
        [Tooltip("How far in front of the attacker the hitbox sits, along the strike direction. " +
                 "0 = centred on the attacker; raise it to push the swing further out.")]
        [SerializeField, Min(0f)] private float offset = 0.4f;

        [Tooltip("Windup before the hitbox goes live (seconds). 0 = instant. The swing is aimed at " +
                 "the moment of attack; it becomes active after this delay.")]
        [SerializeField, Min(0f)] private float delay = 0f;

        [Tooltip("Seconds the hitbox stays live per swing (after the delay).")]
        [SerializeField, Min(0.01f)] private float activeSeconds = 0.12f;

        [Tooltip("Knockback force applied to anything hit (0 = none).")]
        [SerializeField, Min(0f)] private float knockback = 0f;

        [Tooltip("Layers the swing can hit (e.g. the player layer for an enemy's hitbox).")]
        [SerializeField] private LayerMask hitMask = ~0;

        private static readonly Collider2D[] OverlapBuffer = new Collider2D[16];

        private Collider2D _collider;
        private GameObject _owner;
        private Vector2 _strikeDir = Vector2.right;
        private float _damage;
        private float _delayLeft;       // > 0 during windup, before the hitbox goes live
        private float _activeTimeLeft;  // > 0 while the hitbox is live
        private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _collider.enabled = false; // shape only — detection is via overlap queries
        }

        /// <summary>
        /// Swing along <paramref name="direction"/>, dealing <paramref name="damage"/> to whatever it
        /// overlaps for the active window. <paramref name="owner"/> (the attacker) is never hit.
        /// </summary>
        public void Strike(Vector2 direction, float damage, GameObject owner)
        {
            Aim(direction, damage, owner);
            _delayLeft = delay;
            _activeTimeLeft = delay > 0f ? 0f : activeSeconds;
        }

        /// <summary>Strike that stays live for a CUSTOM duration with no windup — for a sustained hit like
        /// a charge (the hitbox follows the moving attacker and hits each target once over the window).</summary>
        public void Strike(Vector2 direction, float damage, GameObject owner, float activeDuration)
        {
            Aim(direction, damage, owner);
            _delayLeft = 0f;
            _activeTimeLeft = Mathf.Max(0f, activeDuration);
        }

        /// <summary>Stop an in-progress swing immediately (e.g. the attacker dies mid-charge).</summary>
        public void Cancel()
        {
            _delayLeft = 0f;
            _activeTimeLeft = 0f;
        }

        private void Aim(Vector2 direction, float damage, GameObject owner)
        {
            _owner = owner;
            _damage = damage;
            _strikeDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

            transform.localPosition = _strikeDir * offset;
            transform.right = _strikeDir; // rotate the authored +X shape toward the strike

            _hitThisSwing.Clear();
        }

        private void Update()
        {
            if (_delayLeft > 0f)
            {
                _delayLeft -= Time.deltaTime;
                if (_delayLeft <= 0f) _activeTimeLeft = activeSeconds;
                return;
            }

            if (_activeTimeLeft <= 0f) return;
            _activeTimeLeft -= Time.deltaTime;
            QueryHits();
        }

        /// <summary>Overlap the hitbox shape against the hit mask and damage each new target once.</summary>
        private void QueryHits()
        {
            int count = HitboxQuery.Overlap(_collider, hitMask, OverlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var other = OverlapBuffer[i];
                if (_owner != null && other.transform.IsChildOf(_owner.transform)) continue; // never the attacker
                if (!other.TryGetComponent<IDamageable>(out var target) || !target.IsAlive) continue;
                if (!_hitThisSwing.Add(target)) continue; // already hit this swing

                CombatHit.Apply(other.gameObject, new DamageInfo(_damage, _strikeDir, _owner), knockback);
            }
        }
    }
}
