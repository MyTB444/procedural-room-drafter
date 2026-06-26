using System;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// The logic of TRV's actions. It owns no input bindings and no tuning numbers —
    /// it reads intent from <see cref="TRVInput"/> and tuning from <see cref="TRVStats"/>,
    /// then turns them into smooth 8-directional movement plus combat hooks.
    ///
    /// Movement feel: target velocity = direction × speed, and the current velocity is
    /// eased toward it with separate accelerate/decelerate rates. Diagonals are
    /// normalized so W+A is exactly as fast as W. Facing is tracked as one of the 8
    /// <see cref="CharacterDirection"/> values for animation/aiming to consume.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class TRVController : MonoBehaviour, IKnockbackable
    {
        [Header("References")]
        [Tooltip("The data asset with all tunable stats.")]
        [SerializeField] private TRVStats stats;

        [Tooltip("The input source. Auto-found on this GameObject if left empty.")]
        [SerializeField] private TRVInput input;

        [Tooltip("Camera used to turn the cursor position into a world aim direction. Defaults to Camera.main.")]
        [SerializeField] private Camera aimCamera;

        [Tooltip("Melee hitbox swung on attack. Auto-found in children if left empty.")]
        [SerializeField] private AttackHitbox attackHitbox;

        private Rigidbody2D _body;
        private Health _health;
        private Vector2 _velocity;        // our own smoothed velocity
        private Cooldown _attackCooldown;

        // ── Dash state ──
        private float _dashWindupLeft;    // > 0 during the pre-dash windup (no i-frames yet)
        private float _dashTimeLeft;      // > 0 while dashing (the burst — i-frames active)
        private Cooldown _dashCooldown;
        private Vector2 _dashDir;

        // ── Attack state ──
        private float _attackSlowTimeLeft; // > 0 while the attack swing slows movement

        // ── Facing history (for forgiving diagonal release) ──
        private CharacterDirection _previousFacing = CharacterDirection.South;
        private float _facingSetTime = float.NegativeInfinity;

        // ── Runtime state others can read / subscribe to ──
        public bool IsAlive => _health != null && _health.IsAlive;
        public Vector2 MoveDirection { get; private set; } // last non-zero unit heading
        public CharacterDirection Facing { get; private set; } = CharacterDirection.South;
        public bool IsMoving { get; private set; }
        public bool IsDashing => _dashTimeLeft > 0f;

        /// <summary>No damage or knockback lands while true. Granted by the dash — and lost the
        /// instant an attack cancels the dash, so an attack-cancelled dodge ends invincibility.</summary>
        public bool IsInvincible => IsDashing;

        /// <summary>8-way direction of the most recent attack, aimed at the cursor (independent of movement).</summary>
        public CharacterDirection AttackDirection { get; private set; } = CharacterDirection.South;

        /// <summary>Fraction of the attack cooldown still remaining (1 = just attacked, 0 = ready to swing).
        /// Reflects the CURRENT cooldown length, so it stays accurate if an effect changes it — for UI.</summary>
        public float AttackCooldownRemaining01 => _attackCooldown.Remaining01;

        public event Action<CharacterDirection> FacingChanged;
        public event Action<CharacterDirection> DashStarted; // passes the 8-way dash direction
        public event Action<CharacterDirection> Attacked;    // passes the aimed attack direction

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            // Top-down: no gravity, no physics-driven spin.
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();

            if (input == null)
                input = GetComponent<TRVInput>();
            if (aimCamera == null)
                aimCamera = Camera.main;
            if (attackHitbox == null)
                attackHitbox = GetComponentInChildren<AttackHitbox>(true);
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.DashPressed += HandleDash;
                input.AttackPressed += HandleAttack;
                input.InteractPressed += HandleInteract;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.DashPressed -= HandleDash;
                input.AttackPressed -= HandleAttack;
                input.InteractPressed -= HandleInteract;
            }
        }

        private void Start()
        {
            if (stats == null)
            {
                Debug.LogError($"[{nameof(TRVController)}] No Stats asset assigned on '{name}'.", this);
                enabled = false;
                return;
            }
            _health.Init(stats.MaxHealth);
        }

        private void FixedUpdate()
        {
            if (stats == null || input == null) return;

            _health.Invincible = IsInvincible; // dash i-frames gate Health.TakeDamage

            // -0.5) Dash windup: a short pause after pressing dash before the burst fires. Movement
            //       stays normal during it (no i-frames yet); when it elapses the burst begins.
            if (_dashWindupLeft > 0f)
            {
                _dashWindupLeft -= Time.fixedDeltaTime;
                if (_dashWindupLeft <= 0f)
                    BeginDashBurst();
            }

            // 0) Dash overrides normal movement: drive a constant fast velocity in the
            //    locked dash direction until the timer runs out, then fall through to
            //    normal movement (decel takes over from the high dash speed smoothly).
            if (_dashTimeLeft > 0f)
            {
                _dashTimeLeft -= Time.fixedDeltaTime;
                _velocity = _dashDir * stats.DashSpeed;
                _body.linearVelocity = _velocity;
                return;
            }

            // 1) Read intent and apply a deadzone.
            Vector2 raw = input.MoveInput;
            if (raw.magnitude < stats.InputDeadzone)
                raw = Vector2.zero;
            else if (raw.sqrMagnitude > 1f)
                raw = raw.normalized; // never exceed unit length

            // 2) Resolve to an 8-way direction if requested; otherwise just normalize diagonals.
            Vector2 direction = raw;
            if (direction != Vector2.zero)
            {
                if (stats.SnapTo8Directions)
                    direction = CharacterDirectionUtil.Snap(direction);            // unit, on an axis
                else if (stats.NormalizeDiagonal && direction.sqrMagnitude > 1f)
                    direction = direction.normalized;
            }

            // 3) Smoothly ease current velocity toward the target. Attacking briefly slows movement.
            float moveSpeed = stats.MoveSpeed;
            if (_attackSlowTimeLeft > 0f)
            {
                _attackSlowTimeLeft -= Time.fixedDeltaTime;
                moveSpeed *= stats.AttackMoveMultiplier;
            }

            Vector2 targetVelocity = direction * moveSpeed;
            bool hasInput = direction.sqrMagnitude > 0.0001f;
            float rate = hasInput ? stats.Acceleration : stats.Deceleration;

            _velocity = Vector2.MoveTowards(_velocity, targetVelocity, rate * Time.fixedDeltaTime);
            _body.linearVelocity = _velocity; // Unity 6 API (was 'velocity')

            // 4) Track facing from the latest real heading, with forgiving diagonal release.
            bool wasMoving = IsMoving;
            IsMoving = hasInput;

            if (hasInput)
            {
                MoveDirection = direction.normalized;
                SetFacing(CharacterDirectionUtil.FromVector(MoveDirection));
            }
            else if (wasMoving)
            {
                // We just stopped. If facing dropped from a diagonal to one of its component
                // cardinals only a moment ago, it's because one key was released a frame or two
                // before the other — restore the diagonal so the idle matches the intended heading.
                if (CharacterDirectionUtil.IsDiagonal(_previousFacing)
                    && CharacterDirectionUtil.AreAdjacent(_previousFacing, Facing)
                    && Time.time - _facingSetTime < stats.DiagonalReleaseGrace)
                {
                    SetFacing(_previousFacing);
                }
            }
        }

        /// <summary>Commit a new facing, remembering the previous one and when it changed.</summary>
        private void SetFacing(CharacterDirection newFacing)
        {
            if (newFacing == Facing) return;
            _previousFacing = Facing;
            _facingSetTime = Time.time;
            Facing = newFacing;
            FacingChanged?.Invoke(Facing);
        }

        // ─────────────────────────────────────────────────────────────
        // ACTIONS
        // ─────────────────────────────────────────────────────────────
        private void HandleDash()
        {
            if (!IsAlive || IsDashing || _dashWindupLeft > 0f || !_dashCooldown.IsReady
                || _attackSlowTimeLeft > 0f) return;

            // Lock in the dash direction and face it now; the burst (and i-frames) fire after a
            // short windup. Dash toward current movement; if standing still, dash where we face.
            _dashDir = IsMoving && MoveDirection.sqrMagnitude > 0.0001f
                ? MoveDirection
                : CharacterDirectionUtil.ToVector(Facing);
            SetFacing(CharacterDirectionUtil.FromVector(_dashDir));

            if (stats.DashStartupDelay > 0f)
                _dashWindupLeft = stats.DashStartupDelay;
            else
                BeginDashBurst();
        }

        /// <summary>Start the actual dash burst — drives velocity, arms i-frames, begins cooldown.</summary>
        private void BeginDashBurst()
        {
            _dashWindupLeft = 0f;
            _dashTimeLeft = stats.DashDuration;
            _dashCooldown.Begin(stats.DashCooldown);
            DashStarted?.Invoke(CharacterDirectionUtil.FromVector(_dashDir));
        }

        private void HandleAttack()
        {
            if (!IsAlive || !_attackCooldown.IsReady) return;
            _attackCooldown.Begin(stats.AttackCooldown);

            // Aim at the cursor, fully independent of movement direction.
            AttackDirection = CharacterDirectionUtil.FromVector(GetAimDirection());

            // Attacking briefly slows movement and cancels any in-progress (or winding-up) dash.
            _attackSlowTimeLeft = stats.AttackMoveSlowDuration;
            _dashTimeLeft = 0f;
            _dashWindupLeft = 0f;

            // Swing the melee hitbox along the shown 8-way direction (matches the attack animation),
            // dealing stats.Damage to whatever it overlaps.
            if (attackHitbox != null)
                attackHitbox.Strike(CharacterDirectionUtil.ToVector(AttackDirection), stats.Damage, gameObject);

            // Drives the attack animation (TRVAnimator listens).
            Attacked?.Invoke(AttackDirection);
        }

        /// <summary>
        /// World-space direction from the character toward the cursor. Falls back to the current
        /// facing if no camera/pointer is available (e.g. gamepad with no mouse movement).
        /// </summary>
        private Vector2 GetAimDirection()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            if (aimCamera != null && input != null)
            {
                Vector3 screen = input.PointerScreenPosition;
                // Distance from the camera to the character's plane (works for ortho and perspective).
                screen.z = Mathf.Abs(aimCamera.transform.position.z - transform.position.z);
                Vector2 aim = (Vector2)aimCamera.ScreenToWorldPoint(screen) - (Vector2)transform.position;
                if (aim.sqrMagnitude > 0.0001f)
                    return aim;
            }
            return CharacterDirectionUtil.ToVector(Facing);
        }

        private void HandleInteract()
        {
            if (!IsAlive) return;
            // Hook interaction logic here (open door, pick up item, talk...).
            Debug.Log($"{stats.CharacterName} interacts ({Facing}).", this);
        }

        /// <summary><see cref="IKnockbackable"/> — shove TRV along a direction as a pure impulse:
        /// it sets velocity and control returns immediately (no stun), so the push just eases out
        /// via accel/decel. No-op while <see cref="IsInvincible"/> (dashing). Damage is handled by
        /// the <see cref="Health"/> component.</summary>
        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (!IsAlive || force <= 0f || direction.sqrMagnitude < 0.0001f || IsInvincible) return;
            _velocity = direction.normalized * force;
        }
    }
}
