using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Bridges <see cref="TRVController"/> state into an Animator. It does NOT decide
    /// which clip plays — it publishes the character's facing, motion and attack as Animator
    /// parameters, and blend trees in the Animator Controller select the right directional clip.
    ///
    /// Because <see cref="TRVController.Facing"/> holds the LAST direction moved, the idle
    /// automatically faces the way you were going when you stop. Attacks use a separate
    /// cursor-aimed direction so they fire any way regardless of movement.
    ///
    /// Expected Animator parameters (create the ones you use; missing ones are skipped):
    ///   • LookX, LookY     (Float)   — unit vector of the current facing (movement blend trees).
    ///   • IsMoving         (Bool)    — true while receiving move input (Idle↔Move transitions).
    ///   • Speed            (Float)   — 0 when idle, 1 when moving (alternative to IsMoving).
    ///   • AttackX, AttackY (Float)   — unit vector of the cursor-aimed attack (attack blend tree).
    ///   • Attack           (Trigger) — fired on each attack.
    /// </summary>
    [RequireComponent(typeof(TRVController))]
    public class TRVAnimator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator that holds the directional blend trees. Auto-found in children if empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Source of facing/motion state. Auto-found on this GameObject if empty.")]
        [SerializeField] private TRVController controller;

        [Header("Animator Parameter Names")]
        [SerializeField] private string lookXParam = "LookX";
        [SerializeField] private string lookYParam = "LookY";
        [SerializeField] private string isMovingParam = "IsMoving";
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string attackXParam = "AttackX";
        [SerializeField] private string attackYParam = "AttackY";
        [SerializeField] private string attackTriggerParam = "Attack";

        private AnimParam _lookX, _lookY, _isMoving, _speed, _attackX, _attackY, _attack;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<TRVController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _lookX = new AnimParam(animator, lookXParam);
            _lookY = new AnimParam(animator, lookYParam);
            _isMoving = new AnimParam(animator, isMovingParam);
            _speed = new AnimParam(animator, speedParam);
            _attackX = new AnimParam(animator, attackXParam);
            _attackY = new AnimParam(animator, attackYParam);
            _attack = new AnimParam(animator, attackTriggerParam);
        }

        private void OnEnable()
        {
            if (controller != null) controller.Attacked += OnAttacked;
        }

        private void OnDisable()
        {
            if (controller != null) controller.Attacked -= OnAttacked;
        }

        private void Update()
        {
            if (controller == null) return;

            // Facing is a discrete 8-way value; turn it into the unit vector the blend tree expects.
            Vector2 look = TRVDirectionUtil.ToVector(controller.Facing);
            _lookX.SetFloat(look.x);
            _lookY.SetFloat(look.y);
            _isMoving.SetBool(controller.IsMoving);
            _speed.SetFloat(controller.IsMoving ? 1f : 0f);
        }

        // Aim the attack blend tree at the cursor-derived direction, then fire the trigger.
        private void OnAttacked(TRVDirection dir)
        {
            Vector2 aim = TRVDirectionUtil.ToVector(dir);
            _attackX.SetFloat(aim.x);
            _attackY.SetFloat(aim.y);
            _attack.SetTrigger();
        }

        /// <summary>
        /// One Animator parameter: caches its hash and whether the controller actually defines it,
        /// so every setter is a safe no-op (no warning spam) when the parameter is absent.
        /// </summary>
        private readonly struct AnimParam
        {
            private readonly Animator _animator;
            private readonly int _hash;
            private readonly bool _exists;

            public AnimParam(Animator animator, string name)
            {
                _animator = animator;
                _hash = Animator.StringToHash(name);
                _exists = false;
                if (animator != null)
                {
                    foreach (var p in animator.parameters)
                    {
                        if (p.name == name) { _exists = true; break; }
                    }
                }
            }

            public void SetFloat(float value) { if (_exists) _animator.SetFloat(_hash, value); }
            public void SetBool(bool value) { if (_exists) _animator.SetBool(_hash, value); }
            public void SetTrigger() { if (_exists) _animator.SetTrigger(_hash); }
        }
    }
}
