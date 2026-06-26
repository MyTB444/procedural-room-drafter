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
    ///   • DashX, DashY     (Float)   — unit vector of the dash direction (dash blend tree).
    ///   • Dash             (Trigger) — fired when a dash burst starts.
    ///   • InteractX        (Float)   — horizontal sign of the interact (-1 left, +1 right); the
    ///                                  interact is LEFT/RIGHT only, so pick the clip on its sign.
    ///   • Interact         (Trigger) — fired on each interact.
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
        [SerializeField] private string dashXParam = "DashX";
        [SerializeField] private string dashYParam = "DashY";
        [SerializeField] private string dashTriggerParam = "Dash";
        [SerializeField] private string interactXParam = "InteractX";
        [SerializeField] private string interactTriggerParam = "Interact";

        private AnimParam _lookX, _lookY, _isMoving, _speed, _attackX, _attackY, _attack;
        private AnimParam _dashX, _dashY, _dash;
        private AnimParam _interactX, _interact;

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
            _dashX = new AnimParam(animator, dashXParam);
            _dashY = new AnimParam(animator, dashYParam);
            _dash = new AnimParam(animator, dashTriggerParam);
            _interactX = new AnimParam(animator, interactXParam);
            _interact = new AnimParam(animator, interactTriggerParam);
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.Attacked += OnAttacked;
            controller.DashStarted += OnDashStarted;
            controller.Interacted += OnInteracted;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.Attacked -= OnAttacked;
            controller.DashStarted -= OnDashStarted;
            controller.Interacted -= OnInteracted;
        }

        private void Update()
        {
            if (controller == null) return;

            // Facing is a discrete 8-way value; turn it into the unit vector the blend tree expects.
            Vector2 look = CharacterDirectionUtil.ToVector(controller.Facing);
            _lookX.SetFloat(look.x);
            _lookY.SetFloat(look.y);
            _isMoving.SetBool(controller.IsMoving);
            _speed.SetFloat(controller.IsMoving ? 1f : 0f);
        }

        // Aim the attack blend tree at the cursor-derived direction, then fire the trigger.
        private void OnAttacked(CharacterDirection dir)
        {
            Vector2 aim = CharacterDirectionUtil.ToVector(dir);
            _attackX.SetFloat(aim.x);
            _attackY.SetFloat(aim.y);
            _attack.SetTrigger();
        }

        // Point the dash blend tree along the dash direction, then fire the trigger.
        private void OnDashStarted(CharacterDirection dir)
        {
            Vector2 d = CharacterDirectionUtil.ToVector(dir);
            _dashX.SetFloat(d.x);
            _dashY.SetFloat(d.y);
            _dash.SetTrigger();
        }

        // Interact is LEFT/RIGHT only — publish the horizontal sign, then fire the trigger. Negated so
        // it matches the interact clips' orientation (the right-facing clip sits on the -1 side of the
        // blend tree); flip this back if you instead swap the clips' thresholds in the Animator.
        private void OnInteracted(float faceX)
        {
            _interactX.SetFloat(-faceX);
            _interact.SetTrigger();
        }
    }
}
