using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shared glue for landing a hit: applies damage and (optionally) knockback to whatever combat
    /// interfaces the target happens to implement. Used by every attacker — TRV's melee, enemy
    /// melee, <see cref="Projectile"/> — so the "check IDamageable, then IKnockbackable" dance
    /// lives in exactly one place.
    /// </summary>
    public static class CombatHit
    {
        /// <summary>
        /// Hit <paramref name="target"/> with <paramref name="damage"/>, plus a knockback of
        /// <paramref name="knockbackForce"/> along the damage's <see cref="DamageInfo.HitDirection"/>
        /// when the target supports it. Returns true if something damageable was actually hit.
        /// </summary>
        public static bool Apply(GameObject target, in DamageInfo damage, float knockbackForce = 0f)
        {
            bool damaged = false;

            if (target.TryGetComponent<IDamageable>(out var d) && d.IsAlive)
            {
                d.TakeDamage(damage);
                damaged = true;
            }

            if (knockbackForce > 0f && damage.HitDirection != Vector2.zero
                && target.TryGetComponent<IKnockbackable>(out var k))
            {
                k.ApplyKnockback(damage.HitDirection, knockbackForce);
            }

            return damaged;
        }
    }
}
