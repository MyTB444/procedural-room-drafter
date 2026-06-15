using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shared, actor-agnostic stats for ANY character — player or enemy: health, movement
    /// feel, and basic combat. Create assets directly for simple enemies
    /// (Create ▸ TRV ▸ Character Stats (Base)), or subclass it for actor-specific extras
    /// (see <see cref="TRVStats"/> which adds player input-feel + dash).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStats", menuName = "TRV/Character Stats (Base)", order = 1)]
    public class CharacterStats : ScriptableObject
    {
        [field: Header("Identity")]
        [field: Tooltip("Display name for this character build.")]
        [field: SerializeField] public string CharacterName { get; private set; } = "Character";

        // ─────────────────────────────────────────────────────────────
        // HEALTH
        // ─────────────────────────────────────────────────────────────
        [field: Header("Health")]
        [field: Tooltip("Maximum (and starting) health.")]
        [field: Min(1f)]
        [field: SerializeField] public float MaxHealth { get; private set; } = 100f;

        // ─────────────────────────────────────────────────────────────
        // MOVEMENT
        // ─────────────────────────────────────────────────────────────
        [field: Header("Movement")]
        [field: Tooltip("Top speed in units/second at full input.")]
        [field: Min(0f)]
        [field: SerializeField] public float MoveSpeed { get; private set; } = 6f;

        [field: Tooltip("How fast we ramp UP to target speed (units/sec²). " +
                        "Higher = snappier, lower = more momentum.")]
        [field: Min(0f)]
        [field: SerializeField] public float Acceleration { get; private set; } = 60f;

        [field: Tooltip("How fast we slow DOWN to a stop (units/sec²). " +
                        "Higher = stops on a dime, lower = ice-skating glide.")]
        [field: Min(0f)]
        [field: SerializeField] public float Deceleration { get; private set; } = 70f;

        // ─────────────────────────────────────────────────────────────
        // COMBAT
        // ─────────────────────────────────────────────────────────────
        [field: Header("Combat")]
        [field: Tooltip("Damage dealt per attack.")]
        [field: Min(0f)]
        [field: SerializeField] public float Damage { get; private set; } = 15f;

        [field: Tooltip("Minimum seconds between attacks.")]
        [field: Min(0f)]
        [field: SerializeField] public float AttackCooldown { get; private set; } = 0.35f;

        [field: Tooltip("Reach of an attack in world units.")]
        [field: Min(0f)]
        [field: SerializeField] public float AttackRange { get; private set; } = 1.2f;
    }
}
