using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TRV
{
    /// <summary>
    /// The ONLY script that talks to the Input System. It reads the existing
    /// "InputSystem_Actions" asset (the "Player" action map) and re-exposes
    /// everything as clean, engine-agnostic data:
    ///
    ///   • <see cref="MoveInput"/> / <see cref="PointerScreenPosition"/> — polled each frame.
    ///   • <see cref="DashPressed"/> / <see cref="AttackPressed"/> / <see cref="FirePressed"/> /
    ///     <see cref="InteractPressed"/> — C# events.
    ///
    /// Nothing else in the game references InputAction, key codes, or devices, so
    /// rebinding or swapping the input backend never reaches gameplay code.
    /// </summary>
    public class TRVInput : MonoBehaviour
    {
        [Tooltip("Assign the project's InputSystem_Actions asset here.")]
        [SerializeField] private InputActionAsset actions;

        [Tooltip("Names inside the Input Actions asset. Change only if you rename them in the asset.")]
        [SerializeField] private string actionMap = "Player";
        [SerializeField] private string moveActionName = "Move";
        [Tooltip("Reuses the existing 'Sprint' binding (Left Shift) as the dash trigger.")]
        [SerializeField] private string dashActionName = "Sprint";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string interactActionName = "Interact";

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _dash;
        private InputAction _attack;
        private InputAction _interact;

        /// <summary>Raw 2D move axis this frame, components in [-1, 1].</summary>
        public Vector2 MoveInput => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>Pointer (mouse / pen / touch) position in screen pixels; Vector2.zero if none.</summary>
        public Vector2 PointerScreenPosition =>
            Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

        /// <summary>Raised the moment the dash control is pressed.</summary>
        public event Action DashPressed;

        /// <summary>Raised the moment the attack control is pressed.</summary>
        public event Action AttackPressed;

        /// <summary>Raised the moment the FIRE control (right mouse button) is pressed. Polled from
        /// the mouse directly — no action for it exists in the input asset.</summary>
        public event Action FirePressed;

        /// <summary>Raised when the interact control completes.</summary>
        public event Action InteractPressed;

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"[{nameof(TRVInput)}] No Input Actions asset assigned on '{name}'. " +
                               "Drag InputSystem_Actions into the 'Actions' field.", this);
                enabled = false;
                return;
            }

            _map = actions.FindActionMap(actionMap, throwIfNotFound: true);
            _move = _map.FindAction(moveActionName, throwIfNotFound: true);
            _dash = _map.FindAction(dashActionName, throwIfNotFound: true);
            _attack = _map.FindAction(attackActionName, throwIfNotFound: true);
            _interact = _map.FindAction(interactActionName, throwIfNotFound: true);
        }

        private void OnEnable()
        {
            if (_map == null) return;
            _dash.performed += OnDashPerformed;
            _attack.performed += OnAttackPerformed;
            _interact.performed += OnInteractPerformed;
            _map.Enable();
        }

        private void OnDisable()
        {
            if (_map == null) return;
            _dash.performed -= OnDashPerformed;
            _attack.performed -= OnAttackPerformed;
            _interact.performed -= OnInteractPerformed;
            _map.Disable();
        }

        private void Update()
        {
            // Right click = ranged fire. The actions asset has no binding for it, so poll directly.
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                FirePressed?.Invoke();
        }

        private void OnDashPerformed(InputAction.CallbackContext _) => DashPressed?.Invoke();
        private void OnAttackPerformed(InputAction.CallbackContext _) => AttackPressed?.Invoke();
        private void OnInteractPerformed(InputAction.CallbackContext _) => InteractPressed?.Invoke();
    }
}
