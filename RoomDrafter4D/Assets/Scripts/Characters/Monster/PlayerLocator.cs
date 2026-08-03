using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shared lookup for the player's location, used by enemy AI. Caches the single
    /// <see cref="TRVController"/> in the scene (re-finding it if it's been destroyed) so every
    /// enemy doesn't run its own FindObject. Pure static helper — no scene presence.
    /// </summary>
    public static class PlayerLocator
    {
        private static TRVController _player;

        /// <summary>The player's controller, or null if none is in the scene.</summary>
        public static TRVController Player
        {
            // Unity's fake-null makes this re-find after a destroyed/reloaded player too.
            get => _player != null ? _player : (_player = Object.FindAnyObjectByType<TRVController>());
        }

        /// <summary>The player's position, when a LIVING player exists.</summary>
        public static bool TryGetPosition(out Vector2 position)
        {
            var p = Player;
            if (p != null && p.IsAlive)
            {
                position = p.transform.position;
                return true;
            }
            position = default;
            return false;
        }
    }
}
