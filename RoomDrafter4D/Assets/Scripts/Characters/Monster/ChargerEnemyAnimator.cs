using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Animator bridge for <see cref="ChargerEnemyController"/>. Like <see cref="EnemyAnimator"/> it
    /// publishes 4-way Horizontal/Vertical + a Move bool, but <b>Attack is a BOOL</b> held for the whole
    /// charge: true on charge start, false when the lunge ends. The Animator's Start → Attack-loop → End
    /// transitions are driven by that bool plus exit-time transitions (Idle → Start on Attack true,
    /// Attack → End on Attack false). The charge direction flows through FacingDirection, so the attack
    /// blend tree faces the lunge.
    ///
    /// Params: Horizontal/Vertical (Float), Move (Bool), Attack (Bool).
    /// </summary>
    [RequireComponent(typeof(ChargerEnemyController))]
    public class ChargerEnemyAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private ChargerEnemyController controller;

        [Header("Animator Parameter Names")]
        [SerializeField] private string horizontalParam = "Horizontal";
        [SerializeField] private string verticalParam = "Vertical";
        [SerializeField] private string moveParam = "Move";
        [SerializeField] private string attackParam = "Attack";

        private AnimParam _horizontal, _vertical, _move, _attack;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<ChargerEnemyController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _horizontal = new AnimParam(animator, horizontalParam);
            _vertical = new AnimParam(animator, verticalParam);
            _move = new AnimParam(animator, moveParam);
            _attack = new AnimParam(animator, attackParam);
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.ChargeStarted += OnChargeStarted;
            controller.ChargeEnded += OnChargeEnded;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.ChargeStarted -= OnChargeStarted;
            controller.ChargeEnded -= OnChargeEnded;
        }

        private void Update()
        {
            if (controller == null) return;

            // Snap facing to 4-way, diagonals → horizontal (East/West).
            CharacterDirectionUtil.ToFourWay(controller.FacingDirection, out float h, out float v);
            _horizontal.SetFloat(h);
            _vertical.SetFloat(v);
            _move.SetBool(controller.IsMoving);
        }

        private void OnChargeStarted(Vector2 dir) => _attack.SetBool(true);
        private void OnChargeEnded() => _attack.SetBool(false);
    }
}
