using System;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Health + damage for any character (player or enemy) — the shared <see cref="IDamageable"/>.
    /// Owns current/max health, fires <see cref="Changed"/>/<see cref="Died"/>, and flashes the
    /// character's <see cref="HitFlash"/> on a NON-lethal hit (the killing blow is left unflashed so
    /// a death visual — e.g. the enemy fade — owns the sprite from then on). Knockback is separate
    /// (<see cref="IKnockbackable"/> on the controller, since it drives movement). Controllers call
    /// <see cref="Init"/> on spawn/pool-reuse and subscribe to <see cref="Died"/> for death behaviour.
    /// Auto-added via the controllers' <c>RequireComponent</c>, so no manual wiring.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        /// <summary>While true, incoming damage is ignored (e.g. dash i-frames). Set by the owner.</summary>
        public bool Invincible { get; set; }

        public event Action<float> Changed; // new current health
        public event Action Died;

        private HitFlash _flash;

        private void Awake() => _flash = GetComponentInChildren<HitFlash>(true);

        /// <summary>(Re)start at full health for the given maximum — call on spawn / pool reuse.</summary>
        public void Init(float max)
        {
            Max = max;
            Current = max;
            if (_flash != null) _flash.enabled = true; // re-arm after a death (pool reuse)
            Changed?.Invoke(Current);
        }

        /// <summary>Change the maximum WITHOUT resetting to full (mid-run max-HP upgrade): an
        /// increase also heals by the added amount; a decrease clamps Current. No-op while dead.</summary>
        public void SetMax(float newMax)
        {
            if (!IsAlive || newMax <= 0f || Mathf.Approximately(newMax, Max)) return;
            float delta = newMax - Max;
            Max = newMax;
            Current = delta > 0f ? Current + delta : Mathf.Min(Current, Max);
            Changed?.Invoke(Current);
        }

        public void TakeDamage(in DamageInfo info) => TakeDamage(info.Amount);

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f || Invincible) return;
            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current);

            if (Current > 0f)
            {
                if (_flash != null) _flash.Flash();
            }
            else
            {
                // Killing blow: stop any in-progress flash and hand the sprite to the death visual.
                if (_flash != null) _flash.enabled = false;
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current);
        }
    }
}
