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
    /// <see cref="TRVDirection"/> values for animation/aiming to consume.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TRVController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The data asset with all tunable stats.")]
        [SerializeField] private TRVStats stats;

        [Tooltip("The input source. Auto-found on this GameObject if left empty.")]
        [SerializeField] private TRVInput input;

        [Tooltip("Camera used to turn the cursor position into a world aim direction. Defaults to Camera.main.")]
        [SerializeField] private Camera aimCamera;

        private Rigidbody2D _body;
        private Vector2 _velocity;        // our own smoothed velocity
        private Cooldown _attackCooldown;

        // ── Dash state ──
        private float _dashTimeLeft;      // > 0 while dashing
        private Cooldown _dashCooldown;
        private Vector2 _dashDir;

        // ── Facing history (for forgiving diagonal release) ──
        private TRVDirection _previousFacing = TRVDirection.South;
        private float _facingSetTime = float.NegativeInfinity;

        // ── Runtime state others can read / subscribe to ──
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        public Vector2 MoveDirection { get; private set; } // last non-zero unit heading
        public TRVDirection Facing { get; private set; } = TRVDirection.South;
        public bool IsMoving { get; private set; }
        public bool IsDashing => _dashTimeLeft > 0f;

        /// <summary>8-way direction of the most recent attack, aimed at the cursor (independent of movement).</summary>
        public TRVDirection AttackDirection { get; private set; } = TRVDirection.South;

        public event Action<TRVDirection> FacingChanged;
        public event Action<float> HealthChanged;   // passes new current health
        public event Action DashStarted;
        public event Action<TRVDirection> Attacked; // passes the aimed attack direction
        public event Action Died;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            // Top-down: no gravity, no physics-driven spin.
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            if (input == null)
                input = GetComponent<TRVInput>();
            if (aimCamera == null)
                aimCamera = Camera.main;
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
            CurrentHealth = stats.MaxHealth;
            HealthChanged?.Invoke(CurrentHealth);
        }

        private void FixedUpdate()
        {
            if (stats == null || input == null) return;

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
                    direction = TRVDirectionUtil.Snap(direction);            // unit, on an axis
                else if (stats.NormalizeDiagonal && direction.sqrMagnitude > 1f)
                    direction = direction.normalized;
            }

            // 3) Smoothly ease current velocity toward the target.
            Vector2 targetVelocity = direction * stats.MoveSpeed;
            bool hasInput = targetVelocity.sqrMagnitude > 0.0001f;
            float rate = hasInput ? stats.Acceleration : stats.Deceleration;

            _velocity = Vector2.MoveTowards(_velocity, targetVelocity, rate * Time.fixedDeltaTime);
            _body.linearVelocity = _velocity; // Unity 6 API (was 'velocity')

            // 4) Track facing from the latest real heading, with forgiving diagonal release.
            bool wasMoving = IsMoving;
            IsMoving = hasInput;

            if (hasInput)
            {
                MoveDirection = direction.normalized;
                SetFacing(TRVDirectionUtil.FromVector(MoveDirection));
            }
            else if (wasMoving)
            {
                // We just stopped. If facing dropped from a diagonal to one of its component
                // cardinals only a moment ago, it's because one key was released a frame or two
                // before the other — restore the diagonal so the idle matches the intended heading.
                if (TRVDirectionUtil.IsDiagonal(_previousFacing)
                    && TRVDirectionUtil.AreAdjacent(_previousFacing, Facing)
                    && Time.time - _facingSetTime < stats.DiagonalReleaseGrace)
                {
                    SetFacing(_previousFacing);
                }
            }
        }

        /// <summary>Commit a new facing, remembering the previous one and when it changed.</summary>
        private void SetFacing(TRVDirection newFacing)
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
            if (!IsAlive || IsDashing || !_dashCooldown.IsReady) return;

            // Dash toward current movement; if standing still, dash where we face.
            _dashDir = IsMoving && MoveDirection.sqrMagnitude > 0.0001f
                ? MoveDirection
                : TRVDirectionUtil.ToVector(Facing);

            _dashTimeLeft = stats.DashDuration;
            _dashCooldown.Begin(stats.DashCooldown);

            // Face the dash direction immediately.
            SetFacing(TRVDirectionUtil.FromVector(_dashDir));

            DashStarted?.Invoke();
        }

        private void HandleAttack()
        {
            if (!IsAlive || !_attackCooldown.IsReady) return;
            _attackCooldown.Begin(stats.AttackCooldown);

            // Aim at the cursor, fully independent of movement direction.
            AttackDirection = TRVDirectionUtil.FromVector(GetAimDirection());

            // Drives the attack animation (TRVAnimator listens). Hook the real attack here too:
            // spawn a hitbox along AttackDirection within stats.AttackRange, deal stats.Damage, etc.
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
            return TRVDirectionUtil.ToVector(Facing);
        }

        private void HandleInteract()
        {
            if (!IsAlive) return;
            // Hook interaction logic here (open door, pick up item, talk...).
            Debug.Log($"{stats.CharacterName} interacts ({Facing}).", this);
        }

        /// <summary>Apply damage. Fires HealthChanged, and Died when health reaches zero.</summary>
        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth);
            if (CurrentHealth == 0f)
                Died?.Invoke();
        }

        /// <summary>Restore health up to MaxHealth.</summary>
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            CurrentHealth = Mathf.Min(stats.MaxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth);
        }
    }
}
