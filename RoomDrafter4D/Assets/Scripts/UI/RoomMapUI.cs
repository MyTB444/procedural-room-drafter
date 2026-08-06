using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TRV
{
    /// <summary>
    /// A 5×5 minimap of the rooms around the player (2 grid layers each way). Every cell keeps its
    /// own (shared) square sprite and is TINTED by what's drafted at that coordinate — blank
    /// (nothing yet), Lands (black), Anubis (red), Aqua (blue), Halls (purple), all editor-tunable
    /// — resolved by the room's biome LAYOUT via <see cref="RoomManager.TryGetRoomBiome"/> (the
    /// cache + the current room).
    ///
    /// Setup: give <see cref="gridParent"/> a GridLayoutGroup constrained to 5 columns. Cells are
    /// either the parent's EXISTING Image children (make 25, row-major, top-left first) or, when
    /// <see cref="cellPrefab"/> is assigned, 25 clones instantiated at runtime. Refreshes every
    /// frame (25 dictionary lookups — trivial).
    /// </summary>
    public class RoomMapUI : MonoBehaviour
    {
        private const int Radius = 2; // 2 layers around the player → 5×5

        [Tooltip("The grid container (GridLayoutGroup, 5 columns, fills top-left → right).")]
        [SerializeField] private Transform gridParent;

        [Tooltip("Optional cell template — when set, 25 clones are created under the grid parent. " +
                 "Leave empty to use the parent's existing Image children instead (make 25).")]
        [SerializeField] private Image cellPrefab;

        [Tooltip("The square sprite every cell uses (tinted per room). Applied to all cells on " +
                 "start; leave empty to keep whatever sprite the cell Images already have.")]
        [SerializeField] private Sprite cellSprite;

        [Header("Cell colours (tint of the cell sprite)")]
        [Tooltip("No room drafted at that coordinate (default: invisible).")]
        [SerializeField] private Color blankColor = Color.clear;
        [SerializeField] private Color landsColor = Color.black;
        [SerializeField] private Color anubisColor = Color.red;
        [SerializeField] private Color aquaColor = Color.blue;
        [SerializeField] private Color hallsColor = new Color(0.6f, 0.2f, 0.8f); // purple

        [Tooltip("The map view toggled by the M key (e.g. the panel holding the grid + frame). " +
                 "Must be a CHILD of this component's object (which stays active to read the key). " +
                 "Defaults to the grid parent. Starts hidden.")]
        [SerializeField] private GameObject viewRoot;

        [Tooltip("TESTING: tick to fill every cell, cycling through the four biome colours — " +
                 "preview how the full grid looks with all images spawned. Live-togglable in play " +
                 "mode; untick to go back to the real map.")]
        [SerializeField] private bool testFillAll;

        [Tooltip("Optional — auto-found when empty.")]
        [SerializeField] private RoomManager roomManager;

        private Image[] _cells; // row-major, top row (north) first
        private TMP_Text[] _cellTexts; // optional per-cell layer label (a TMP child of the cell image)
        private bool _initialized;

        private void Awake() => EnsureInit();

        /// <summary>Idempotent setup — also run from <see cref="Toggle"/>, so the map works even
        /// when this component sits on an object that starts INACTIVE (Awake never ran).</summary>
        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            if (roomManager == null) roomManager = FindAnyObjectByType<RoomManager>(FindObjectsInactive.Include);
            if (viewRoot == null && gridParent != null) viewRoot = gridParent.gameObject;
            BuildCells();
            if (viewRoot != null) viewRoot.SetActive(false); // hidden until M is pressed
        }

        private bool _visible;

        /// <summary>Show/hide the map — driven by <see cref="RoomManager"/> on the M key (polled
        /// there, so it works no matter where this component lives in the hierarchy).</summary>
        public void Toggle()
        {
            EnsureInit();
            if (viewRoot == null) return;
            // The map is non-blocking and may open over any UI — EXCEPT the pause menu/death
            // screen, which block everything (turning it OFF is always allowed).
            if (!_visible && UIGate.MenuActive) return;
            _visible = !_visible;
            if (viewRoot == gameObject)
            {
                gameObject.SetActive(_visible); // the view IS this object — toggle it directly
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true); // component may start disabled
            viewRoot.SetActive(_visible);
        }

        private void BuildCells()
        {
            if (gridParent == null) return;
            int total = (Radius * 2 + 1) * (Radius * 2 + 1);
            _cells = new Image[total];

            if (cellPrefab != null)
            {
                for (int i = 0; i < total; i++)
                    _cells[i] = Instantiate(cellPrefab, gridParent);
            }
            else
            {
                // No template — use the grid's existing Image children (authored in the scene).
                int found = 0;
                foreach (Transform child in gridParent)
                {
                    if (found == total) break;
                    if (child.TryGetComponent<Image>(out var image)) _cells[found++] = image;
                }
            }

            if (cellSprite != null)
                foreach (var cell in _cells)
                    if (cell != null) cell.sprite = cellSprite;

            // Each cell's LAYER label: the TMP text authored as a child of the cell image (none = no label).
            _cellTexts = new TMP_Text[total];
            for (int i = 0; i < total; i++)
                if (_cells[i] != null)
                    _cellTexts[i] = _cells[i].GetComponentInChildren<TMP_Text>(true);
        }

        private void Update()
        {
            if (viewRoot != null && !viewRoot.activeSelf) return; // hidden — nothing to refresh
            if (_cells == null) return;

            if (testFillAll)
            {
                var cycle = new[] { landsColor, anubisColor, aquaColor, hallsColor };
                int c = 0;
                for (int dy = Radius; dy >= -Radius; dy--)
                    for (int dx = -Radius; dx <= Radius; dx++, c++)
                    {
                        if (_cells[c] != null) _cells[c].color = cycle[c % cycle.Length];
                        if (_cellTexts[c] != null) // preview: ring distance from the map's centre
                            _cellTexts[c].text = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)).ToString();
                    }
                return;
            }

            if (roomManager == null) return;

            var centre = roomManager.CurrentCoord;
            int i = 0;
            for (int dy = Radius; dy >= -Radius; dy--)          // top row = north
                for (int dx = -Radius; dx <= Radius; dx++, i++)
                {
                    var coord = centre + new Vector2Int(dx, dy);
                    var cell = _cells[i];
                    if (cell != null) cell.color = ColorFor(coord);

                    // Layer label: only on DRAFTED rooms, and never on the start room (layer 0).
                    if (_cellTexts[i] != null)
                    {
                        int layer = roomManager.LayerOf(coord);
                        bool drafted = roomManager.TryGetRoomBiome(coord, out var biome) && biome != null;
                        _cellTexts[i].text = drafted && layer > 0 ? layer.ToString() : "";
                    }
                }
        }

        private Color ColorFor(Vector2Int coord)
        {
            if (!roomManager.TryGetRoomBiome(coord, out var biome) || biome == null)
                return blankColor;
            return biome.Layout switch
            {
                BiomeLayout.Plain => landsColor,
                BiomeLayout.Open => anubisColor,
                BiomeLayout.Corridors => aquaColor,
                BiomeLayout.Halls => hallsColor,
                _ => blankColor,
            };
        }
    }
}
