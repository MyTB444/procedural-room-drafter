using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A reusable melee hitbox: a child object with a trigger Collider2D that's normally OFF.
    /// <see cref="Strike"/> points it along a direction and switches it on for a brief active
    /// window; every <see cref="IDamageable"/> that overlaps during the window is hit once (deduped
    /// per swing) through <see cref="CombatHit"/>. Drop it on the player AND on every enemy — same
    /// component, but each can shape/size its own collider; the caller supplies the damage from its
    /// own stats.
    ///
    /// Setup: put this on a CHILD of the attacker (so it can be offset/rotated on its own), with a
    /// Collider2D shaped like the swing — authored around the child's local origin reaching out
    /// along +X (the component rotates +X toward the strike direction). It forces the collider to
    /// trigger + disabled in Awake. Set <c>hitMask</c> to the layers this attacker should hit.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AttackHitbox : MonoBehaviour
    {
        [Tooltip("How far in front of the attacker the hitbox sits, along the strike direction. " +
                 "0 = centred on the attacker; raise it to push the swing further out.")]
        [SerializeField, Min(0f)] private float offset = 0.4f;

        [Tooltip("Windup before the hitbox appears (seconds). 0 = instant. The swing is aimed at " +
                 "the moment of attack; the collider switches on after this delay.")]
        [SerializeField, Min(0f)] private float delay = 0f;

        [Tooltip("Seconds the hitbox stays live per swing (after the delay).")]
        [SerializeField, Min(0.01f)] private float activeSeconds = 0.12f;

        [Tooltip("Knockback force applied to anything hit (0 = none).")]
        [SerializeField, Min(0f)] private float knockback = 0f;

        [Tooltip("Layers the swing can hit (e.g. the enemy layer for the player's hitbox).")]
        [SerializeField] private LayerMask hitMask = ~0;

        private Collider2D _collider;
        private GameObject _owner;
        private Vector2 _strikeDir = Vector2.right;
        private float _damage;
        private float _delayLeft;       // > 0 during windup, before the hitbox switches on
        private float _activeTimeLeft;  // > 0 while the hitbox is live
        private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;
            _collider.enabled = false;
        }

        /// <summary>
        /// Swing along <paramref name="direction"/>, dealing <paramref name="damage"/> to whatever it
        /// overlaps for the active window. <paramref name="owner"/> (the attacker) is never hit.
        /// </summary>
        public void Strike(Vector2 direction, float damage, GameObject owner)
        {
            _owner = owner;
            _damage = damage;
            _strikeDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

            transform.localPosition = _strikeDir * offset;
            transform.right = _strikeDir; // rotate the authored +X shape toward the strike

            _hitThisSwing.Clear();
            _collider.enabled = false; // stays off through the windup

            if (delay > 0f)
            {
                _delayLeft = delay;
                _activeTimeLeft = 0f;
            }
            else
            {
                _delayLeft = 0f;
                _activeTimeLeft = activeSeconds;
                _collider.enabled = true;
            }
        }

        private void Update()
        {
            // Windup: count down, then switch the hitbox on for its active window.
            if (_delayLeft > 0f)
            {
                _delayLeft -= Time.deltaTime;
                if (_delayLeft <= 0f)
                {
                    _activeTimeLeft = activeSeconds;
                    _collider.enabled = true;
                }
                return;
            }

            if (_activeTimeLeft <= 0f) return;
            _activeTimeLeft -= Time.deltaTime;
            if (_activeTimeLeft <= 0f) _collider.enabled = false;
        }

        // Stay (not Enter) so a target already overlapping when the hitbox switches on still gets
        // hit; the per-swing set keeps it to one hit each.
        private void OnTriggerStay2D(Collider2D other)
        {
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0) return;
            if (_owner != null && other.transform.IsChildOf(_owner.transform)) return;
            if (!other.TryGetComponent<IDamageable>(out var target) || !target.IsAlive) return;
            if (!_hitThisSwing.Add(target)) return; // already hit this swing

            CombatHit.Apply(other.gameObject, new DamageInfo(_damage, _strikeDir, _owner), knockback);
        }
    }
}
