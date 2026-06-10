using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A lightweight, reusable cooldown gate based on <see cref="Time.time"/>.
    /// A freshly-defaulted Cooldown is "ready" immediately, so no special seeding is needed.
    /// Usage: check <see cref="IsReady"/> to gate an action, then call <see cref="Begin"/> to start it.
    /// </summary>
    public struct Cooldown
    {
        private float _readyAt;

        public bool IsReady => Time.time >= _readyAt;

        public void Begin(float seconds) => _readyAt = Time.time + seconds;
    }
}
