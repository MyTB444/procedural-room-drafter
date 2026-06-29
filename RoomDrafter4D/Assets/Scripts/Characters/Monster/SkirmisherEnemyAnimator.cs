using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Animator bridge for <see cref="SkirmisherEnemyController"/>. Publishes 4-way Horizontal/Vertical +
    /// a Move bool like <see cref="EnemyAnimator"/>, and maps the controller's events to triggers:
    /// <c>Attacked(0)</c> → <c>Attack</c>, <c>Attacked(1)</c> → <c>Attack2</c>, <c>AbilityUsed</c> →
    /// <c>Ability</c>.
    ///
    /// Params: Horizontal/Vertical (Float), Move (Bool), Attack/Attack2/Ability (Triggers).
    /// </summary>
    [RequireComponent(typeof(SkirmisherEnemyController))]
    public class SkirmisherEnemyAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private SkirmisherEnemyController controller;

        [Header("Animator Parameter Names")]
        [SerializeField] private string horizontalParam = "Horizontal";
        [SerializeField] private string verticalParam = "Vertical";
        [SerializeField] private string moveParam = "Move";
        [SerializeField] private string attackTriggerParam = "Attack";
        [SerializeField] private string attack2TriggerParam = "Attack2";
        [SerializeField] private string abilityTriggerParam = "Ability";

        private AnimParam _horizontal, _vertical, _move, _attack, _attack2, _ability;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<SkirmisherEnemyController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _horizontal = new AnimParam(animator, horizontalParam);
            _vertical = new AnimParam(animator, verticalParam);
            _move = new AnimParam(animator, moveParam);
            _attack = new AnimParam(animator, attackTriggerParam);
            _attack2 = new AnimParam(animator, attack2TriggerParam);
            _ability = new AnimParam(animator, abilityTriggerParam);
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.Attacked += OnAttacked;
            controller.AbilityUsed += OnAbilityUsed;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.Attacked -= OnAttacked;
            controller.AbilityUsed -= OnAbilityUsed;
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

        private void OnAttacked(int index) => (index == 1 ? _attack2 : _attack).SetTrigger();
        private void OnAbilityUsed() => _ability.SetTrigger();
    }
}
