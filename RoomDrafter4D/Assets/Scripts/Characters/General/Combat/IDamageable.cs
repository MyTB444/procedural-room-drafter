using UnityEngine;

namespace TRV
{
    /// <summary>
    /// One damage event: how much, and the world-space direction the hit came along (attacker →
    /// target, used for hit reactions / knockback). Passed by readonly-ref so callers build it once
    /// and hand it around without copies. Knockback is intentionally NOT part of this — it's a
    /// separate concern (see <see cref="IKnockbackable"/>) so a target can be hurt without being
    /// pushed, and vice versa.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector2 HitDirection; // unit; Vector2.zero if unspecified
        public readonly GameObject Source;     // who dealt it (may be null)

        public DamageInfo(float amount, Vector2 hitDirection = default, GameObject source = null)
        {
            Amount = amount;
            HitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.zero;
            Source = source;
        }
    }

    /// <summary>
    /// Anything that can be hurt — the player, enemies, breakables. The shared contract attackers
    /// (melee, <see cref="Projectile"/>, hazards) use so they don't care what they're hitting.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>False once dead — attackers should skip targets that already returned false.</summary>
        bool IsAlive { get; }

        /// <summary>Apply a hit. Implementations decide death, i-frames, etc.</summary>
        void TakeDamage(in DamageInfo info);
    }
}
