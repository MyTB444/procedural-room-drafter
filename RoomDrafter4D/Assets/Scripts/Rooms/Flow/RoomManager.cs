using System.Collections;
using System.Collections.Generic;
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

        [Tooltip("Pools/places island decor objects per room. Optional — auto-found if present.")]
        [SerializeField] private IslandDecorPool decorPool;

        [Header("Start")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private Vector2Int startCoord = Vector2Int.zero;

        [Tooltip("How many cells in FRONT of the entry door to spawn the player (clear of the door trigger).")]
        [SerializeField] private int spawnInset = 2;

        private Vector2Int _coord;
        private bool _transitioning;

        // Island decor placements for the room currently shown — saved into its snapshot on leave,
        // so a cached room re-spawns the same decor without re-running generation.
        private List<PatchPlacement> _currentIslandDecor;

        // Visited rooms, captured on leave and restored on return (includes the hand-made start room).
        private readonly RoomCache _cache = new RoomCache();

        public Vector2Int CurrentCoord => _coord;

        private void Awake()
        {
            // Be forgiving about wiring: the painter usually lives on the same object, and there's
            // one player in the scene. Biome still has to be assigned explicitly.
            if (painter == null) painter = GetComponent<TilemapPainter>();
            if (decorPool == null) decorPool = FindFirstObjectByType<IslandDecorPool>();
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

            // Classify by nearest edge, not position-vs-centre — doors can be anywhere on an edge,
            // and a door cell is always nearest its own edge.
            StartCoroutine(Transition(painter.NearestEdgeWorld(playerWorldPos, biome.Width, biome.Height)));
        }

        private IEnumerator Transition(Cardinal exitDir)
        {
            _transitioning = true;

            if (fader != null) yield return fader.FadeOut();

            // Save the room we're leaving (hand-made start room or generated, with any changes).
            // Island decor objects aren't tiles, so carry their placements on the snapshot too.
            var snapshot = painter.Capture(biome.Width, biome.Height);
            snapshot.IslandDecor = _currentIslandDecor;
            _cache.Save(_coord, snapshot);

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
            {
                painter.Restore(snapshot);
                _currentIslandDecor = snapshot.IslandDecor;
            }
            else
            {
                var grid = new RoomGenerator(biome).Generate(RoomSeed.Rng(worldSeed, coord));
                painter.Paint(grid, biome);
                _currentIslandDecor = grid.IslandDecor;
            }

            // Swap the pooled decor objects over to this room (releases the previous room's).
            if (decorPool != null) decorPool.Show(_currentIslandDecor, painter);

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

    }
}
