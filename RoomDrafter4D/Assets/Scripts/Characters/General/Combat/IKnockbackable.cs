using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Anything that can be shoved by a hit. Kept separate from <see cref="IDamageable"/> on
    /// purpose: an immovable target can take damage without being pushed, and a bump pad can knock
    /// back without dealing damage. Attackers apply the two independently.
    /// </summary>
    public interface IKnockbackable
    {
        /// <summary>Shove this object along <paramref name="direction"/> with the given impulse force.</summary>
        void ApplyKnockback(Vector2 direction, float force);
    }
}
