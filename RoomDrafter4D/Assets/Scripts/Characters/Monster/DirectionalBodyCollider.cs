using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Swaps a <see cref="BoxCollider2D"/>'s offset/size when the enemy faces LEFT or RIGHT (horizontal),
    /// and KEEPS the collider's authored offset/size when it faces UP or DOWN — for sprites whose
    /// silhouette is wider sideways (e.g. a quadruped wolf) so the body collider matches the facing.
    /// Author the collider in the inspector as the up/down shape; this overrides it for horizontal facing.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class DirectionalBodyCollider : MonoBehaviour
    {
        [Tooltip("Source of facing. Auto-found on this object if empty.")]
        [SerializeField] private EnemyController controller;

        [Header("Collider while facing LEFT / RIGHT (horizontal)")]
        [SerializeField] private Vector2 horizontalOffset = new Vector2(0.005f, 0.02f);
        [SerializeField] private Vector2 horizontalSize = new Vector2(0.6f, 0.34f);

        private BoxCollider2D _box;
        private Vector2 _verticalOffset, _verticalSize; // the authored up/down values, kept as-is
        private bool _isHorizontal;

        private void Awake()
        {
            _box = GetComponent<BoxCollider2D>();
            if (controller == null) controller = GetComponent<EnemyController>();
            _verticalOffset = _box.offset; // capture the authored (up/down) shape
            _verticalSize = _box.size;
            _isHorizontal = false;
        }

        private void Update()
        {
            if (controller == null) return;

            // Same 4-way snap as the animators: horizontal facing → the wide collider.
            CharacterDirectionUtil.ToFourWay(controller.FacingDirection, out float h, out _);
            bool horizontal = h != 0f;
            if (horizontal == _isHorizontal) return; // no change

            _isHorizontal = horizontal;
            _box.offset = horizontal ? horizontalOffset : _verticalOffset;
            _box.size = horizontal ? horizontalSize : _verticalSize;
        }
    }
}
