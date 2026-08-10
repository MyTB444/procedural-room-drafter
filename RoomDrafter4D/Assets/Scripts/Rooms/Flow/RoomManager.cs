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

        [Header("Layers (difficulty scaling)")]
        [Tooltip("Layer = ring distance from the start room (Chebyshev — the first ring around the " +
                 "start is layer 1). Scaling KICKS IN at this layer: each layer from here on adds " +
                 "one scaling STEP (layer 1 is always unscaled).")]
        [SerializeField, Min(2)] private int scalingStartLayer = 2;

        [Tooltip("Enemy MAX HP increase per scaling step (0.1 = +10% each).")]
        [SerializeField, Min(0f)] private float enemyHealthPercentPerLayer = 0.1f;

        [Tooltip("Enemy DAMAGE increase per scaling step (0.1 = +10% each).")]
        [SerializeField, Min(0f)] private float enemyDamagePercentPerLayer = 0.1f;

        [Tooltip("Extra enemies spawned per room per enemy-step (on top of the biome's EnemiesPerRoom).")]
        [SerializeField, Min(0)] private int extraEnemiesPerLayer = 1;

        [Tooltip("How many LAYERS it takes to add one enemy-step — 2 = the enemy count grows every " +
                 "2nd layer (first increase still at Scaling Start Layer); 1 = every layer.")]
        [SerializeField, Min(1)] private int layersPerExtraEnemy = 2;

        [Tooltip("Extra LEVELS a book choice grants per scaling step (on top of its base 1).")]
        [SerializeField, Min(0)] private int bookLevelsPerLayer = 1;

        [Tooltip("From this layer on, the DEFAULT (Lands) biome can no longer be drafted: the draft " +
                 "UI hides its Skip button, and a keyless player can't pass a door leading there.")]
        [SerializeField, Min(1)] private int noLandsFromLayer = 4;

        [Header("Room clear reward")]
        [Tooltip("HP pot prefab (a Collectable, e.g. the HealthPotion) dropped at the room's middle " +
                 "when its last enemy dies. Empty = no reward.")]
        [SerializeField] private GameObject clearRewardPotPrefab;

        [Tooltip("Pots per LAYER on clear (count = layer × this; layer 0/start = none).")]
        [SerializeField, Min(0)] private int potsPerLayerOnClear = 1;

        [Tooltip("How far around the middle point the pots scatter (world units).")]
        [SerializeField, Min(0f)] private float clearRewardScatter = 0.6f;

        private static RoomManager _instance;

        /// <summary>The scene's room manager (fake-null re-finds it after a reload).</summary>
        public static RoomManager Instance =>
            _instance != null ? _instance : (_instance = FindAnyObjectByType<RoomManager>());

        /// <summary>The current room's LAYER: Chebyshev ring distance from the start room (start = 0,
        /// the ring around it = 1, and so on outward).</summary>
        public int CurrentLayer =>
            Mathf.Max(Mathf.Abs(_coord.x - startCoord.x), Mathf.Abs(_coord.y - startCoord.y));

        /// <summary>Scaling steps for the current layer: 0 until <see cref="scalingStartLayer"/>,
        /// then +1 per layer (layer 2 = 1 step, layer 3 = 2 steps, … with the default start).</summary>
        public int LayerSteps => Mathf.Max(0, CurrentLayer - scalingStartLayer + 1);

        /// <summary>Enemy max-HP multiplier for the current layer (1 = unscaled).</summary>
        public float EnemyHealthMultiplier => 1f + enemyHealthPercentPerLayer * LayerSteps;

        /// <summary>Enemy damage multiplier for the current layer (1 = unscaled).</summary>
        public float EnemyDamageMultiplier => 1f + enemyDamagePercentPerLayer * LayerSteps;

        /// <summary>Extra enemies added to every room at the current layer — grows one enemy-step
        /// every <see cref="layersPerExtraEnemy"/> layers (ceil, so the first step still lands ON
        /// the scaling start layer: with the defaults L2→+1, L4→+2, L6→+3…).</summary>
        public int ExtraEnemies =>
            extraEnemiesPerLayer * ((LayerSteps + layersPerExtraEnemy - 1) / layersPerExtraEnemy);

        /// <summary>Extra levels a book choice grants at the current layer (on top of its base 1).</summary>
        public int BonusBookLevels => bookLevelsPerLayer * LayerSteps;

        /// <summary>Layer (ring distance from the start) of an arbitrary room coordinate — 0 = the
        /// start room. Also used by the map UI to label drafted rooms.</summary>
        public int LayerOf(Vector2Int coord) =>
            Mathf.Max(Mathf.Abs(coord.x - startCoord.x), Mathf.Abs(coord.y - startCoord.y));

        /// <summary>Whether the pending draft's target room may take the DEFAULT (Lands) biome —
        /// false from <see cref="noLandsFromLayer"/> on (the draft UI hides its Skip button).</summary>
        public bool PendingDraftAllowsDefault => LayerOf(_pendingDraftCoord) < noLandsFromLayer;

        private Vector2Int _pendingDraftCoord; // target coord of the draft currently open/starting

        [Tooltip("The room-draft UI opened when the player steps on a door. Optional — auto-found " +
                 "from the scene; when none exists, doors transition immediately (old behaviour).")]
        [SerializeField] private RoomDraftUI draftUI;

        private Vector2Int _coord;
        private bool _transitioning;
        private bool _drafting;        // the draft UI is open, waiting for a choice
        private bool _draftSuppressed; // player cancelled — no new draft until they step OFF the door
        private bool _warnedNoDraftUI; // one-shot setup warning
        private Vector3 _pendingDoorPos;
        private PlayerInventory _inventory; // lazily found — for the any-keys draft gate
        private RoomMapUI _mapUI;           // lazily found — toggled with the M key

        private BiomeConfig _nextBiome;     // chosen by the player for the next NEW room
        private bool _nextBiomeForced;      // TESTING (BiomeHotkeys): skip the draft flow until a fresh room consumes the pick
        private BiomeConfig _currentBiome;  // biome of the room currently shown

        // Island decor placements for the room currently shown — saved into its snapshot on leave,
        // so a cached room re-spawns the same decor without re-running generation.
        private List<PatchPlacement> _currentIslandDecor;

        // Upgrade books of the room currently shown: world spots + their live instances (parallel
        // lists). Unconsumed books persist on the snapshot; a consumed one stays gone on revisit.
        private List<Vector3> _currentBookSpots;
        private readonly List<GameObject> _bookInstances = new List<GameObject>();
        private readonly HashSet<Vector2Int> _openBookCells = new HashSet<Vector2Int>(); // Anubis book grid cells (barrels avoid them)
        private List<Vector2Int> _currentBarrelCells; // unbroken Anubis barrels (breaks persist via this list)
        private bool _roomHadEnemies; // armed on a fresh spawn; the clear reward fires when the count hits 0

        // Rolled main-room content for the room currently shown (Halls big igroom): the interior rect
        // and the chosen filler (-1 = nothing). Saved into the snapshot on leave so a revisited room
        // re-spawns the same content. (Upgrades come from the BOOK system — see SpawnBooks.)
        private RectInt _currentMainArea;
        private int _currentMainFillerIndex = -1;
        private List<Vector2Int> _currentMainFillerCells; // unbroken filler cells (breaks persist via this list)
        private List<Vector2Int> _currentVaseCells; // unbroken Lands vase patches (breaks persist via this list)
        private List<Vector2Int> _currentGrassVaseCells; // unbroken vases scattered in grass cores

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
            if (biome == null) return;
            _nextBiome = biome;
            _nextBiomeForced = true; // testing pick — bypasses the draft flow at the next new door
        }

        /// <summary>Pick the next-room biome by index into <see cref="Biomes"/>.</summary>
        public void SelectBiome(int index)
        {
            if (biomes != null && index >= 0 && index < biomes.Length)
                SelectBiome(biomes[index]);
        }

        private void Awake()
        {
            _instance = this;

            // Be forgiving about wiring: the painter usually lives on the same object, and there's
            // one player in the scene. Biomes still have to be assigned explicitly.
            if (painter == null) painter = GetComponent<TilemapPainter>();
            if (decorPool == null) decorPool = FindAnyObjectByType<IslandDecorPool>();
            if (enemyPool == null) enemyPool = FindAnyObjectByType<EnemyPool>();
            if (collectablePool == null) collectablePool = FindAnyObjectByType<CollectablePool>();
            if (player == null)
            {
                var found = FindAnyObjectByType<TRVController>();
                if (found != null) player = found.transform;
            }

            // Roll a session-unique world seed so neighbour rooms aren't identical every run. It's held
            // only in memory (never persisted), and fixed for the rest of this session so a room you
            // walk back into regenerates the same as when you left it.
            if (randomizeWorldSeed)
                worldSeed = System.Guid.NewGuid().GetHashCode();

            // Default biome for the start room + initial selection: Lands (Plain) when present,
            // else the list's first entry.
            _nextBiome = DefaultBiome();
            _currentBiome = _nextBiome;
            if (draftUI == null) draftUI = FindAnyObjectByType<RoomDraftUI>(FindObjectsInactive.Include);
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
                    if (decorPool != null)
                    {
                        decorPool.Prewarm(b.IslandDecorObjects);
                        if (b.VasePrefab != null) decorPool.Prewarm(new[] { b.VasePrefab });
                    }
                    if (enemyPool != null)
                    {
                        enemyPool.Prewarm(b.EnemyPrefabs);
                        enemyPool.Prewarm(b.MinibossPrefabs);
                    }
                }

        }

        private void Update()
        {
            // Room-clear reward: the moment the last enemy of this room dies, HP pots (layer ×
            // potsPerLayerOnClear) drop at the room's middle and fly to the player.
            if (_roomHadEnemies && enemyPool != null && enemyPool.ActiveCount == 0)
            {
                _roomHadEnemies = false;
                SpawnClearReward();
            }

            // M toggles the room map — polled HERE (always active) so it works no matter where
            // the map component lives in the UI hierarchy, active or not.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.mKey.wasPressedThisFrame) return;
            if (_mapUI == null) _mapUI = FindAnyObjectByType<RoomMapUI>(FindObjectsInactive.Include);
            if (_mapUI != null) _mapUI.Toggle();
        }

        /// <summary>Drop the clear reward — one HP pot per layer (× the tunable) scattered around
        /// the room's middle point; the pots ground-wait then fly to the player (Collectable flow).</summary>
        private void SpawnClearReward()
        {
            if (clearRewardPotPrefab == null || potsPerLayerOnClear <= 0 || painter == null) return;
            int count = CurrentLayer * potsPerLayerOnClear;
            var pool = CollectablePool.Instance;
            if (count <= 0 || pool == null || _currentBiome == null) return;

            var centre = painter.CellCenterWorld(_currentBiome.Width / 2, _currentBiome.Height / 2);
            for (int i = 0; i < count; i++)
                pool.Spawn(clearRewardPotPrefab, centre + (Vector3)(Random.insideUnitCircle * clearRewardScatter));
        }

        /// <summary>Called by a <see cref="DoorPortal"/> while the player stands on a door tile.
        /// Opens the room-draft UI (controls frozen) instead of transitioning immediately; the UI
        /// then calls <see cref="ConfirmDraft"/> or <see cref="CancelDraft"/>.</summary>
        public void OnPlayerEnteredDoor(Vector3 playerWorldPos)
        {
            if (_transitioning || _drafting || _draftSuppressed) return;

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
            var exitDir = painter.NearestEdgeWorld(playerWorldPos, _currentBiome.Width, _currentBiome.Height);

            // Already drafted — a room exists on the other side (cached, keeps its biome): nothing
            // to choose, just walk through.
            if (_cache.TryGet(_coord + exitDir.Offset(), out _))
            {
                StartCoroutine(Transition(exitDir));
                return;
            }

            // TESTING override (BiomeHotkeys 0-3): a forced selection bypasses the whole draft
            // flow — no key spent, no UI, straight into the chosen biome.
            if (_nextBiomeForced)
            {
                StartCoroutine(Transition(exitDir));
                return;
            }

            // The target room's layer decides whether the DEFAULT (Lands) biome is allowed there.
            _pendingDraftCoord = _coord + exitDir.Offset();

            // No keys at all — nothing to choose either: straight into the default room… unless
            // the door leads to a no-Lands layer, in which case a keyless player can't pass.
            if (!HasAnyKeys())
            {
                if (!PendingDraftAllowsDefault) return; // locked out — come back with a key
                _nextBiome = DefaultBiome();
                StartCoroutine(Transition(exitDir));
                return;
            }

            if (draftUI == null) // late lookup — the UI may live on an initially inactive object
                draftUI = FindAnyObjectByType<RoomDraftUI>(FindObjectsInactive.Include);
            if (draftUI != null)
            {
                if (!draftUI.Open()) return; // another blocking UI is up — retried while on the door
                _drafting = true;
                _pendingDoorPos = playerWorldPos;
                return;
            }

            // No draft UI in the scene — old behaviour: transition right away with _nextBiome.
            if (!_warnedNoDraftUI)
            {
                _warnedNoDraftUI = true;
                Debug.LogWarning($"[{nameof(RoomManager)}] No RoomDraftUI found — doors transition " +
                                 "immediately. Add a RoomDraftUI to the scene for room drafting.", this);
            }
            StartCoroutine(Transition(exitDir));
        }

        /// <summary>Called by <see cref="DoorPortal"/> when the player steps OFF a door tile —
        /// re-arms the draft UI after a cancel.</summary>
        public void OnPlayerLeftDoor() => _draftSuppressed = false;

        /// <summary>The draft UI was cancelled: nothing happens until the player leaves the door
        /// trigger and re-enters it.</summary>
        public void CancelDraft()
        {
            if (!_drafting) return;
            _drafting = false;
            _draftSuppressed = true;
        }

        /// <summary>The draft UI confirmed a choice: transition through the pending door into
        /// <paramref name="biome"/> (null = the default biome, i.e. the Skip button).</summary>
        public void ConfirmDraft(BiomeConfig biome)
        {
            if (!_drafting) return;
            _drafting = false;
            // Defensive: a null (Skip) confirm into a no-Lands layer is refused (the UI hides the
            // Skip button there, so this shouldn't happen).
            if (biome == null && !PendingDraftAllowsDefault) return;
            _nextBiome = biome != null ? biome : DefaultBiome();
            // Classify by nearest edge, not position-vs-centre — doors can be anywhere on an edge,
            // and a door cell is always nearest its own edge.
            StartCoroutine(Transition(painter.NearestEdgeWorld(_pendingDoorPos, _currentBiome.Width, _currentBiome.Height)));
        }

        /// <summary>The biome of the room at <paramref name="coord"/> if it exists (drafted or
        /// visited) — the CURRENT room included. False = nothing drafted there yet. For the map UI.</summary>
        public bool TryGetRoomBiome(Vector2Int coord, out BiomeConfig biome)
        {
            if (coord == _coord)
            {
                biome = _currentBiome;
                return biome != null;
            }
            if (_cache.TryGet(coord, out var snapshot) && snapshot.Biome != null)
            {
                biome = snapshot.Biome;
                return true;
            }
            biome = null;
            return false;
        }

        /// <summary>The biome a key colour drafts: blue → Aqua, purple → Halls, red → Anubis.
        /// Null when that biome isn't in <see cref="Biomes"/>.</summary>
        public BiomeConfig BiomeForKey(KeyType key) => key switch
        {
            KeyType.Blue => FindBiome(BiomeLayout.Corridors),
            KeyType.Purple => FindBiome(BiomeLayout.Halls),
            _ => FindBiome(BiomeLayout.Open),
        };

        private BiomeConfig FindBiome(BiomeLayout layout)
        {
            if (biomes == null) return null;
            foreach (var b in biomes)
                if (b != null && b.Layout == layout) return b;
            return null;
        }

        /// <summary>True when the player holds at least one key of ANY colour — with none, doors
        /// skip the draft UI and lead straight to the default room.</summary>
        private bool HasAnyKeys()
        {
            if (_inventory == null) _inventory = FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (_inventory == null) return false;
            foreach (KeyType type in System.Enum.GetValues(typeof(KeyType)))
                if (_inventory.Keys(type) > 0) return true;
            return false;
        }

        /// <summary>The default (Skip / start) biome: Lands (Plain) when present, else the list's
        /// first entry.</summary>
        private BiomeConfig DefaultBiome()
        {
            var lands = FindBiome(BiomeLayout.Plain);
            if (lands != null) return lands;
            if (biomes != null)
                foreach (var b in biomes)
                    if (b != null) return b;
            return null;
        }

        private IEnumerator Transition(Cardinal exitDir)
        {
            _transitioning = true;

            if (fader != null) yield return fader.FadeOut();

            // Save the room we're leaving (its biome + island decor placements ride on the snapshot).
            var snapshot = painter.Capture(_currentBiome.Width, _currentBiome.Height);
            snapshot.IslandDecor = _currentIslandDecor;
            snapshot.Biome = _currentBiome;
            snapshot.MainArea = _currentMainArea;
            snapshot.MainFillerIndex = _currentMainFillerIndex;
            snapshot.MainFillerCells = _currentMainFillerCells; // remembers which fillers are still intact
            snapshot.VaseCells = _currentVaseCells;             // smashed Lands vases stay gone
            snapshot.GrassVaseCells = _currentGrassVaseCells;

            // Books still standing (not consumed) persist; a consumed book's spot is dropped.
            var remainingBooks = new List<Vector3>();
            if (_currentBookSpots != null)
                for (int i = 0; i < _bookInstances.Count && i < _currentBookSpots.Count; i++)
                    if (_bookInstances[i] != null && _bookInstances[i].activeSelf)
                        remainingBooks.Add(_currentBookSpots[i]);
            snapshot.BookSpots = remainingBooks;
            snapshot.BarrelCells = _currentBarrelCells; // same list Fill mutates on break

            _cache.Save(_coord, snapshot);

            _coord += exitDir.Offset();
            LoadRoom(_coord, exitDir.Opposite()); // arrive at the opposite door

            if (fader != null) yield return fader.FadeIn();

            _transitioning = false;
        }

        /// <summary>Generate + paint the room for a coord and place the player on the entry landing.
        /// A cached room keeps its original biome; a fresh one uses the selected next biome.</summary>
        private void LoadRoom(Vector2Int coord, Cardinal enterFrom)
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
                _currentMainArea = snapshot.MainArea;            // re-spawn the same main-room content
                _currentMainFillerIndex = snapshot.MainFillerIndex;
                _currentMainFillerCells = snapshot.MainFillerCells; // already-broken fillers stay gone
                _currentVaseCells = snapshot.VaseCells;             // already-smashed vases stay gone
                _currentGrassVaseCells = snapshot.GrassVaseCells;
                _currentBookSpots = snapshot.BookSpots;             // consumed books stay gone
                _currentBarrelCells = snapshot.BarrelCells;         // smashed barrels stay gone
            }
            else
            {
                roomBiome = _nextBiome;
                _nextBiomeForced = false; // a fresh room consumed the testing pick
                var grid = new RoomGenerator(roomBiome).Generate(RoomSeed.Rng(worldSeed, coord));
                painter.Paint(grid, roomBiome);
                _currentIslandDecor = grid.IslandDecor;
                enemyPositions = PickEnemyPositions(grid, roomBiome, enterFrom);
                _currentMainArea = grid.MainIgroomArea;          // roll the big igroom's content (fresh only)
                _currentMainFillerIndex = PickMainFiller(grid.MainIgroomArea, roomBiome);
                _currentMainFillerCells = _currentMainFillerIndex >= 0 ? BuildFillerCells(_currentMainArea) : null;
                _currentVaseCells = grid.VaseSpots; // Lands vase patches (snapshot rides the same list)
                _currentGrassVaseCells = grid.GrassVaseSpots;
                _currentBookSpots = ComputeBookSpots(grid, roomBiome); // biome-specific book placement
                _currentBarrelCells = ComputeBarrelCells(grid, roomBiome); // Anubis barrels (after books — avoids their cells)
            }
            _currentBiome = roomBiome;

            // Rebuild the nav grid from the painted room so enemies can path around water/walls.
            _nav.Rebuild(painter, roomBiome.Width, roomBiome.Height);
            RoomNav.Current = _nav;

            // Swap the pooled objects over to this room (each releases the previous room's first).
            if (decorPool != null) decorPool.Show(_currentIslandDecor, painter, roomBiome);
            SpawnMainContent(roomBiome); // big-igroom filler — AFTER Show (filler adds to the pool)
            SpawnVases(roomBiome);       // Lands vase patches — AFTER Show (also adds to the pool)
            SpawnBooks(roomBiome);       // the biome's upgrade books (fresh spots or the snapshot's)
            SpawnBarrels(roomBiome);     // Anubis barrels — AFTER Show (also adds to the pool)
            if (enemyPool != null) enemyPool.Populate(enemyPositions, roomBiome, EnemyHealthMultiplier, EnemyDamageMultiplier);
            _roomHadEnemies = enemyPositions != null && enemyPositions.Count > 0; // arms the clear reward (fresh rooms only)
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

                // Fresh room, fresh ammo: the per-room fire bullets refill on every entry.
                if (player.TryGetComponent<TRVController>(out var trv)) trv.ResetRoomBullets();
            }

        }

        /// <summary>Roll the big main igroom's FILLER for a FRESH Halls room: a random
        /// <see cref="BiomeConfig.MainRoomFillers"/> entry (crates/vases scattered over the interior),
        /// or -1 for nothing (no main area / none configured). Upgrades come from the BOOK system —
        /// the attack book always spawns at the igroom's centre, which the filler cells avoid.</summary>
        private int PickMainFiller(RectInt area, BiomeConfig biome)
        {
            if (area.width <= 0 || biome == null) return -1;

            var fillerIndices = new List<int>();
            var fillers = biome.MainRoomFillers;
            if (fillers != null)
                for (int i = 0; i < fillers.Length; i++)
                    if (fillers[i] != null) fillerIndices.Add(i);

            return fillerIndices.Count > 0 ? fillerIndices[Random.Range(0, fillerIndices.Count)] : -1;
        }

        /// <summary>Spawn the current room's big-igroom filler decor (crates/vases). Uses the cached
        /// area/index (fresh on generation, restored from the snapshot on revisit). No-op for
        /// non-Halls rooms (no main area).</summary>
        private void SpawnMainContent(BiomeConfig biome)
        {
            if (_currentMainArea.width <= 0 || biome == null || painter == null) return;
            if (_currentMainFillerIndex < 0) return;

            var fillers = biome.MainRoomFillers;
            if (fillers == null || _currentMainFillerIndex >= fillers.Length) return;
            var prefab = fillers[_currentMainFillerIndex];
            if (prefab == null || decorPool == null || _currentMainFillerCells == null) return;
            // Fill mutates the cell list (drops a cell when its crate breaks); the snapshot rides
            // the same list, so a smashed crate stays gone when the player returns to the room.
            decorPool.Fill(prefab, _currentMainFillerCells, painter);
        }

        /// <summary>Spawn the breakable VASE at the centre point of each Lands vase patch (the
        /// recorded cells are the patches' bottom-left corners, so the centre = cell centre + half
        /// a cell diagonally). Fill mutates the cell list on break; the snapshot rides the same
        /// list, so a smashed vase stays gone on revisit.</summary>
        private void SpawnVases(BiomeConfig biome)
        {
            if (biome == null || biome.VasePrefab == null || decorPool == null || painter == null) return;

            if (_currentVaseCells != null && _currentVaseCells.Count > 0)
            {
                var halfDiagonal = (painter.CellCenterWorld(1, 1) - painter.CellCenterWorld(0, 0)) * 0.5f;
                var oneCellRight = painter.CellCenterWorld(1, 0) - painter.CellCenterWorld(0, 0);
                var offset = halfDiagonal - oneCellRight * 0.05f; // patch centre, nudged a hair LEFT
                decorPool.Fill(biome.VasePrefab, _currentVaseCells, painter, offset);
            }
            if (_currentGrassVaseCells != null && _currentGrassVaseCells.Count > 0)
                decorPool.Fill(biome.VasePrefab, _currentGrassVaseCells, painter); // grass vases sit on cell centres
        }

        /// <summary>A scattered HALF of the main igroom's interior cells to hold fillers (skips collision
        /// tiles and the 3×3 around the centre — the biome's BOOK spawns there). Shuffled and halved so
        /// crates/vases are sparse and randomly placed, not a packed wall.</summary>
        private List<Vector2Int> BuildFillerCells(RectInt area)
        {
            int bcx = area.xMin + area.width / 2, bcy = area.yMin + area.height / 2;
            var cells = new List<Vector2Int>(area.width * area.height);
            for (int x = area.xMin; x < area.xMax; x++)
                for (int y = area.yMin; y < area.yMax; y++)
                {
                    if (Mathf.Abs(x - bcx) <= 1 && Mathf.Abs(y - bcy) <= 1) continue; // book spot
                    if (painter == null || !painter.HasSolidAt(x, y))
                        cells.Add(new Vector2Int(x, y));
                }

            // Fisher–Yates shuffle, then keep half — random subset = scattered locations.
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            int keep = cells.Count / 4; // sparse: a quarter of the interior gets a filler
            if (cells.Count > keep) cells.RemoveRange(keep, cells.Count - keep);
            return cells;
        }

        /// <summary>Where the biome's upgrade BOOKS go in a FRESH room (world positions). Per layout:
        /// Aqua = the middle of every 4×4 island (decor keeps the centre 2×2 clear); Halls = the big
        /// igroom's centre (+ rarely one small igroom's centre, both kept clear of decor/fillers);
        /// Anubis = the end of ONE south corridor that did NOT get the tall decor patch B (max 1);
        /// Lands = none.</summary>
        private List<Vector3> ComputeBookSpots(RoomGrid grid, BiomeConfig biome)
        {
            var spots = new List<Vector3>();
            _openBookCells.Clear(); // re-filled by the Open branch; barrels read it afterwards
            if (grid == null || biome == null || biome.BookPrefab == null || painter == null) return spots;

            switch (biome.Layout)
            {
                case BiomeLayout.Corridors: // Aqua: one per island, on its exact middle point
                    foreach (var (ax, ay) in grid.IslandAnchors)
                        spots.Add((painter.CellCenterWorld(ax + 1, ay + 1) +
                                   painter.CellCenterWorld(ax + 2, ay + 2)) * 0.5f);
                    break;

                case BiomeLayout.Halls: // always the big igroom's middle; rarely one small igroom too
                {
                    var main = grid.MainIgroomArea;
                    if (main.width > 0)
                        spots.Add(painter.CellCenterWorld(main.xMin + main.width / 2, main.yMin + main.height / 2));

                    // The rare extra book: only igrooms with NO painted collision inside — a
                    // waterfall ends on a solid 3×3 base patch inside its igroom, and a book
                    // there would overlap it.
                    var candidates = new List<RectInt>();
                    var smalls = grid.IgroomDecorAreas;
                    if (smalls != null)
                        foreach (var area in smalls)
                        {
                            bool clear = true;
                            for (int x = area.xMin; x < area.xMax && clear; x++)
                                for (int y = area.yMin; y < area.yMax && clear; y++)
                                    if (painter.HasSolidAt(x, y)) clear = false;
                            if (clear) candidates.Add(area);
                        }
                    if (candidates.Count > 0 && Random.value < biome.ExtraBookChance)
                    {
                        var area = candidates[Random.Range(0, candidates.Count)];
                        spots.Add(painter.CellCenterWorld(area.xMin + area.width / 2, area.yMin + area.height / 2));
                    }
                    break;
                }

                case BiomeLayout.Open: // Anubis: corridor ends without the tall (patch B) decor —
                {                      // one guaranteed; a SECOND on the extra-book chance if
                                       // another free end exists. The book sits 1 tile ABOVE the
                                       // end row (where decor B would stand — hence B-corridors
                                       // are excluded).
                    var candidates = new List<Vector2Int>();
                    foreach (var (cx, endY) in grid.HighGroundEnds)
                        if (painter.HighGroundDecorBEnd.x != cx || painter.HighGroundDecorBEnd.y != endY)
                            candidates.Add(new Vector2Int(cx, endY));
                    if (candidates.Count > 0)
                    {
                        int first = Random.Range(0, candidates.Count);
                        var firstCell = new Vector2Int(candidates[first].x, candidates[first].y + 1);
                        _openBookCells.Add(firstCell);
                        spots.Add(painter.CellCenterWorld(firstCell.x, firstCell.y));

                        if (candidates.Count > 1 && Random.value < biome.ExtraBookChance)
                        {
                            candidates.RemoveAt(first);
                            var extra = candidates[Random.Range(0, candidates.Count)];
                            var extraCell = new Vector2Int(extra.x, extra.y + 1);
                            _openBookCells.Add(extraCell);
                            spots.Add(painter.CellCenterWorld(extraCell.x, extraCell.y));
                        }
                    }
                    break;
                }
            }
            return spots;
        }

        /// <summary>Anubis BARREL cells for a FRESH room: sometimes ONE stands behind a corridor
        /// end that has neither decor B nor a book; the rest scatter on random REGULAR floor —
        /// never at the floor's edge (all 4 neighbours must be floor, and any cell with painted
        /// collision — shoreline rims, stairs rails, decor bases — is skipped), never on a stairs
        /// footprint, clear of doors, ≥2 apart.</summary>
        private List<Vector2Int> ComputeBarrelCells(RoomGrid grid, BiomeConfig biome)
        {
            var cells = new List<Vector2Int>();
            if (grid == null || biome == null || biome.BarrelPrefab == null || painter == null) return cells;
            if (biome.Layout != BiomeLayout.Open) return cells;

            int count = Random.Range(biome.MinBarrels, Mathf.Max(biome.MinBarrels, biome.MaxBarrels) + 1);
            if (count <= 0) return cells;

            // Sometimes one barrel takes a free corridor end (no decor B, no book there).
            var freeEnds = new List<Vector2Int>();
            foreach (var (cx, endY) in grid.HighGroundEnds)
            {
                if (painter.HighGroundDecorBEnd.x == cx && painter.HighGroundDecorBEnd.y == endY) continue;
                var spot = new Vector2Int(cx, endY + 1);
                if (_openBookCells.Contains(spot)) continue;
                freeEnds.Add(spot);
            }
            if (freeEnds.Count > 0 && Random.value < biome.BarrelAtEndChance)
            {
                cells.Add(freeEnds[Random.Range(0, freeEnds.Count)]);
                count--;
            }

            // The rest scatter over regular floor.
            var candidates = new List<Vector2Int>();
            for (int x = 1; x < grid.Width - 1; x++)
                for (int y = 1; y < grid.Height - 1; y++)
                {
                    if (grid[x, y] != CellType.Floor || grid.IsHighGround(x, y)) continue;
                    if (painter.HasSolidAt(x, y)) continue; // rims/stairs rails/decor bases/walls
                    if (grid[x - 1, y] != CellType.Floor || grid[x + 1, y] != CellType.Floor ||
                        grid[x, y - 1] != CellType.Floor || grid[x, y + 1] != CellType.Floor)
                        continue; // exactly at the floor's edge
                    if (NearDoor(grid, x, y) || InStairsFootprint(grid, x, y)) continue;
                    candidates.Add(new Vector2Int(x, y));
                }

            for (int i = candidates.Count - 1; i > 0; i--) // shuffle
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            foreach (var c in candidates)
            {
                if (count == 0) break;
                bool tooClose = false;
                foreach (var b in cells)
                    if (Mathf.Abs(b.x - c.x) <= 1 && Mathf.Abs(b.y - c.y) <= 1) { tooClose = true; break; }
                if (tooClose) continue;
                cells.Add(c);
                count--;
            }
            return cells;
        }

        private static bool NearDoor(RoomGrid grid, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (grid.Get(x + dx, y + dy) == CellType.Door) return true;
            return false;
        }

        private static bool InStairsFootprint(RoomGrid grid, int x, int y)
        {
            foreach (var (sx, sy) in grid.StairPatches)
                if (x >= sx && x <= sx + 2 && y >= sy && y <= sy + 2) return true;
            return false;
        }

        /// <summary>Spawn the room's barrels (breakable, pooled via <see cref="IslandDecorPool.Fill"/> —
        /// the cell list is mutated on break and rides the snapshot, so smashed barrels stay gone).</summary>
        private void SpawnBarrels(BiomeConfig biome)
        {
            if (biome == null || biome.BarrelPrefab == null || decorPool == null || painter == null) return;
            if (_currentBarrelCells == null || _currentBarrelCells.Count == 0) return;
            decorPool.Fill(biome.BarrelPrefab, _currentBarrelCells, painter);
        }

        /// <summary>Spawn the current room's book instances at <see cref="_currentBookSpots"/>
        /// (destroying the previous room's). A consumed book deactivates itself; on leave its spot
        /// is pruned from the snapshot, so it stays gone on revisit.</summary>
        private void SpawnBooks(BiomeConfig biome)
        {
            foreach (var book in _bookInstances)
                if (book != null) Destroy(book);
            _bookInstances.Clear();

            if (_currentBookSpots == null || biome == null || biome.BookPrefab == null) return;
            foreach (var spot in _currentBookSpots)
                _bookInstances.Add(Instantiate(biome.BookPrefab, spot, Quaternion.identity));
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
                    if (painter != null && painter.HasSolidAt(x, y)) continue; // skip painted collision (e.g. the waterfall base patch)
                    var c = new Vector2Int(x, y);
                    floor.Add(c);
                    int dx = x - entry.x, dy = y - entry.y;
                    if (dx * dx + dy * dy >= safeSq) eligible.Add(c);
                }

            var pickFrom = eligible.Count > 0 ? eligible : floor; // tiny room → fall back to any floor
            if (pickFrom.Count == 0) return positions;

            // Slot 0 spawns in the LARGEST open walking space (where a miniboss, if any, goes); the rest
            // are random. (A normal enemy harmlessly takes slot 0 when no miniboss spawns.)
            var prime = LargestSpaceCell(grid, pickFrom);
            pickFrom.Remove(prime);
            for (int i = pickFrom.Count - 1; i > 0; i--) // Fisher–Yates on the remainder
            {
                int j = Random.Range(0, i + 1);
                (pickFrom[i], pickFrom[j]) = (pickFrom[j], pickFrom[i]);
            }

            int n = Mathf.Min(biome.EnemiesPerRoom + ExtraEnemies, pickFrom.Count + 1); // layers add enemies
            positions.Add(painter.CellCenterWorld(prime.x, prime.y));
            for (int i = 0; i < n - 1; i++)
                positions.Add(painter.CellCenterWorld(pickFrom[i].x, pickFrom[i].y));
            return positions;
        }

        /// <summary>Among <paramref name="candidates"/>, the cell with the MOST open space around it — the
        /// max Chebyshev distance to the nearest non-walkable cell (a multi-source BFS distance transform
        /// from every wall/water/building). Used to drop the miniboss where it has the most room.</summary>
        private static Vector2Int LargestSpaceCell(RoomGrid grid, List<Vector2Int> candidates)
        {
            int w = grid.Width, h = grid.Height;
            var clearance = new int[w, h];
            var queue = new Queue<Vector2Int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (grid[x, y].IsWalkable())
                    {
                        clearance[x, y] = int.MaxValue;
                    }
                    else
                    {
                        clearance[x, y] = 0; // obstacle = BFS source
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                }

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int next = clearance[c.x, c.y] + 1;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = c.x + dx, ny = c.y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || clearance[nx, ny] <= next) continue;
                        clearance[nx, ny] = next;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
            }

            Vector2Int best = candidates[0];
            int bestClear = -1;
            foreach (var c in candidates)
                if (clearance[c.x, c.y] > bestClear) { bestClear = clearance[c.x, c.y]; best = c; }
            return best;
        }
    }
}
