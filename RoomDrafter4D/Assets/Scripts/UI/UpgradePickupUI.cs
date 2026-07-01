using System.Collections;
using TMPro;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shows a brief on-screen message when the player collects an <see cref="Upgrade"/> — each upgrade
    /// carries its own <see cref="Upgrade.Message"/> (e.g. "Attack Speed +50%"). Subscribes to the static
    /// <see cref="Upgrade.Collected"/> event, reveals the text for <see cref="displaySeconds"/>, then hides
    /// it. Collecting another upgrade before it fades restarts the timer with the new message. One instance
    /// serves every upgrade — drop it on a Canvas.
    /// </summary>
    public class UpgradePickupUI : MonoBehaviour
    {
        [Tooltip("The label that shows the upgrade message.")]
        [SerializeField] private TMP_Text text;

        [Tooltip("Object toggled on/off to show the message (e.g. a panel). Defaults to the text's object.")]
        [SerializeField] private GameObject root;

        [Tooltip("How long the message stays on screen (seconds). Uses real time, so a pause won't stall it.")]
        [SerializeField, Min(0f)] private float displaySeconds = 3f;

        private Coroutine _hide;

        private void Awake()
        {
            if (root == null && text != null) root = text.gameObject;
            if (root != null) root.SetActive(false); // hidden until an upgrade is collected
        }

        private void OnEnable() => Upgrade.Collected += Show;
        private void OnDisable() => Upgrade.Collected -= Show;

        private void Show(string message)
        {
            if (text != null) text.text = message;
            if (root != null) root.SetActive(true);

            if (_hide != null) StopCoroutine(_hide);
            _hide = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(displaySeconds);
            if (root != null) root.SetActive(false);
            _hide = null;
        }
    }
}
