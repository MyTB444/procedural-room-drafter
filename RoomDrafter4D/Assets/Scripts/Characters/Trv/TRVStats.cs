using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Player-specific stats for TRV. Inherits all shared stats from <see cref="CharacterStats"/>
    /// (health, movement, combat) and adds the human-input feel and dash tuning that only the
    /// player needs. Create assets via Create ▸ TRV ▸ Character Stats.
    /// </summary>
    [CreateAssetMenu(fileName = "TRV_Stats", menuName = "TRV/Character Stats", order = 0)]
    public class TRVStats : CharacterStats
    {
        // ─────────────────────────────────────────────────────────────
        // PLAYER INPUT FEEL  (only meaningful for human/analog input)
        // ─────────────────────────────────────────────────────────────
        [field: Header("Player Input Feel")]
        [field: Tooltip("Force movement onto the 8 cardinal/diagonal axes even for analog sticks. " +
                        "Keyboard is already 8-way; enable for a strictly grid-like feel on gamepad.")]
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
        // DASH  (a short, fast burst)
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
    }
}
