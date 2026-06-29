using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Turns the owner's OWN body collider into an attack hitbox while ACTIVE (driven by the controller
    /// during a dash). Each physics step it overlap-queries the body shape against <c>hitMask</c> via
    /// <see cref="HitboxQuery"/> and damages every <see cref="IDamageable"/> once per activation (deduped)
    /// through <see cref="CombatHit"/> — knocking each target away from the owner. Like
    /// <see cref="AttackHitbox"/> it QUERIES rather than relying on triggers, so the body's collision /
    /// exclude layers don't block hits. No aiming — the whole body is the hitbox.
    ///
    /// Setup: drop it on the character that owns the body Collider2D (the player). The controller calls
    /// <see cref="SetActive"/> each physics step with whether it's dashing; set <c>hitMask</c> to the
    /// layers the dash should hit (e.g. the enemy layer).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DashHitbox : MonoBehaviour
    {
        [Tooltip("Knockback force applied to anything the dash hits (0 = none).")]
        [SerializeField, Min(0f)] private float knockback = 0f;

        [Tooltip("Layers the dash can hit (e.g. the enemy layer).")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Tooltip("Extra reach (units) added to the body shape so a target that's merely TOUCHING (two " +
                 "solid bodies kept apart by collision) still gets hit. Raise if hits are being missed.")]
        [SerializeField, Min(0f)] private float reach = 0.2f;

        private static readonly Collider2D[] OverlapBuffer = new Collider2D[16];

        private Collider2D _collider;
        private GameObject _owner;
        private float _damage;
        private bool _active;
        private readonly HashSet<IDamageable> _hitThisDash = new HashSet<IDamageable>();

        private void Awake() => _collider = GetComponent<Collider2D>();

        /// <summary>
        /// Drive the hitbox from the dash state each physics step. On the RISING edge (dash starts) it
        /// re-arms: clears the per-dash hit set and records the owner/damage. Falling edge just stops it.
        /// </summary>
        public void SetActive(bool active, GameObject owner, float damage)
        {
            if (active && !_active)
            {
                _hitThisDash.Clear();
                _owner = owner;
                _damage = damage;
            }
            _active = active;
        }

        private void FixedUpdate()
        {
            if (!_active) return;

            int count = HitboxQuery.Overlap(_collider, hitMask, OverlapBuffer, reach);
            for (int i = 0; i < count; i++)
            {
                var other = OverlapBuffer[i];
                if (_owner != null && other.transform.IsChildOf(_owner.transform)) continue; // never the dasher
                if (!other.TryGetComponent<IDamageable>(out var target) || !target.IsAlive) continue;
                if (!_hitThisDash.Add(target)) continue; // already hit this dash

                // Knock the target away from the owner (the dash's push-through direction).
                Vector2 dir = _owner != null
                    ? (Vector2)(other.transform.position - _owner.transform.position)
                    : Vector2.zero;
                CombatHit.Apply(other.gameObject, new DamageInfo(_damage, dir, _owner), knockback);
            }
        }
    }
}
