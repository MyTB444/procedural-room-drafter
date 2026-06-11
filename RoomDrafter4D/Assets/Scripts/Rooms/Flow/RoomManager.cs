using System.Collections;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Owns the room you're in and the transitions between them. Rooms are deterministic from
    /// (worldSeed, coord) and ALWAYS painted onto the SAME tilemaps at the same place — we never
    /// create new grids. Walking through a door: fade to black → repaint the grid for the neighbour
    /// room → drop the player in front of the matching entry door (found in the painted Door layer,
    /// so doors can sit anywhere on an edge) → fade back in.
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BiomeConfig biome;
        [SerializeField] private TilemapPainter painter;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private Transform player;

        [Header("Start")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private Vector2Int startCoord = Vector2Int.zero;

        [Tooltip("How many cells in FRONT of the entry door to spawn the player (clear of the door trigger).")]
        [SerializeField] private int spawnInset = 2;

        private Vector2Int _coord;
        private bool _transitioning;

        // Visited rooms, captured on leave and restored on return (includes the hand-made start room).
        private readonly RoomCache _cache = new RoomCache();

        public Vector2Int CurrentCoord => _coord;

        private void Awake()
        {
            // Be forgiving about wiring: the painter usually lives on the same object, and there's
            // one player in the scene. Biome still has to be assigned explicitly.
            if (painter == null) painter = GetComponent<TilemapPainter>();
            if (player == null)
            {
                var found = FindFirstObjectByType<TRVController>();
                if (found != null) player = found.transform;
            }
        }

        private void Start()
        {
            // The starting room is the hand-made one already painted in the scene — leave it alone.
            // Rooms are generated/painted only when the player walks through a door.
            _coord = startCoord;
        }

        /// <summary>Called by a <see cref="DoorPortal"/> when the player steps onto a door tile.</summary>
        public void OnPlayerEnteredDoor(Vector3 playerWorldPos)
        {
            if (_transitioning) return;

            if (biome == null || painter == null)
            {
                Debug.LogError($"[{nameof(RoomManager)}] Can't transition — " +
                    $"{(biome == null ? "Biome" : "Painter")} is not assigned on '{name}'.", this);
                return;
            }

            StartCoroutine(Transition(DirectionFromCenter(playerWorldPos)));
        }

        private IEnumerator Transition(Cardinal exitDir)
        {
            _transitioning = true;

            if (fader != null) yield return fader.FadeOut();

            // Save the room we're leaving (hand-made start room or generated, with any changes).
            _cache.Save(_coord, painter.Capture(biome.Width, biome.Height));

            _coord += exitDir.Offset();
            LoadRoom(_coord, exitDir.Opposite()); // arrive at the opposite door

            if (fader != null) yield return fader.FadeIn();

            _transitioning = false;
        }

        /// <summary>Generate + paint the room for a coord and place the player on the entry landing.</summary>
        private void LoadRoom(Vector2Int coord, Cardinal enterFrom)
        {
            if (biome == null || painter == null)
            {
                Debug.LogError($"[{nameof(RoomManager)}] Assign Biome + Painter.", this);
                return;
            }

            // Restore a previously visited room exactly; otherwise generate a fresh one.
            if (_cache.TryGet(coord, out var snapshot))
                painter.Restore(snapshot);
            else
                painter.Paint(new RoomGenerator(biome).Generate(RoomSeed.Rng(worldSeed, coord)), biome);

            // Spawn in FRONT of the actual door on the entry edge (so doors can be anywhere on it).
            if (player != null)
            {
                if (painter.TryGetDoorSpawn(enterFrom, biome.Width, biome.Height, spawnInset, out var world))
                {
                    player.position = world;
                }
                else
                {
                    // Fallback: the generator's formula landing (e.g. no door tile found on that edge).
                    var landing = RoomDoors.Landing(biome, enterFrom);
                    player.position = painter.CellCenterWorld(landing.x, landing.y);
                }
            }
        }

        /// <summary>Which door the player used, from their position relative to room centre.</summary>
        private Cardinal DirectionFromCenter(Vector3 worldPos)
        {
            Vector3 center = painter.CellCenterWorld(biome.Width / 2, biome.Height / 2);
            Vector3 delta = worldPos - center;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x >= 0 ? Cardinal.East : Cardinal.West;
            return delta.y >= 0 ? Cardinal.North : Cardinal.South;
        }
    }
}
