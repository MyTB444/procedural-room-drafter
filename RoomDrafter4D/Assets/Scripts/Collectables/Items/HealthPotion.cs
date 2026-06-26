using UnityEngine;

namespace TRV
{
    /// <summary>An HP pot pickup: restores a fixed amount of health to the player on collect.</summary>
    public class HealthPotion : Collectable
    {
        [Header("Effect")]
        [Tooltip("How much health this restores when collected.")]
        [SerializeField, Min(0f)] private float healAmount = 5f;

        protected override void OnCollected(TRVController player)
        {
            if (player != null && player.TryGetComponent<Health>(out var health))
                health.Heal(healAmount);
        }
    }
}
