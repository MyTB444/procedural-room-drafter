using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A data-driven upgrade that compounds one or more stat multipliers onto the player — configure the
    /// modifiers in the inspector. Covers most stat upgrades (e.g. attack: AttackCooldown ×0.8 +
    /// AttackStaminaCost ×0.8; stamina: MaxStamina ×1.25 + StaminaRegen ×1.25).
    /// </summary>
    public class StatUpgrade : Upgrade
    {
        /// <summary>How a modifier changes its stat: scale it (multiplier) or add a flat amount.</summary>
        public enum Op { Multiply, Add }

        [System.Serializable]
        public class Modifier
        {
            [Tooltip("Which stat to modify.")]
            public PlayerUpgrades.Stat Stat;

            [Tooltip("Multiply = scale by Factor (0.8 = −20%, 1.25 = +25%). Add = add Factor to the stat " +
                     "(use for stats that start at 0, e.g. health regen; negative to reduce).")]
            public Op Op = Op.Multiply;

            [Tooltip("Multiply: the multiplier. Add: the flat amount.")]
            public float Factor = 1f;
        }

        [Tooltip("Stat modifiers this upgrade applies.")]
        [SerializeField] private Modifier[] modifiers;

        protected override void Apply(PlayerUpgrades upgrades)
        {
            if (modifiers == null) return;
            foreach (var m in modifiers)
            {
                if (m == null) continue;
                if (m.Op == Op.Add) upgrades.AddStat(m.Stat, m.Factor);
                else upgrades.ScaleStat(m.Stat, m.Factor);
            }
        }
    }
}
