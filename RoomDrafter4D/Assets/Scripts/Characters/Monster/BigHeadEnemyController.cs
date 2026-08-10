using UnityEngine;

namespace TRV
{
    /// <summary>
    /// "Big Head" — a FLYING, kiting ranged enemy built on <see cref="EnemyController"/> (works
    /// with the standard 4-direction <see cref="EnemyAnimator"/> — the shot raises the normal
    /// Attack trigger). Cycle: when the base FSM attacks (player within AttackRange + cooldown
    /// ready) it RUNS AWAY from the player at <see cref="fleeSpeed"/> (regular movement keeps
    /// using MoveSpeed); once far enough (<see cref="safeDistance"/> — or pressed against the
    /// WORLD LIMIT and unable to flee further, or timed out) it stops, TURNS to the player and
    /// INSTANTLY fires a bullet, HOLDS in place facing the player for <see cref="holdSeconds"/>,
    /// then WANDERS for <see cref="wanderSeconds"/> at the regular MoveSpeed before the base FSM
    /// resumes — ready to attack again once the cooldown (begun at the flee's start) allows.
    ///
    /// FLYING: the prefab's physics make it ignore everything EXCEPT the hand-painted WORLD LIMIT
    /// tilemap layer (set its body layer to collide ONLY with that layer in the collision matrix,
    /// Rigidbody2D DYNAMIC with gravity 0) — it glides over water/walls/decor but the world limit
    /// walls it in, so it can never leave the map; while fleeing it slides around them.
    ///
    /// BULLET: assign a <see cref="PlayerOnlyBullet"/> prefab — it has NO collider/physics and
    /// hits the player purely by proximity, so it passes through walls/water/decor and ignores
    /// every collision setup (including the flyer's restricted layer matrix) by definition.
    /// </summary>
    public class BigHeadEnemyController : EnemyController
    {
        private enum Phase { None, Flee, Hold, Wander }

        [Header("Big Head")]
        [Tooltip("Run-away speed while fleeing to firing distance (regular movement uses MoveSpeed).")]
        [SerializeField, Min(0.1f)] private float fleeSpeed = 4f;

        [Tooltip("Distance from the player at which it stops fleeing and fires.")]
        [SerializeField, Min(1f)] private float safeDistance = 5f;

        [Tooltip("Safety cap on a single flee — it fires from wherever it is when this runs out.")]
        [SerializeField, Min(0.5f)] private float maxFleeSeconds = 3f;

        [Tooltip("While fleeing: pressed against the world limit (barely moving) for this long → " +
                 "it can flee no further and fires from where it is.")]
        [SerializeField, Min(0.1f)] private float stuckFireSeconds = 0.35f;

        [Tooltip("After firing it stays put, FACING the player, for this long.")]
        [SerializeField, Min(0f)] private float holdSeconds = 2f;

        [Tooltip("How long it wanders (at MoveSpeed) after the hold before the FSM resumes.")]
        [SerializeField, Min(0f)] private float wanderSeconds = 2.5f;

        [Tooltip("How often the wander direction rerolls.")]
        [SerializeField, Min(0.2f)] private float wanderTurnSeconds = 0.8f;

        [Tooltip("The bullet fired at the player — a prefab with an IProjectile component. Use " +
                 "PlayerOnlyBullet (no collider/physics; hits the player by proximity, passes " +
                 "through everything else by definition).")]
        [SerializeField] private GameObject bulletPrefab;

        [Tooltip("Where the bullet spawns. Empty = this transform.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("The projectile APPEARS at the muzzle first and is thrown this many seconds " +
                 "later (0 = thrown instantly).")]
        [SerializeField, Min(0f)] private float throwDelay = 0.15f;

        private Phase _phase;
        private float _phaseLeft;
        private float _turnLeft;
        private Vector2 _roamDir;
        private Vector2 _lastPosition;
        private float _stuckTime;
        private GameObject _heldBullet; // spawned but not yet thrown (throwDelay pending)

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _phase = Phase.None; // pooled respawn: back to the normal FSM
            _heldBullet = null;
        }

        /// <summary>The whole attack = the flee → fire → hold → wander cycle, driven by
        /// OverrideMovement.</summary>
        protected override void DoAttack(Vector2 dir)
        {
            BeginAttackCooldown();
            _phase = Phase.Flee;
            _phaseLeft = maxFleeSeconds;
            _stuckTime = 0f;
            _lastPosition = transform.position;
        }

