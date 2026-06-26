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
        private float _duration; // length of the most recent Begin, for Remaining01

        public bool IsReady => Time.time >= _readyAt;

        public void Begin(float seconds)
        {
            _duration = seconds;
            _readyAt = Time.time + seconds;
        }

        /// <summary>Fraction of the most-recent cooldown still left: 1 right after <see cref="Begin"/>,
        /// easing to 0 when ready (0 if never begun or already elapsed). Captures the duration at Begin
        /// time, so it stays correct even when the cooldown length is changed between uses.</summary>
        public float Remaining01 => _duration > 0f ? Mathf.Clamp01((_readyAt - Time.time) / _duration) : 0f;
    }
}
