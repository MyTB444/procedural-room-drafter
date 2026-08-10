using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TRV
{
    /// <summary>
    /// Drives the player's stamina bar (+ optional text) from <see cref="TRVController"/>. Stamina
    /// regenerates continuously, so this POLLS the controller each frame (unlike the event-driven
    /// <see cref="PlayerHealthUI"/>); the <see cref="Slider"/> mirrors HP — set its maxValue/value to
    /// max/current (make it non-interactable). The controller is auto-found when left unassigned.
    /// </summary>
    public class PlayerStaminaUI : MonoBehaviour
    {
        [Tooltip("The stamina bar — a UI Slider. Its maxValue/value are set to max/current stamina. " +
                 "Make it non-interactable so the player can't drag it.")]
        [SerializeField] private Slider slider;

        [Tooltip("The stamina text (e.g. '80 / 100'). Optional — leave empty to show only the bar.")]
        [SerializeField] private TMP_Text text;

        [Tooltip("Player. Optional — auto-found from the scene's TRVController when left empty.")]
        [SerializeField] private TRVController player;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TRVController>();
        }

        private void Update()
        {
            if (player == null) return;

            float max = player.MaxStamina;
            float current = player.CurrentStamina;
            if (slider != null)
            {
                slider.maxValue = max;
                slider.value = current;
            }
            if (text != null)
                // RoundToInt, not CeilToInt: float bonus maths can leave max at e.g. 120.0000047
                // (float epsilon), which CeilToInt would show as 121.
                text.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
        }
    }
}