        protected override bool OverrideMovement(float dt, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            switch (_phase)
            {
                case Phase.Flee:
                {
                    _phaseLeft -= dt;
                    if (!PlayerLocator.TryGetPosition(out var player))
                    {
                        Fire(); // player gone mid-flee — loose a shot and move on
                        return true;
                    }

                    Vector2 away = (Vector2)transform.position - (Vector2)player;
                    if (away.magnitude >= safeDistance || _phaseLeft <= 0f)
                    {
                        Fire();
                        return true;
                    }

                    // Pressed against the world limit (physics blocks it) and barely moving →
                    // it can flee no further: fire from here.
                    Vector2 position = transform.position;
                    float moved = (position - _lastPosition).magnitude;
                    _lastPosition = position;
                    _stuckTime = moved < fleeSpeed * dt * 0.25f ? _stuckTime + dt : 0f;
                    if (_stuckTime >= stuckFireSeconds)
                    {
                        Fire();
                        return true;
                    }

                    Vector2 dir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.down;
                    velocity = dir * fleeSpeed; // the world limit collider does the containing
                    FacingDirection = dir;
                    return true;
                }

                case Phase.Hold:
                {
                    _phaseLeft -= dt;
                    // Stand still, tracking the player with its facing.
                    if (PlayerLocator.TryGetPosition(out var watched))
                    {
                        Vector2 toPlayer = (Vector2)watched - (Vector2)transform.position;
                        if (toPlayer.sqrMagnitude > 0.0001f) FacingDirection = toPlayer.normalized;
                    }
                    if (_phaseLeft <= 0f)
                    {
                        _phase = Phase.Wander;
                        _phaseLeft = wanderSeconds;
                        RollWanderDir();
                    }
                    return true;
                }

                case Phase.Wander:
                {
                    _phaseLeft -= dt;
                    if (_phaseLeft <= 0f)
                    {
                        _phase = Phase.None; // done — the base FSM takes over again
                        return true;
                    }
                    _turnLeft -= dt;
                    if (_turnLeft <= 0f) RollWanderDir();
                    velocity = _roamDir * Stats.MoveSpeed; // world limit blocks/slides at the border
                    FacingDirection = _roamDir;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Stop, turn to the player and shoot instantly (attack anim + bullet via
        /// <see cref="OnAttack"/>), then HOLD in place facing the player before wandering.</summary>
        private void Fire()
        {
            Vector2 aim = FacingDirection;
            if (PlayerLocator.TryGetPosition(out var player))
            {
                Vector2 toPlayer = (Vector2)player - (Vector2)transform.position;
                if (toPlayer.sqrMagnitude > 0.0001f) aim = toPlayer.normalized;
            }
            FacingDirection = aim;
            FireAttack(aim); // AttackPerformed (anim) + OnAttack (the bullet)

            _phase = Phase.Hold;
            _phaseLeft = holdSeconds;
        }

        /// <summary>The attack = the bullet (no melee hitbox). The projectile is SPAWNED at the
        /// muzzle immediately (visible, sitting still) and thrown <see cref="throwDelay"/> seconds
        /// later.</summary>
        protected override void OnAttack(Vector2 dir)
        {
            if (bulletPrefab == null) return;
            var origin = muzzle != null ? muzzle.position : transform.position;
            var bullet = Instantiate(bulletPrefab, origin, Quaternion.identity);

            // Bullets carry no damage of their own — the SHOOTER's damage stat (layer-scaled) is it.
            if (bullet.TryGetComponent<IProjectile>(out var armed))
                armed.SetDamage((Stats != null ? Stats.Damage : 0f) * DamageScale);
            if (throwDelay <= 0f)
            {
                Throw(bullet, dir);
                return;
            }
            _heldBullet = bullet;
            StartCoroutine(ThrowAfterDelay(bullet, dir));
        }

        private System.Collections.IEnumerator ThrowAfterDelay(GameObject bullet, Vector2 dir)
        {
            yield return new WaitForSeconds(throwDelay);
            if (bullet == null) yield break; // destroyed while held (e.g. the thrower died)
            _heldBullet = null;
            Throw(bullet, dir);
        }

        /// <summary>Launch the bullet at the player's position AT THROW TIME (re-aimed from the
        /// bullet's spot, so the delay can't make it fly at a stale position); the attack's
        /// original direction is only a fallback for a missing player.</summary>
        private void Throw(GameObject bullet, Vector2 fallbackDir)
        {
            Vector2 dir = fallbackDir;
            if (PlayerLocator.TryGetPosition(out var playerPos))
            {
                Vector2 toPlayer = playerPos - (Vector2)bullet.transform.position;
                if (toPlayer.sqrMagnitude > 0.0001f) dir = toPlayer.normalized;
            }
            if (bullet.TryGetComponent<IProjectile>(out var projectile))
                projectile.Launch(dir, gameObject);
        }

        /// <summary>A bullet still sitting at the muzzle dies with its thrower (an unlaunched
        /// bullet never flies or expires, so it would linger forever).</summary>
        protected override void Die()
        {
            if (_heldBullet != null)
            {
                Destroy(_heldBullet);
                _heldBullet = null;
            }
            base.Die();
        }

        private void RollWanderDir()
        {
            _turnLeft = wanderTurnSeconds;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            _roamDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
