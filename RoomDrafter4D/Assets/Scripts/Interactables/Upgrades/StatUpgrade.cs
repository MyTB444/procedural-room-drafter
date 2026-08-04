using UnityEngine;

namespace TRV
{
    /// <summary>
    /// A data-driven upgrade that adds FLAT +number bonuses to one or more player stats — configure
    /// the modifiers in the inspector (e.g. MaxStamina +25, HealthRegen +2; negative amounts reduce
    /// a stat, e.g. AttackCooldown −0.1s for a faster attack).
    /// </summary>
    public class StatUpgrade : Upgrade
    {
        [System.Serializable]
        public class Modifier
        {
            [Tooltip("Which stat to modify.")]
            public PlayerUpgrades.Stat Stat;

            [Tooltip("Flat amount ADDED to the stat (+number; negative to reduce, e.g. −0.1 attack " +
                     "cooldown for a faster attack).")]
            public float Amount = 1f;
        }

        [Tooltip("Stat bonuses this upgrade applies.")]
        [SerializeField] private Modifier[] modifiers;

        protected override void Apply(PlayerUpgrades upgrades)
        {
            if (modifiers == null) return;
            foreach (var m in modifiers)
            {
                if (m == null) continue;
                upgrades.AddStat(m.Stat, m.Amount);
            }
        }
    }
}
