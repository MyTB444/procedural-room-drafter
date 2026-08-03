using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TRV
{
    /// <summary>
    /// Drives the player's HP bar + HP text from the shared <see cref="Health"/> component. Subscribes
    /// to <see cref="Health.Changed"/>, so it refreshes the instant the player is healed or takes damage;
    /// it also syncs once on enable in case Health initialised before this woke up. The bar is a UI
    /// <see cref="Slider"/> (driven by its <c>maxValue</c>/<c>value</c> = max/current — make it
    /// non-interactable); the text is optional. Health is auto-found from the scene's
    /// <see cref="TRVController"/> when left unassigned.
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour
    {
        [Tooltip("The HP bar — a UI Slider. Its maxValue/value are set to the player's max/current HP. " +
                 "Make it non-interactable so the player can't drag it.")]
        [SerializeField] private Slider slider;

        [Tooltip("The HP text (e.g. '80 / 100'). Optional — leave empty to show only the bar.")]
        [SerializeField] private TMP_Text text;

        [Tooltip("Player Health. Optional — auto-found from the scene's TRVController when left empty.")]
        [SerializeField] private Health health;

        private void Awake()
        {
            if (health == null)
            {
                var player = FindAnyObjectByType<TRVController>();
                if (player != null) health = player.GetComponent<Health>();
            }
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.Changed += OnHealthChanged;
            Refresh(health.Current); // sync now — Health.Init may have fired Changed before we subscribed
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
        }

        private void OnHealthChanged(float current) => Refresh(current);

        private void Refresh(float current)
        {
            float max = health.Max;
            if (slider != null)
            {
                slider.maxValue = max;
                slider.value = current;
            }
            if (text != null)
                text.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
