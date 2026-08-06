using UnityEngine;

namespace TRV
{
    /// <summary>
    /// One distinct enemy attack: a child <see cref="AttackHitbox"/> (its own size/shape/offset/knockback)
    /// plus the damage it deals. A multi-attack enemy holds several of these (e.g. a small fast jab and a
    /// big lunge) and performs whichever its behaviour chooses. Serializable so it's authored per-attack
    /// in the inspector. (NOTE: this [Serializable] name is asset data — renaming loses assignments.)
    /// </summary>
    [System.Serializable]
    public class EnemyAttack
    {
        [Tooltip("The hitbox swung for this attack — a child AttackHitbox with its own shape/size/offset.")]
        public AttackHitbox Hitbox;

        [Tooltip("Damage this attack deals.")]
        [Min(0f)] public float Damage = 1f;

        /// <summary>Swing this attack's hitbox along <paramref name="dir"/>. No-op if no hitbox is set.</summary>
        public void Perform(Vector2 dir, GameObject owner, float damageScale = 1f)
        {
            if (Hitbox != null) Hitbox.Strike(dir, Damage * damageScale, owner);
        }
    }
}
