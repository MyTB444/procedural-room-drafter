using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A spawned object that travels in a fixed direction and strikes things. Lets a spawner
    /// (player or enemy) launch a projectile without knowing the concrete type — see
    /// <see cref="Projectile"/> for the default implementation.
    /// </summary>
    public interface IProjectile
    {
        /// <summary>
        /// Send it flying along <paramref name="direction"/> (any magnitude — it's normalized).
        /// <paramref name="source"/> is the shooter, so the projectile can ignore its own owner.
        /// </summary>
        void Launch(Vector2 direction, GameObject source);
    }
}
