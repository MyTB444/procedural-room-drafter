using TMPro;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shows the player's EFFECTIVE stats (upgrades applied) on the menu screen: max HP, max
    /// stamina, HP regen, stamina regen, attack damage, attack cooldown and move speed — each an
    /// optional TMP text (leave any you don't want empty). Values come from the
    /// <see cref="TRVController"/> display properties, so upgrade multipliers/bonuses are included.
    ///
    /// Put this on (a child of) the pause-menu root and assign the texts: it refreshes on enable
    /// and every frame while visible (the menu root being inactive stops the polling), so stats
    /// picked up between menu openings always show current.
    /// </summary>
    public class PlayerStatsUI : MonoBehaviour
    {
        [Header("Stat value texts (optional — leave empty to skip)")]
        [SerializeField] private TMP_Text maxHealthText;
        [SerializeField] private TMP_Text maxStaminaText;
        [SerializeField] private TMP_Text healthRegenText;
        [SerializeField] private TMP_Text staminaRegenText;
        [SerializeField] private TMP_Text attackDamageText;
        [SerializeField] private TMP_Text attackCooldownText;
        [SerializeField] private TMP_Text moveSpeedText;

        [Tooltip("Number format for the values (e.g. 0.## = up to two decimals, no trailing zeros).")]
        [SerializeField] private string numberFormat = "0.##";

        [Tooltip("Optional — auto-found when empty.")]
        [SerializeField] private TRVController player;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TRVController>(FindObjectsInactive.Include);
        }

        private void OnEnable() => Refresh();

        // Cheap (7 text writes) and typically only runs while the menu is open — keeps the panel
        // live even if an upgrade could somehow apply while it shows.
        private void Update() => Refresh();

        private void Refresh()
        {
            if (player == null) return;
            Set(maxHealthText, player.MaxHealth);
            Set(maxStaminaText, player.MaxStamina);
            Set(healthRegenText, player.HealthRegenPerSecond);
            Set(staminaRegenText, player.StaminaRegenPerSecond);
            Set(attackDamageText, player.AttackDamage);
            Set(attackCooldownText, player.AttackCooldownSeconds);
            Set(moveSpeedText, player.MoveSpeed);
        }

        private void Set(TMP_Text text, float value)
        {
            if (text != null) text.text = value.ToString(numberFormat);
        }
    }
}
