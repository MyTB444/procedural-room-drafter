using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Reusable melee-enemy brain + body. Wanders (idle ↔ roam, random cadence) until the player
    /// enters <see cref="EnemyStats.DetectionRadius"/>, then chases and, once within AttackRange,
    /// attacks on cooldown — halting in place for <see cref="EnemyStats.AttackHaltDuration"/> after
    /// each swing. Movement is full 2D; the 4-direction animation snaps diagonals to East/West.
    /// Implements the shared combat interfaces so the player's attacks damage and knock it back.
    ///
    /// Reuse: drop on any melee enemy with an EnemyStats asset + a child <see cref="AttackHitbox"/>.
    /// For other archetypes (e.g. ranged) subclass and override <see cref="OnAttack"/> / <see cref="Die"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class EnemyController : MonoBehaviour, IKnockbackable
    {
        [Header("References")]
        [Tooltip("Data asset with all tunable stats + AI settings.")]
        [SerializeField] private EnemyStats stats;

        [Tooltip("Melee hitbox swung on attack. Auto-found in children if empty.")]
        [SerializeField] private AttackHitbox attackHitbox;

        [Header("Death")]
        [Tooltip("How long the corpse fades out (seconds) before the object is disabled. The death " +
                 "knockback slides it during this window.")]
        [SerializeField, Min(0f)] private float deathFadeDuration = 0.5f;

        private enum State { Idle, Wander, Chase, Recover }

        private Rigidbody2D _body;
        private Health _health;
        private Vector2 _velocity;
        private Cooldown _attackCooldown;

        private State _state = State.Idle;
        private float _stateTimer;        // counts down idle / wander / recover durations
        private Vector2 _wanderDir;
        private Collider2D _collider;
        private bool _dying;              // in the knockback + fade death sequence
        private bool _seenPlayer;         // fired OnFirstDetected yet? (re-armed each spawn)
        private float _strikeLeft;        // > 0 during an attack's windup, before the hitbox lands
        private Vector2 _strikeDir;       // direction the pending strike was aimed

        private PrefabPool _pool;         // set when spawned from a pool; null if placed by hand
        private SpriteRenderer[] _renderers;
        private Color[] _originalColors;

        // ── Chase pathfinding (routes around water/walls via RoomNav) ──
        private const float RepathInterval = 0.25f;
        private List<Vector2Int> _path;
        private int _pathIndex;        // which waypoint we're currently steering toward
        private float _repathTimer;

        // ── Runtime state others can read / subscribe to ──
        public bool IsAlive => _health != null && _health.IsAlive;
        public bool IsMoving { get; private set; }
        public Vector2 FacingDirection { get; protected set; } = Vector2.down;

        public event Action AttackPerformed; // drives the Animator attack trigger

        /// <summary>The enemy's stats (for archetype subclasses).</summary>
        protected EnemyStats Stats => stats;

        /// <summary>The melee hitbox (child), for archetype subclasses that drive their own attack.</summary>
        protected AttackHitbox AttackHitbox => attackHitbox;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _collider = GetComponent<Collider2D>();
            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _originalColors[i] = _renderers[i].color;
            if (attackHitbox == null) attackHitbox = GetComponentInChildren<AttackHitbox>(true);
        }

        private void Start()
        {
            if (stats == null)
            {
                Debug.LogError($"[{nameof(EnemyController)}] No EnemyStats assigned on '{name}'.", this);
                enabled = false;
            }
        }

        // Reset on every (re)activation so pooled enemies come back fresh.
        private void OnEnable()
        {
            _health.Died += Die;
            if (stats != null) Initialize();
        }

        private void OnDisable()
        {
            _health.Died -= Die;
        }

        /// <summary>Called by a pool when this enemy is spawned. null = placed in the scene by hand.</summary>
        public void AssignPool(PrefabPool pool) => _pool = pool;

        private void Initialize()
        {
            _health.Init(stats.MaxHealth);
            _dying = false;
            _velocity = Vector2.zero;
            if (_body != null) _body.linearVelocity = Vector2.zero;
            if (_collider != null) _collider.enabled = true;
            RestoreColors();
            EnterIdle();
            _seenPlayer = false;
            _strikeLeft = 0f;
            OnInitialize();
        }

        /// <summary>Hook for subclasses to reset their own state on (re)activation (pool reuse).</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Called ONCE the first time the player enters DetectionRadius (re-armed on respawn).
        /// Hook for an on-sight reaction, e.g. an intro ability animation.</summary>
        protected virtual void OnFirstDetected() { }

        private void RestoreColors()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = _originalColors[i];
        }

        private void FixedUpdate()
        {
            if (stats == null) return;
            float dt = Time.fixedDeltaTime;

            // Dying: AI is over — just coast the death knockback to a stop while the fade runs.
            if (_dying)
            {
                _velocity = Vector2.MoveTowards(_velocity, Vector2.zero, stats.Deceleration * dt);
                _body.linearVelocity = _velocity;
                return;
            }

            if (!IsAlive) return;

            // A subclass action (e.g. a charge) can take over movement for several frames, applying
            // its velocity DIRECTLY (no accel/decel easing) and bypassing the normal FSM.
            if (OverrideMovement(dt, out var forced))
            {
                _velocity = forced;
                _body.linearVelocity = forced;
                IsMoving = forced.sqrMagnitude > 0.01f;
                return;
            }

            Vector2 targetVelocity = DecideMovement(dt);
            float rate = targetVelocity.sqrMagnitude > 0.0001f ? stats.Acceleration : stats.Deceleration;
            _velocity = Vector2.MoveTowards(_velocity, targetVelocity, rate * dt);
            _body.linearVelocity = _velocity;
            // Drive IsMoving off the INTENT (target), not the eased velocity — otherwise it dips below the
            // threshold during turns/reversals and while decelerating, flickering the Move bool and
            // restarting the walk animation.
            IsMoving = targetVelocity.sqrMagnitude > 0.0001f;
        }

        /// <summary>The state machine: returns the target velocity for this tick.</summary>
        private Vector2 DecideMovement(float dt)
        {
            // Attacking/recover: stand still through the attack — wait out the windup, THEN play the
            // attack animation + land the hitbox, then finish the post-attack halt.
            if (_state == State.Recover)
            {
                if (_strikeLeft > 0f)
                {
                    _strikeLeft -= dt;
                    if (_strikeLeft <= 0f) FireAttack(_strikeDir); // windup done → attack anim + hit
                }
                _stateTimer -= dt;
                if (_stateTimer > 0f) return Vector2.zero;
                _state = State.Chase; // done — re-evaluate below
            }

            bool hasPlayer = PlayerLocator.TryGetPosition(out Vector2 playerPos);

            // First sight latches detection ON: once the enemy has seen the player it stays locked on
            // (never loses detection from distance) for the rest of its life — only a dead/absent player
            // (or respawn, which re-arms _seenPlayer) drops it.
            if (hasPlayer && !_seenPlayer
                && Vector2.Distance(transform.position, playerPos) <= stats.DetectionRadius)
            {
                _seenPlayer = true;
                OnFirstDetected();
            }

            bool detected = hasPlayer && _seenPlayer;

            if (detected)
            {
                Vector2 toPlayer = playerPos - (Vector2)transform.position;

                // Within attack range → face the player and HOLD position: attack if cooldown ready
                // (the attack itself opens with a halt/windup, see DoAttack), otherwise wait out the
                // cooldown (don't keep pushing into the player while it's on cooldown).
                if (toPlayer.magnitude <= stats.AttackRange)
                {
                    FacingDirection = toPlayer.normalized;
                    if (_attackCooldown.IsReady) DoAttack(toPlayer.normalized);
                    return Vector2.zero;
                }

                if (_state != State.Chase) _repathTimer = 0f; // repath immediately on entering chase
                _state = State.Chase;

                Vector2 move = ChaseToward(playerPos, dt); // routes around water via RoomNav
                FacingDirection = move != Vector2.zero ? move : toPlayer.normalized;
                return move * stats.MoveSpeed;
            }

            // Player out of range → idle/wander cycle (drop out of a chase first).
            if (_state == State.Chase) EnterIdle();
            return Wander(dt);
        }

        private Vector2 Wander(float dt)
        {
            _stateTimer -= dt;
            if (_stateTimer <= 0f)
            {
                if (_state == State.Wander) EnterIdle();
                else EnterWander();
            }

            if (_state == State.Wander)
            {
                FacingDirection = _wanderDir;
                return _wanderDir * (stats.MoveSpeed * stats.WanderSpeedMultiplier);
            }
            return Vector2.zero; // idle
        }

        /// <summary>
        /// Unit heading toward the player along an A* path (recomputed every <see cref="RepathInterval"/>),
        /// so the enemy routes around water/walls. Falls back to a straight heading when there's no
        /// nav grid or no path (physics still blocks it at the obstacle).
        /// </summary>
        private Vector2 ChaseToward(Vector2 playerPos, float dt)
        {
            Vector2 here = transform.position;
            var nav = RoomNav.Current;
            if (nav == null) return Heading(playerPos - here);

            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                _repathTimer = RepathInterval;
                _path = nav.FindPath(nav.CellOf(here), nav.CellOf(playerPos));
                _pathIndex = 0;
            }

            if (_path == null || _path.Count == 0) return Heading(playerPos - here); // unreachable → straight

            // Advance past waypoints we've reached, then steer toward the next — so we keep flowing along
            // the path instead of stalling on a waypoint between repaths.
            Vector2Int cell = nav.CellOf(here);
            while (_pathIndex < _path.Count - 1 && _path[_pathIndex] == cell) _pathIndex++;
            return Heading((Vector2)nav.WorldOf(_path[_pathIndex]) - here);
        }

        private static Vector2 Heading(Vector2 v) => v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;

        private void EnterIdle()
        {
            _state = State.Idle;
            _stateTimer = UnityEngine.Random.Range(stats.IdleDurationRange.x, stats.IdleDurationRange.y);
        }

        private void EnterWander()
        {
            _state = State.Wander;
            _stateTimer = UnityEngine.Random.Range(stats.WanderDurationRange.x, stats.WanderDurationRange.y);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _wanderDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>Triggered when the player is in range and the cooldown is ready. Default: an instant
        /// melee swing + a recover halt. Override for a different attack (e.g. a multi-frame charge) —
        /// then begin the cooldown via <see cref="BeginAttackCooldown"/> and drive the action through
        /// <see cref="OverrideMovement"/>.</summary>
        protected virtual void DoAttack(Vector2 dir)
        {
            FacingDirection = dir;
            BeginAttackCooldown();

            // Windup FIRST: stand still (halt) for AttackWindup, THEN play the attack animation + land
            // the hitbox together. Then hold the rest of AttackHaltDuration for the recovery.
            _strikeDir = dir;
            _strikeLeft = stats.AttackWindup;
            if (_strikeLeft <= 0f) FireAttack(dir); // no windup → swing immediately

            _state = State.Recover;
            _stateTimer = stats.AttackWindup + stats.AttackHaltDuration;
            _velocity = Vector2.zero;
        }

        /// <summary>Play the attack animation and land the hit (<see cref="OnAttack"/>) — fired
        /// after the windup; also callable by subclasses that time their own shot (e.g. BigHead
        /// fires the moment its flee ends).</summary>
        protected void FireAttack(Vector2 dir)
        {
            AttackPerformed?.Invoke();
            OnAttack(dir);
        }

        /// <summary>Start the attack cooldown (so the next attack waits AttackCooldown seconds).</summary>
        protected void BeginAttackCooldown() => _attackCooldown.Begin(stats.AttackCooldown);

        /// <summary>Lets a subclass take over movement for a multi-frame action (e.g. a charge): return
        /// true and set <paramref name="velocity"/> to apply DIRECTLY this tick (no easing), skipping the
        /// normal FSM. Default: no override.</summary>
        protected virtual bool OverrideMovement(float dt, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            return false;
        }

        /// <summary>What an attack actually does. Default: swing the melee hitbox toward the player.
        /// Override for other archetypes (e.g. fire a projectile).</summary>
        protected virtual void OnAttack(Vector2 dir)
        {
            if (attackHitbox != null) attackHitbox.Strike(dir, stats.Damage, gameObject);
        }

        /// <summary>Whether knockback moves this enemy. Override → false for an unstoppable enemy that's
        /// immune to being pushed (also no death-slide).</summary>
        protected virtual bool Knockbackable => true;

        /// <summary><see cref="IKnockbackable"/> — shove as a pure impulse (no stun). Damage is the
        /// <see cref="Health"/> component's job. Still applies during the death sequence so the
        /// killing blow knocks the corpse back as it fades. No-op when <see cref="Knockbackable"/> is false.</summary>
        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (!Knockbackable || force <= 0f || direction.sqrMagnitude < 0.0001f) return;
            if (!IsAlive && !_dying) return;
            _velocity = direction.normalized * force;
        }

        /// <summary>Runs when Health reports death (subscribed in OnEnable): knockback-and-fade,
        /// then back to the pool. Override to customise the death visual.</summary>
        protected virtual void Die()
        {
            if (_dying) return;
            _dying = true;
            if (_collider != null) _collider.enabled = false; // stop blocking / colliding while dying
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            var startColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                startColors[i] = _renderers[i] != null ? _renderers[i].color : Color.clear;

            for (float t = 0f; t < deathFadeDuration; t += Time.deltaTime)
            {
                float k = 1f - (t / deathFadeDuration);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    var c = startColors[i];
                    c.a = startColors[i].a * k;
                    _renderers[i].color = c;
                }
                yield return null;
            }

            // Back to the pool for reuse (OnEnable re-initialises it on next spawn); or just disable.
            if (_pool != null) _pool.Release(gameObject);
            else gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (stats == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, stats.DetectionRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, stats.AttackRange);
        }
    }
}
