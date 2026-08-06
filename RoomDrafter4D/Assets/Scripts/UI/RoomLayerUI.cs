using TMPro;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// HUD text tracking the LAYER the player is currently at (ring distance from the start room —
    /// <see cref="RoomManager.CurrentLayer"/>). Hidden while in the starting area (layer 0); appears
    /// the moment the player enters layer 1 and updates on every room change (polled — room
    /// transitions are rare, 25 int compares per frame are free).
    ///
    /// Setup: drop on an ACTIVE object, assign the TMP text (+ optional root container to toggle —
    /// defaults to the text's own GameObject; this component must NOT sit on the toggled object).
    /// </summary>
    public class RoomLayerUI : MonoBehaviour
    {
        [Tooltip("The text showing the layer, written as prefix + number.")]
        [SerializeField] private TMP_Text text;

        [Tooltip("Shown before the number, e.g. \"Layer \" → \"Layer 3\".")]
        [SerializeField] private string prefix = "Layer ";

        [Tooltip("Container toggled with visibility. Defaults to the text's GameObject.")]
        [SerializeField] private GameObject root;

        private int _shownLayer = -1;

        private void Awake()
        {
            if (root == null && text != null) root = text.gameObject;
            if (root != null) root.SetActive(false); // hidden until the player leaves the start area
        }

        private void Update()
        {
            var rooms = RoomManager.Instance;
            if (rooms == null || text == null) return;

            int layer = rooms.CurrentLayer;
            if (layer == _shownLayer) return;
            _shownLayer = layer;

            bool show = layer >= 1; // the starting area (layer 0) shows nothing
            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (show) text.text = prefix + layer;
        }
    }
}
