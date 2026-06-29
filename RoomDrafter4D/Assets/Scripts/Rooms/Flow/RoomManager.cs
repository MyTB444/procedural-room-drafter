using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Owns the room you're in and the transitions between them. Rooms are deterministic from
    /// (worldSeed, coord) + the biome chosen for them, and ALWAYS painted onto the SAME tilemaps at
    /// the same place — we never create new grids. Walking through a door: fade to black → repaint
    /// the grid for the neighbour room → drop the player in front of the matching entry door → fade in.
    ///
    /// Multiple biomes: <see cref="biomes"/> lists what's available; the player picks the biome for
    /// the NEXT new room via <see cref="SelectBiome"/>. Each room remembers the biome that generated
    /// it (on its snapshot) so a cached room restores its decor/dimensions correctly.
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Available biomes. The player chooses among these; index 0 is the default/start biome.")]
        [SerializeField] private BiomeConfig[] biomes;
        [SerializeField] private TilemapPainter painter;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private Transform player;

        [Tooltip("Pools/places island decor objects per room. Optional — auto-found if present.")]
        [SerializeField] private IslandDecorPool decorPool;

        [Tooltip("Pools/spawns enemies per room. Optional — auto-found if present.")]
        [SerializeField] private EnemyPool enemyPool;

        [Tooltip("Pools collectables; cleared on room change. Optional — auto-found if present.")]
        [SerializeField] private CollectablePool collectablePool;

        [Tooltip("Interactable spawned in front of EACH door: spend a key on it to make that door lead " +
                 "to a Halls room. Optional — leave empty to disable key holders.")]
        [SerializeField] private GameObject keyHolderPrefab;

        [Tooltip("Cells in FRONT of each door to place its KeyHolder.")]
        [SerializeField] private int keyHolderInset = 1;

        [Header("Start")]
        [Tooltip("Roll a fresh world seed every session so rooms differ run-to-run. Turn OFF to keep " +
                 "the fixed 'World Seed' below (reproducible rooms for debugging). Seeds are never " +
                 "saved across sessions either way — within a session it stays fixed so revisited " +
                 "rooms regenerate identically.")]
        [SerializeField] private bool randomizeWorldSeed = true;
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private Vector2Int startCoord = Vector2Int.zero;

        [Tooltip("How many cells in FRONT of the entry door to spawn the player (clear of the door trigger).")]
        [SerializeField] private int spawnInset = 2;

        private static RoomManager _instance;

        /// <summary>The scene's room manager (fake-null re-finds it after a reload). For KeyHolders etc.</summary>
        public static RoomManager Instance =>
            _instance != null ? _instance : (_instance = FindFirstObjectByType<RoomManager>());

        private Vector2Int _coord;
        private bool _transitioning;

        private BiomeConfig _nextBiome;     // chosen by the player for the next NEW room
        private BiomeConfig _currentBiome;  // biome of the room currently shown

        // Per-edge biome override for the CURRENT room's doors (set by a KeyHolder → Halls); null = none.
        // Reset every room load; consumed when the player exits through that edge.
        private readonly BiomeConfig[] _doorBiomeOverride = new BiomeConfig[4];

        // One KeyHolder per door edge, repositioned + re-armed each room.
        private readonly KeyHolder[] _keyHolders = new KeyHolder[4];

        // Island decor placements for the room currently shown — saved into its snapshot on leave,
        // so a cached room re-spawns the same decor without re-running generation.
        private List<PatchPlacement> _currentIslandDecor;

        // Visited rooms, captured on leave and restored on return (includes the hand-made start room).
        private readonly RoomCache _cache = new RoomCache();

        // Walkability + A* for the room currently shown (so enemies path around water).
        private readonly RoomNav _nav = new RoomNav();

        public Vector2Int CurrentCoord => _coord;

        /// <summary>The biomes the player can choose from (for a selection UI).</summary>
        public IReadOnlyList<BiomeConfig> Biomes => biomes;

        /// <summary>The biome the current room was generated with.</summary>
        public BiomeConfig CurrentBiome => _currentBiome;

        /// <summary>Pick the biome used to generate the NEXT new room the player walks into.</summary>
        public void SelectBiome(BiomeConfig biome)
        {
            if (biome != null) _nextBiome = biome;
        }

        /// <summary>Pick the next-room biome by index into <see cref="Biomes"/>.</summary>
        public void SelectBiome(int index)
        {
            if (biomes != null && index >= 0 && index < biomes.Length && biomes[index] != null)
                _nextBiome = biomes[index];
        }

        private void Awake()
        {
            _instance = this;

            // Be forgiving about wiring: the painter usually lives on the same object, and there's
            // one player in the scene. Biomes still have to be assigned explicitly.
            if (painter == null) painter = GetComponent<TilemapPainter>();
            if (decorPool == null) decorPool = FindFirstObjectByType<IslandDecorPool>();
            if (enemyPool == null) enemyPool = FindFirstObjectByType<EnemyPool>();
            if (collectablePool == null) collectablePool = FindFirstObjectByType<CollectablePool>();
            if (player == null)
            {
                var found = FindFirstObjectByType<TRVController>();
                if (found != null) player = found.transform;
            }

            // Roll a session-unique world seed so neighbour rooms aren't identical every run. It's held
            // only in memory (never persisted), and fixed for the rest of this session so a room you
            // walk back into regenerates the same as when you left it.
            if (randomizeWorldSeed)
                worldSeed = System.Guid.NewGuid().GetHashCode();

            // Default biome for the start room + initial selection.
            _nextBiome = (biomes != null && biomes.Length > 0) ? biomes[0] : null;
            _currentBiome = _nextBiome;
        }

        private void Start()
        {
            // The starting room is the hand-made one already painted in the scene — leave it alone.
            // Rooms are generated/painted only when the player walks through a door.
            _coord = startCoord;

            // Prewarm pools with every biome's prefabs so the first spawn of any biome doesn't hitch.
            if (biomes != null)
                foreach (var b in biomes)
                {
                    if (b == null) continue;
                    if (decorPool != null) decorPool.Prewarm(b.IslandDecorObjects);
                    if (enemyPool != null)
                    {
                        enemyPool.Prewarm(b.EnemyPrefabs);
                        enemyPool.Prewarm(b.MinibossPrefabs);
                    }
                }

            // The hand-made start room is already painted; place KeyHolders at its (painted) doors.
            SetupKeyHolders(_currentBiome);
        }

        /// <summary>Called by a <see cref="DoorPortal"/> when the player steps onto a door tile.</summary>
        public void OnPlayerEnteredDoor(Vector3 playerWorldPos)
        {
            if (_transitioning) return;

            // Doors are locked until the room is cleared of enemies.
            if (enemyPool != null && enemyPool.ActiveCount > 0) return;

            if (_currentBiome == null || painter == null)
            {
                Debug.LogError($"[{nameof(RoomManager)}] Can't transition — " +
                    $"{(_currentBiome == null ? "no Biomes assigned" : "Painter missing")} on '{name}'.", this);
                return;
            }

            // Classify by nearest edge, not position-vs-centre — doors can be anywhere on an edge,
            // and a door cell is always nearest its own edge.
            StartCoroutine(Transition(painter.NearestEdgeWorld(playerWorldPos, _currentBiome.Width, _currentBiome.Height)));
        }

        private IEnumerator Transition(Cardinal exitDir)
        {
            _transitioning = true;

            if (fader != null) yield return fader.FadeOut();

            // Save the room we're leaving (its biome + island decor placements ride on the snapshot).
            var snapshot = painter.Capture(_currentBiome.Width, _currentBiome.Height);
            snapshot.IslandDecor = _currentIslandDecor;
            snapshot.Biome = _currentBiome;
            _cache.Save(_coord, snapshot);

            // Did a KeyHolder set the door we're exiting through to lead to Halls?
            var forcedBiome = _doorBiomeOverride[(int)exitDir];

            _coord += exitDir.Offset();
            LoadRoom(_coord, exitDir.Opposite(), forcedBiome); // arrive at the opposite door

            if (fader != null) yield return fader.FadeIn();

            _transitioning = false;
        }

        /// <summary>Generate + paint the room for a coord and place the player on the entry landing.
        /// <paramref name="forcedBiome"/> (from a KeyHolder) overrides the player's selection for a FRESH
        /// room; a cached room keeps its original biome.</summary>
        private void LoadRoom(Vector2Int coord, Cardinal enterFrom, BiomeConfig forcedBiome = null)
        {
            if (_nextBiome == null || painter == null)
            {
                Debug.LogError($"[{nameof(RoomManager)}] Assign Biomes + Painter.", this);
                return;
            }

            // Restore a previously visited room exactly (with its original biome); otherwise generate
            // a fresh one using the player's currently-selected biome. Enemies only spawn on a FRESH
            // room — a cached (already-visited) room comes back cleared.
            BiomeConfig roomBiome;
            List<Vector3> enemyPositions = null;
            if (_cache.TryGet(coord, out var snapshot))
            {
                painter.Restore(snapshot);
                _currentIslandDecor = snapshot.IslandDecor;
                roomBiome = snapshot.Biome != null ? snapshot.Biome : _nextBiome;
            }
            else
            {
                roomBiome = forcedBiome != null ? forcedBiome : _nextBiome; // KeyHolder forces Halls here
                var grid = new RoomGenerator(roomBiome).Generate(RoomSeed.Rng(worldSeed, coord));
                painter.Paint(grid, roomBiome);
                _currentIslandDecor = grid.IslandDecor;
                enemyPositions = PickEnemyPositions(grid, roomBiome, enterFrom);
            }
            _currentBiome = roomBiome;

            // Rebuild the nav grid from the painted room so enemies can path around water/walls.
            _nav.Rebuild(painter, roomBiome.Width, roomBiome.Height);
            RoomNav.Current = _nav;

            // Swap the pooled objects over to this room (each releases the previous room's first).
            if (decorPool != null) decorPool.Show(_currentIslandDecor, painter, roomBiome);
            if (enemyPool != null) enemyPool.Populate(enemyPositions, roomBiome);
            if (collectablePool != null) collectablePool.ReleaseAll(); // clear any uncollected drops from the old room

            // Spawn in FRONT of the actual door on the entry edge (so doors can be anywhere on it).
            if (player != null)
            {
                if (painter.TryGetDoorSpawn(enterFrom, roomBiome.Width, roomBiome.Height, spawnInset, out var world))
                {
                    player.position = world;
                }
                else
                {
                    // Fallback: the generator's formula landing (e.g. no door tile found on that edge).
                    var landing = RoomDoors.Landing(roomBiome, enterFrom);
                    player.position = painter.CellCenterWorld(landing.x, landing.y);
                }
            }

            // Fresh room: clear the door overrides and (re)place a KeyHolder in front of each door.
            for (int i = 0; i < 4; i++) _doorBiomeOverride[i] = null;
            SetupKeyHolders(roomBiome);
        }

        /// <summary>True if the room on the other side of the current room's <paramref name="edge"/> door
        /// has already been generated (it's in the cache) — a KeyHolder there can't change it to Halls.</summary>
        public bool NeighborRoomExists(Cardinal edge) => _cache.TryGet(_coord + edge.Offset(), out _);

        /// <summary>Make the door on <paramref name="edge"/> of the CURRENT room always lead to a Halls
        /// room — applied to the next FRESH room loaded through it. Called by a <see cref="KeyHolder"/>.</summary>
        public void SetDoorToHalls(Cardinal edge)
        {
            var halls = FindBiome(BiomeLayout.Halls);
            if (halls != null) _doorBiomeOverride[(int)edge] = halls;
        }

        private BiomeConfig FindBiome(BiomeLayout layout)
        {
            if (biomes == null) return null;
            foreach (var b in biomes)
                if (b != null && b.Layout == layout) return b;
            return null;
        }

        /// <summary>Position the per-edge KeyHolders in front of each painted door of the current room
        /// and re-arm them. Edges without a door (shouldn't happen) hide their holder.</summary>
        private void SetupKeyHolders(BiomeConfig biome)
        {
            if (keyHolderPrefab == null || biome == null || painter == null) return;

            for (int e = 0; e < 4; e++)
            {
                var edge = (Cardinal)e;
                if (_keyHolders[e] == null)
                {
                    var spawned = Instantiate(keyHolderPrefab);
                    _keyHolders[e] = spawned.GetComponent<KeyHolder>();
                    if (_keyHolders[e] == null) { Destroy(spawned); continue; } // prefab missing a KeyHolder
                }

                var holder = _keyHolders[e];
                if (painter.TryGetDoorSpawn(edge, biome.Width, biome.Height, keyHolderInset, out var pos))
                {
                    holder.transform.position = pos;
                    holder.Configure(edge);
                    holder.gameObject.SetActive(true);
                }
                else
                {
                    holder.gameObject.SetActive(false); // no door on that edge
                }
            }
        }

        /// <summary>Pick up to EnemiesPerRoom distinct open-Floor cells (world positions) to spawn on,
        /// keeping clear of the door the player enters from.</summary>
        private List<Vector3> PickEnemyPositions(RoomGrid grid, BiomeConfig biome, Cardinal enterFrom)
        {
            var positions = new List<Vector3>();
            if (biome.EnemiesPerRoom <= 0 || biome.EnemyPrefabs == null || biome.EnemyPrefabs.Length == 0)
                return positions;

            var entry = RoomDoors.Landing(biome, grid, enterFrom); // where the player arrives
            int safeSq = biome.EnemySpawnSafeRadius * biome.EnemySpawnSafeRadius;

            var floor = new List<Vector2Int>();    // all open floor
            var eligible = new List<Vector2Int>(); // open floor far enough from the entry
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid[x, y] != CellType.Floor) continue; // open floor only — not doors/buildings/water
                    var c = new Vector2Int(x, y);
                    floor.Add(c);
                    int dx = x - entry.x, dy = y - entry.y;
                    if (dx * dx + dy * dy >= safeSq) eligible.Add(c);
                }

            var pickFrom = eligible.Count > 0 ? eligible : floor; // tiny room → fall back to any floor
            for (int i = pickFrom.Count - 1; i > 0; i--) // Fisher–Yates
            {
                int j = Random.Range(0, i + 1);
                (pickFrom[i], pickFrom[j]) = (pickFrom[j], pickFrom[i]);
            }

            int n = Mathf.Min(biome.EnemiesPerRoom, pickFrom.Count);
            for (int i = 0; i < n; i++)
                positions.Add(painter.CellCenterWorld(pickFrom[i].x, pickFrom[i].y));
            return positions;
        }
    }
}
