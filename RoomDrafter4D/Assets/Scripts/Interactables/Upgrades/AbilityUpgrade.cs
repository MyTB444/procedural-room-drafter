using UnityEngine;

namespace TRV
{
    /// <summary>An upgrade that unlocks an ability — e.g. the dash, which is locked by default until
    /// the player picks this up.</summary>
    public class AbilityUpgrade : Upgrade
    {
        [Tooltip("Which ability this upgrade unlocks.")]
        [SerializeField] private PlayerUpgrades.Ability ability = PlayerUpgrades.Ability.Dash;

        protected override void Apply(PlayerUpgrades upgrades) => upgrades.Unlock(ability);
    }
}
