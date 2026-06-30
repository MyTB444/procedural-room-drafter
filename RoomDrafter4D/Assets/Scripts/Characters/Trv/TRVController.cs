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

        [Tooltip("Optional: turns the body collider into a hitbox during the dash. Auto-found on this " +
                 "object; leave the component off if the dash shouldn't deal damage.")]
        [SerializeField] private DashHitbox dashHitbox;

        private Rigidbody2D _body;
        private Health _health;
        private PlayerUpgrades _upgrades; // runtime stat multipliers + ability unlocks (auto-added)
        private Vector2 _velocity;        // our own smoothed velocity
        private Cooldown _attackCooldown;
        private bool _controlsEnabled = true; // false while the pause menu is open or the player is dead

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

        // Last horizontal facing sign for the left/right-only interact anim (default right).
        private float _interactFaceX = 1f;

        // ── Stamina (spent by actions, regenerates after a short delay) ──
        private float _stamina;
        private float _staminaRegenBlockLeft; // no regen while > 0 (covers the action + the brief after)

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

        /// <summary>Current stamina (0..MaxStamina). Actions spend it; it regenerates after a short delay.</summary>
        public float CurrentStamina => _stamina;

        /// <summary>Maximum stamina (base stat with the MaxStamina upgrade applied). For UI.</summary>
        public float MaxStamina => Effective(PlayerUpgrades.Stat.MaxStamina, stats != null ? stats.MaxStamina : 0f);

        /// <summary>A stat with its upgrades applied (base × multiplier + bonus); the base when no upgrades.</summary>
        private float Effective(PlayerUpgrades.Stat stat, float baseValue) =>
            _upgrades != null ? _upgrades.Apply(stat, baseValue) : baseValue;

        public event Action<CharacterDirection> FacingChanged;
        public event Action<CharacterDirection> DashStarted; // passes the 8-way dash direction
        public event Action<CharacterDirection> Attacked;    // passes the aimed attack direction
        public event Action<float> Interacted;               // passes horizontal facing sign: -1 left, +1 right

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            // Top-down: no gravity, no physics-driven spin.
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();

            _upgrades = GetComponent<PlayerUpgrades>();
            if (_upgrades == null) _upgrades = gameObject.AddComponent<PlayerUpgrades>();

            if (input == null)
                input = GetComponent<TRVInput>();
            if (aimCamera == null)
                aimCamera = Camera.main;
            if (attackHitbox == null)
                attackHitbox = GetComponentInChildren<AttackHitbox>(true);
            if (dashHitbox == null)
                dashHitbox = GetComponent<DashHitbox>(); // optional — dash deals damage only if present
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
            _stamina = MaxStamina;
        }

        private void FixedUpdate()
        {
            if (stats == null || input == null) return;

            // Frozen by the pause menu or death: stand still, ignore all input.
            if (!_controlsEnabled)
            {
                _velocity = Vector2.zero;
                _body.linearVelocity = Vector2.zero;
                IsMoving = false;
                return;
            }

            _health.Invincible = IsInvincible; // dash i-frames gate Health.TakeDamage
            TickStamina(Time.fixedDeltaTime);
            TickHealthRegen(Time.fixedDeltaTime);
            // The body collider deals damage while dashing (re-arms on the rising edge, deduped per dash).
            if (dashHitbox != null) dashHitbox.SetActive(IsDashing, gameObject, stats.Damage);

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
            if (!_controlsEnabled || !IsAlive || IsDashing || _dashWindupLeft > 0f || !_dashCooldown.IsReady
                || _attackSlowTimeLeft > 0f) return;
            if (_upgrades == null || !_upgrades.IsUnlocked(PlayerUpgrades.Ability.Dash)) return; // dash is locked until the upgrade
            if (!TrySpendStamina(stats.DodgeStaminaCost)) return; // not enough stamina → no dodge

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
            if (!_controlsEnabled || !IsAlive || !_attackCooldown.IsReady) return;
            if (!TrySpendStamina(Effective(PlayerUpgrades.Stat.AttackStaminaCost, stats.AttackStaminaCost))) return;
            _attackCooldown.Begin(Effective(PlayerUpgrades.Stat.AttackCooldown, stats.AttackCooldown));

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
            if (!_controlsEnabled || !IsAlive) return;

            // Interact only works when next to an available interactable (proximity, no hitbox).
            var interactable = Interactable.FindNearestAvailable(transform.position);
            if (interactable == null) return;

            // The interact animation is left/right only — resolve the facing to a horizontal sign,
            // keeping the last horizontal one when facing straight up/down (Facing.x == 0).
            float x = CharacterDirectionUtil.ToVector(Facing).x;
            if (!Mathf.Approximately(x, 0f)) _interactFaceX = Mathf.Sign(x);
            Interacted?.Invoke(_interactFaceX);

            interactable.Interact(this);
        }

        // ─────────────────────────────────────────────────────────────
        // STAMINA
        // ─────────────────────────────────────────────────────────────
        /// <summary>Spend stamina for an action if there's enough; returns false (no spend) otherwise so
        /// the caller blocks the action. A successful spend re-arms the post-action regen delay. When the
        /// room is CLEARED of enemies, actions are free — no cost, never blocked.</summary>
        private bool TrySpendStamina(float cost)
        {
            if (!EnemyPool.RoomHasEnemies) return true; // cleared room → free actions, no stamina spent
            if (_stamina < cost) return false;
            _stamina -= cost;
            _staminaRegenBlockLeft = stats.StaminaRegenDelay;
            return true;
        }

        /// <summary>Regenerate stamina — but NOT during an action (dash windup/burst or attack slow) nor
        /// for <see cref="TRVStats.StaminaRegenDelay"/> after one ends; then quickly, and TWICE as fast
        /// while below <see cref="TRVStats.LowStaminaThreshold"/>. In a CLEARED room actions don't
        /// interrupt regen at all (it just keeps filling).</summary>
        private void TickStamina(float dt)
        {
            if (EnemyPool.RoomHasEnemies)
            {
                bool inAction = IsDashing || _dashWindupLeft > 0f || _attackSlowTimeLeft > 0f;
                if (inAction)
                {
                    _staminaRegenBlockLeft = stats.StaminaRegenDelay; // keep the "brief after" delay armed
                    return;
                }
                if (_staminaRegenBlockLeft > 0f)
                {
                    _staminaRegenBlockLeft -= dt;
                    return;
                }
            }
            else
            {
                _staminaRegenBlockLeft = 0f; // cleared room → actions never cancel regen
            }

            float max = MaxStamina;
            if (_stamina >= max) return;
            float rate = Effective(PlayerUpgrades.Stat.StaminaRegen, stats.StaminaRegenPerSecond);
            if (_stamina < stats.LowStaminaThreshold) rate *= stats.LowStaminaRegenMultiplier;
            _stamina = Mathf.Min(max, _stamina + rate * dt);
        }

        /// <summary>Passively regenerate HP at the effective <see cref="TRVStats.HealthRegenPerSecond"/>
        /// (base 0 — upgrades grant/boost it). No-op at full health or when the base+upgrades are 0.</summary>
        private void TickHealthRegen(float dt)
        {
            if (_health == null || !_health.IsAlive || _health.Current >= _health.Max) return;
            float rate = Effective(PlayerUpgrades.Stat.HealthRegen, stats.HealthRegenPerSecond);
            if (rate > 0f) _health.Heal(rate * dt);
        }

        /// <summary>Freeze/unfreeze player control. Disabling stops the body immediately and cancels any
        /// in-progress dash/attack; movement and dash/attack/interact are all ignored until re-enabled.
        /// Used by the pause menu (Escape) and on death, so a frozen or dead player can't act or slide.</summary>
        public void SetControlsEnabled(bool value)
        {
            _controlsEnabled = value;
            if (value) return;

            // Cancel any in-progress action and stop dead.
            _dashWindupLeft = 0f;
            _dashTimeLeft = 0f;
            _attackSlowTimeLeft = 0f;
            _velocity = Vector2.zero;
            if (_body != null) _body.linearVelocity = Vector2.zero;
            IsMoving = false;
            if (dashHitbox != null) dashHitbox.SetActive(false, gameObject, 0f);
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
