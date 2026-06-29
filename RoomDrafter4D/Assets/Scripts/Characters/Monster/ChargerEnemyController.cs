using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A charging melee enemy: wanders/chases exactly like <see cref="EnemyController"/>, but its attack
    /// is a 3-phase CHARGE instead of an instant swing — WINDUP (Start anim, stands still) → CHARGE
    /// (Attack-loop anim, lunges toward the player at <see cref="chargeSpeed"/> while its body deals
    /// damage via the child <see cref="AttackHitbox"/>, struck for the lunge so it sweeps through the
    /// player) → STOP (End anim, halts). The charge lunges in the EXACT direction toward the player at
    /// windup (any direction); the animator snaps facing to 4-way just for the clip. Cooldown spaces charges.
    ///
    /// Setup: drop on an enemy with an <see cref="EnemyStats"/> asset, a child <see cref="AttackHitbox"/>
    /// (hitMask = the player layer; author its offset/shape to reach in front of the charge) and a
    /// <see cref="ChargerEnemyAnimator"/>.
    /// </summary>
    public class ChargerEnemyController : EnemyController
    {
        [Header("Charge")]
        [Tooltip("Wind-up before the lunge (seconds) — match the Start anim length.")]
        [SerializeField, Min(0f)] private float chargeWindup = 0.4f;

        [Tooltip("Lunge speed during the charge (units/sec). Set well above MoveSpeed.")]
        [SerializeField, Min(0f)] private float chargeSpeed = 12f;

        [Tooltip("How long the lunge lasts (seconds).")]
        [SerializeField, Min(0f)] private float chargeDuration = 0.5f;

        [Tooltip("Halt after the lunge (seconds) — match the End anim length.")]
        [SerializeField, Min(0f)] private float chargeStopDuration = 0.3f;

        private enum Phase { None, Windup, Charge, Stop }
        private Phase _phase = Phase.None;
        private float _phaseTimer;
        private Vector2 _chargeDir = Vector2.down;

        /// <summary>Fires when a charge begins (passes the charge dir) → animator sets Attack true.</summary>
        public event System.Action<Vector2> ChargeStarted;
        /// <summary>Fires when the lunge ends (entering the stop) → animator sets Attack false (plays End).</summary>
        public event System.Action ChargeEnded;

        protected override void OnInitialize() => EndCharge(); // reset on (re)spawn

        // Start a charge instead of an instant swing; the base only calls this when in range + ready.
        protected override void DoAttack(Vector2 dir)
        {
            BeginAttackCooldown();
            _chargeDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down; // lunge toward the player (any direction)
            FacingDirection = _chargeDir;
            _phase = Phase.Windup;
            _phaseTimer = chargeWindup;
            ChargeStarted?.Invoke(_chargeDir);
        }

        // Drives the 3-phase charge; returns true (taking over movement) until the stop finishes.
        protected override bool OverrideMovement(float dt, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (_phase == Phase.None) return false;

            FacingDirection = _chargeDir; // keep facing the locked charge dir through the whole sequence
            _phaseTimer -= dt;

            switch (_phase)
            {
                case Phase.Windup:
                    if (_phaseTimer <= 0f)
                    {
                        _phase = Phase.Charge;
                        _phaseTimer = chargeDuration;
                        // Arm the child hitbox for the whole lunge (it follows the body, hits once).
                        if (AttackHitbox != null)
                            AttackHitbox.Strike(_chargeDir, Stats != null ? Stats.Damage : 0f, gameObject, chargeDuration);
                    }
                    return true; // stand still during the wind-up

                case Phase.Charge:
                    velocity = _chargeDir * chargeSpeed;
                    if (_phaseTimer <= 0f)
                    {
                        _phase = Phase.Stop;
                        _phaseTimer = chargeStopDuration;
                        ChargeEnded?.Invoke(); // Attack=false → the End/stop anim plays during the halt
                    }
                    return true;

                case Phase.Stop:
                    if (_phaseTimer <= 0f) { _phase = Phase.None; return false; } // hand back to the FSM
                    return true; // halt while the End anim plays

                default:
                    return false;
            }
        }

        protected override void Die()
        {
            EndCharge(); // stop charging / damaging if killed mid-charge
            base.Die();
        }

        private void EndCharge()
        {
            _phase = Phase.None;
            if (AttackHitbox != null) AttackHitbox.Cancel();
            ChargeEnded?.Invoke();
        }
    }
}
