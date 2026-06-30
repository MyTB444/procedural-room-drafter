using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A two-stance enemy that wanders/chases like <see cref="EnemyController"/> but alternates between
    /// a hit-and-run stance and an aggressive stance, with an intro ABILITY the first time it sees the
    /// player. Built from the base hooks (`OnFirstDetected`, `DoAttack`, `OverrideMovement`) + the
    /// reusable <see cref="EnemyAttack"/> bundle (each attack = its own hitbox + damage).
    ///
    /// • On FIRST sight → pause and play the ability anim once (`OnFirstDetected`).
    /// • <b>Skirmish stance</b> (state 1): approach → <see cref="skirmishAttack"/> → FLEE the player for
    ///   <see cref="fleeDuration"/>s → switch to Aggressive.
    /// • <b>Aggressive stance</b> (state 2): approach → wind up (halt) for `EnemyStats.AttackWindup` →
    ///   <see cref="meleeAttack"/> (like the basic slime), repeated <see cref="aggressiveAttacks"/>
    ///   times → switch back to Skirmish. (Only the regular/melee attack winds up — the skirmish one doesn't.)
    ///
    /// Setup: an <see cref="EnemyStats"/> asset, TWO child <see cref="AttackHitbox"/>es (assigned to the
    /// two <see cref="EnemyAttack"/> slots, each hitMask = player layer), and a
    /// <see cref="SkirmisherEnemyAnimator"/>.
    /// </summary>
    public class SkirmisherEnemyController : EnemyController
    {
        [Header("Attacks (each = its own hitbox + damage)")]
        [Tooltip("Aggressive-stance attack (state 2), thrown repeatedly — like the basic slime's.")]
        [SerializeField] private EnemyAttack meleeAttack = new EnemyAttack();

        [Tooltip("Skirmish-stance attack (state 1's 'attack 2') — usually a bigger hitbox; then it flees.")]
        [SerializeField] private EnemyAttack skirmishAttack = new EnemyAttack();

        [Header("Behaviour")]
        [Tooltip("How many aggressive (melee) attacks before switching back to the skirmish stance.")]
        [SerializeField, Min(1)] private int aggressiveAttacks = 2;

        [Tooltip("Seconds to FINISH the skirmish attack (facing the player) before turning to flee — " +
                 "match the attack 2 anim length.")]
        [SerializeField, Min(0f)] private float skirmishAttackDuration = 0.5f;

        [Tooltip("Seconds the skirmisher runs away from the player after its skirmish attack.")]
        [SerializeField, Min(0f)] private float fleeDuration = 5f;

        [Tooltip("Flee speed = MoveSpeed × this.")]
        [SerializeField, Min(0f)] private float fleeSpeedMultiplier = 1f;

        [Tooltip("If it's run into a wall (barely moving while fleeing) for this long, it stops fleeing " +
                 "and switches to the aggressive stance.")]
        [SerializeField, Min(0f)] private float fleeWallSwapTime = 0.2f;

        [Tooltip("Seconds it pauses to play the intro ability anim the first time it sees the player.")]
        [SerializeField, Min(0f)] private float abilityDuration = 0.6f;

        private enum Stance { Skirmish, Aggressive } // state 1, state 2
        private Stance _stance;
        private int _aggressiveCount;
        private float _meleeWindupTimer;   // regular-attack windup (EnemyStats.AttackWindup); skirmish has none
        private float _meleeRecoverTimer;  // post-melee halt (EnemyStats.AttackHaltDuration) — no movement
        private Vector2 _meleeDir = Vector2.down;
        private float _skirmishHoldTimer; // finishing the skirmish attack before fleeing
        private float _fleeTimer;
        private float _abilityTimer;
        private Vector2 _fleeDir = Vector2.down;
        private Vector2 _fleeLastPos;     // for wall-stuck detection while fleeing
        private float _fleeStuckTime;

        /// <summary>Which attack fired: 0 = melee (state 2), 1 = skirmish (state 1) — drives the animator.</summary>
        public event System.Action<int> Attacked;
        /// <summary>The intro ability was used (first sight) — drives the animator's Ability trigger.</summary>
        public event System.Action AbilityUsed;

        protected override bool Knockbackable => false; // immune to being pushed

        protected override void OnInitialize()
        {
            _stance = Stance.Skirmish;
            _aggressiveCount = 0;
            _meleeWindupTimer = 0f;
            _meleeRecoverTimer = 0f;
            _skirmishHoldTimer = 0f;
            _fleeTimer = 0f;
            _fleeStuckTime = 0f;
            _abilityTimer = 0f;
        }

        // Intro ability the first time the player is seen: pause + play the ability anim once.
        protected override void OnFirstDetected()
        {
            _abilityTimer = abilityDuration;
            AbilityUsed?.Invoke();
        }

        // Triggered by the base when in range + cooldown ready — perform the current stance's attack.
        protected override void DoAttack(Vector2 dir)
        {
            BeginAttackCooldown();
            FacingDirection = dir;

            if (_stance == Stance.Aggressive)
            {
                // Regular attack: WIND UP (halt) for EnemyStats.AttackWindup, THEN the melee fires
                // (in OverrideMovement). The skirmish attack below has NO windup.
                _meleeDir = dir;
                _meleeWindupTimer = Stats != null ? Stats.AttackWindup : 0f;
                if (_meleeWindupTimer <= 0f) FireMelee(dir); // no windup → fire now
            }
            else // Skirmish (state 1): the bigger attack fires immediately, FINISH it, then flee
            {
                skirmishAttack.Perform(dir, gameObject);
                Attacked?.Invoke(1);
                _skirmishHoldTimer = skirmishAttackDuration; // hold facing the player until the attack finishes
            }
        }

        // Play the melee attack (animation + hitbox) after its windup; advances/switches the stance and
        // starts the post-attack halt (no movement during it).
        private void FireMelee(Vector2 dir)
        {
            meleeAttack.Perform(dir, gameObject);
            Attacked?.Invoke(0);
            _meleeRecoverTimer = Stats != null ? Stats.AttackHaltDuration : 0f;
            if (++_aggressiveCount >= aggressiveAttacks)
                _stance = Stance.Skirmish; // done being aggressive → back to state 1
        }

        // Drives the intro-ability hold and the skirmish flee; otherwise the base FSM (approach/attack).
        protected override bool OverrideMovement(float dt, out Vector2 velocity)
        {
            velocity = Vector2.zero;

            // Intro ability: stand still while it plays (once on first sight).
            if (_abilityTimer > 0f)
            {
                _abilityTimer -= dt;
                return true;
            }

            // Regular-attack windup: stand still (halt) for AttackWindup, THEN fire the melee.
            if (_meleeWindupTimer > 0f)
            {
                _meleeWindupTimer -= dt;
                if (_meleeWindupTimer <= 0f) FireMelee(_meleeDir);
                return true;
            }

            // Post-melee halt: stand still (no movement) through the AttackHaltDuration recovery.
            if (_meleeRecoverTimer > 0f)
            {
                _meleeRecoverTimer -= dt;
                return true;
            }

            // Finish the skirmish attack (hold still, still facing the player) before turning to flee.
            if (_skirmishHoldTimer > 0f)
            {
                _skirmishHoldTimer -= dt;
                if (_skirmishHoldTimer <= 0f)
                {
                    _fleeTimer = fleeDuration; // now turn away and run
                    _fleeDir = PlayerLocator.TryGetPosition(out var pp)
                        ? SafeDir((Vector2)transform.position - pp)
                        : _fleeDir;
                    _fleeLastPos = transform.position;
                    _fleeStuckTime = 0f;
                }
                return true; // hold during the attack 2 animation
            }

            // Skirmish flee: run away from the player for fleeDuration; if it runs INTO A WALL (barely
            // moving) for fleeWallSwapTime, stop early and go aggressive. Either way → aggressive at the end.
            if (_fleeTimer > 0f)
            {
                _fleeTimer -= dt;
                if (PlayerLocator.TryGetPosition(out var p))
                    _fleeDir = SafeDir((Vector2)transform.position - p); // keep fleeing the CURRENT position

                Vector2 pos = transform.position;
                float expected = Stats.MoveSpeed * fleeSpeedMultiplier * dt;
                if (Vector2.Distance(pos, _fleeLastPos) < expected * 0.5f) _fleeStuckTime += dt;
                else _fleeStuckTime = 0f;
                _fleeLastPos = pos;

                if (_fleeTimer <= 0f || _fleeStuckTime >= fleeWallSwapTime)
                {
                    _fleeTimer = 0f;
                    _stance = Stance.Aggressive;
                    _aggressiveCount = 0;
                    return true; // stop this tick (velocity stays zero)
                }

                FacingDirection = _fleeDir;
                velocity = _fleeDir * (Stats.MoveSpeed * fleeSpeedMultiplier);
                return true;
            }

            return false; // hand back to the base FSM (chase + the in-range attack gate)
        }

        private static Vector2 SafeDir(Vector2 v) => v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.down;
    }
}
