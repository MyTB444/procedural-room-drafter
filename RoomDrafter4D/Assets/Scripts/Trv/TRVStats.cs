using UnityEngine;

namespace TRV
{
    /// <summary>
    /// All tweakable data for a TRV character in one place.
    /// This is a ScriptableObject: create assets from it (Create > TRV > Character Stats),
    /// tune every value in the Inspector, and swap/share stat sets between characters
    /// without touching any code. Nothing here drives behaviour — it is pure data that
    /// the input and action scripts read from.
    /// </summary>
    [CreateAssetMenu(fileName = "TRV_Stats", menuName = "TRV/Character Stats", order = 0)]
    public class TRVStats : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────
        // IDENTITY
        // ─────────────────────────────────────────────────────────────
        [field: Header("Identity")]
        [field: Tooltip("Display name for this character build.")]
        [field: SerializeField] public string CharacterName { get; private set; } = "TRV";

        // ─────────────────────────────────────────────────────────────
        // HEALTH
        // ─────────────────────────────────────────────────────────────
        [field: Header("Health")]
        [field: Tooltip("Maximum (and starting) health.")]
        [field: Min(1f)]
        [field: SerializeField] public float MaxHealth { get; private set; } = 100f;

        // ─────────────────────────────────────────────────────────────
        // MOVEMENT  (this is what makes the 8-way movement feel good)
        // ─────────────────────────────────────────────────────────────
        [field: Header("Movement")]
        [field: Tooltip("Top speed in units/second when at full input.")]
        [field: Min(0f)]
        [field: SerializeField] public float MoveSpeed { get; private set; } = 6f;

        [field: Tooltip("How fast we ramp UP to target speed (units/sec²). " +
                        "Higher = snappier/twitchier, lower = more momentum.")]
        [field: Min(0f)]
        [field: SerializeField] public float Acceleration { get; private set; } = 60f;

        [field: Tooltip("How fast we slow DOWN to a stop when input is released (units/sec²). " +
                        "Higher = stops on a dime, lower = ice-skating glide.")]
        [field: Min(0f)]
        [field: SerializeField] public float Deceleration { get; private set; } = 70f;

        [field: Tooltip("Force movement onto the 8 cardinal/diagonal axes even for analog sticks. " +
                        "Keyboard is already 8-way; enable this for a strictly grid-like feel on gamepad.")]
        [field: SerializeField] public bool SnapTo8Directions { get; private set; } = false;

        [field: Tooltip("Keep diagonal speed equal to straight speed (recommended). " +
                        "If off, holding W+A could move ~1.41x faster.")]
        [field: SerializeField] public bool NormalizeDiagonal { get; private set; } = true;

        [field: Tooltip("Below this input magnitude we treat the stick/keys as released (deadzone).")]
        [field: Range(0f, 0.5f)]
        [field: SerializeField] public float InputDeadzone { get; private set; } = 0.15f;

        [field: Tooltip("Forgiveness window (seconds) when releasing a diagonal. If you let go of one " +
                        "key slightly before the other, the idle keeps the diagonal facing instead of " +
                        "snapping to a cardinal. ~0.08–0.15s feels natural; 0 disables it.")]
        [field: Range(0f, 0.3f)]
        [field: SerializeField] public float DiagonalReleaseGrace { get; private set; } = 0.1f;

        // ─────────────────────────────────────────────────────────────
        // DASH  (a short, fast burst — replaces sprint)
        // ─────────────────────────────────────────────────────────────
        [field: Header("Dash")]
        [field: Tooltip("Speed of the dash burst in units/second. Set well above MoveSpeed for a snappy lunge.")]
        [field: Min(0f)]
        [field: SerializeField] public float DashSpeed { get; private set; } = 22f;

        [field: Tooltip("How long the dash lasts in seconds. Keep it short (≈0.1–0.2s) for a quick dash. " +
                        "Dash distance ≈ DashSpeed × DashDuration.")]
        [field: Min(0.01f)]
        [field: SerializeField] public float DashDuration { get; private set; } = 0.15f;

        [field: Tooltip("Minimum seconds between dashes (cooldown).")]
        [field: Min(0f)]
        [field: SerializeField] public float DashCooldown { get; private set; } = 0.6f;

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
