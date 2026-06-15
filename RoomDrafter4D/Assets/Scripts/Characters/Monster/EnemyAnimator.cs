using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Bridges <see cref="EnemyController"/> state into an Animator using 4-direction parameters.
    /// Diagonals snap to the horizontal (East/West) clip — when |x| ≥ |y| the enemy faces E/W,
    /// otherwise N/S. Mirrors <see cref="TRVAnimator"/>; reusable by any enemy with these params.
    ///
    /// Expected Animator parameters (create the ones you use; missing ones are skipped):
    ///   • Horizontal, Vertical (Float)   — 4-way facing unit (one is ±1, the other 0).
    ///   • Move                 (Bool)    — true while moving (Idle↔Move).
    ///   • Attack               (Trigger) — fired on each attack.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyController controller;

        [Header("Animator Parameter Names")]
        [SerializeField] private string horizontalParam = "Horizontal";
        [SerializeField] private string verticalParam = "Vertical";
        [SerializeField] private string moveParam = "Move";
        [SerializeField] private string attackTriggerParam = "Attack";

        private AnimParam _horizontal, _vertical, _move, _attack;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<EnemyController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _horizontal = new AnimParam(animator, horizontalParam);
            _vertical = new AnimParam(animator, verticalParam);
            _move = new AnimParam(animator, moveParam);
            _attack = new AnimParam(animator, attackTriggerParam);
        }

        private void OnEnable() { if (controller != null) controller.AttackPerformed += OnAttack; }
        private void OnDisable() { if (controller != null) controller.AttackPerformed -= OnAttack; }

        private void Update()
        {
            if (controller == null) return;

            // Snap facing to 4-way, diagonals → horizontal (East/West).
            Vector2 f = controller.FacingDirection;
            float h = 0f, v = 0f;
            if (Mathf.Abs(f.x) >= Mathf.Abs(f.y))
            {
                if (Mathf.Abs(f.x) > 0.0001f) h = Mathf.Sign(f.x);
            }
            else
            {
                v = Mathf.Sign(f.y);
            }

            _horizontal.SetFloat(h);
            _vertical.SetFloat(v);
            _move.SetBool(controller.IsMoving);
        }

        private void OnAttack() => _attack.SetTrigger();
    }
}
