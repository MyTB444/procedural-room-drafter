using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Stats for an enemy: inherits the actor-agnostic <see cref="CharacterStats"/> (health,
    /// movement, Damage/AttackCooldown/AttackRange) and adds the AI tuning shared by
    /// melee enemies — detection range, wander cadence, and the post-attack halt. Subclass for
    /// archetype-specific extras. Create via Create ▸ TRV ▸ Enemy Stats.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStats", menuName = "TRV/Enemy Stats", order = 2)]
    public class EnemyStats : CharacterStats
    {
        [field: Header("Detection")]
        [field: Tooltip("The player is chased once within this distance (world units); outside it, " +
                        "the enemy wanders.")]
        [field: Min(0f)]
        [field: SerializeField] public float DetectionRadius { get; private set; } = 6f;

        [field: Header("Wander (player out of range)")]
        [field: Tooltip("Fraction of MoveSpeed used while wandering — roaming is calmer than a chase.")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float WanderSpeedMultiplier { get; private set; } = 0.5f;

        [field: Tooltip("Idle pause duration, picked randomly in [x, y] seconds.")]
        [field: SerializeField] public Vector2 IdleDurationRange { get; private set; } = new Vector2(0.8f, 2f);

        [field: Tooltip("Wander-move duration, picked randomly in [x, y] seconds.")]
        [field: SerializeField] public Vector2 WanderDurationRange { get; private set; } = new Vector2(0.6f, 1.5f);

        [field: Header("Attack")]
        [field: Tooltip("The attack's opening HALT: the moment it attacks, the enemy stops and the attack " +
                        "animation plays for this long before the hitbox lands (the windup is part of the " +
                        "attack, not an idle pause). 0 = the hit lands immediately. Set the child " +
                        "AttackHitbox's own delay to 0 and tune the windup here.")]
        [field: Min(0f)]
        [field: SerializeField] public float AttackWindup { get; private set; } = 0.3f;

        [field: Tooltip("After attacking, the enemy halts (no movement) for this long — a recovery " +
                        "window before it can chase/attack again.")]
        [field: Min(0f)]
        [field: SerializeField] public float AttackHaltDuration { get; private set; } = 0.6f;
    }
}
