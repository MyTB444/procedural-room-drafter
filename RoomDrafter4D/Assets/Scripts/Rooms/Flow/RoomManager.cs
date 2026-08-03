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

        private static RoomManager _instance;

        /// <summary>The scene's room manager (fake-null re-finds it after a reload).</summary>
        public static RoomManager Instance =>
            _instance != null ? _instance : (_instance = FindFirstObjectByType<RoomManager>());

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
        private BiomeConfig _currentBiome;  // biome of the room currently shown

        // Island decor placements for the room currently shown — saved into its snapshot on leave,
        // so a cached room re-spawns the same decor without re-running generation.
        private List<PatchPlacement> _currentIslandDecor;

        // Rolled main-room content for the room currently shown (Halls big igroom): the interior rect
        // and which option won (one of upgrade/filler index is >= 0, or both -1 = nothing). Saved into
        // the snapshot on leave so a revisited room re-spawns the same content.
        private RectInt _currentMainArea;
        private int _currentMainUpgradeIndex = -1;
        private int _currentMainFillerIndex = -1;
        private List<Vector2Int> _currentMainFillerCells; // unbroken filler cells (breaks persist via this list)
        private List<Vector2Int> _currentVaseCells; // unbroken Lands vase patches (breaks persist via this list)
        private List<Vector2Int> _currentGrassVaseCells; // unbroken vases scattered in grass cores
        private GameObject _mainUpgradeInstance; // the spawned upgrade pickup (destroyed on room change)
        private PlayerUpgrades _playerUpgrades;

        // The player's runtime upgrade ledger (lazy — auto-added to the player by TRVController).
        private PlayerUpgrades PlayerUpgrades =>
            _playerUpgrades != null ? _playerUpgrades :
            (_playerUpgrades = player != null ? player.GetComponent<PlayerUpgrades>() : null);

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

            // Default biome for the start room + initial selection: Lands (Plain) when present,
            // else the list's first entry.
            _nextBiome = DefaultBiome();
            _currentBiome = _nextBiome;
            if (draftUI == null) draftUI = FindFirstObjectByType<RoomDraftUI>(FindObjectsInactive.Include);
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
            // M toggles the room map — polled HERE (always active) so it works no matter where
            // the map component lives in the UI hierarchy, active or not.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.mKey.wasPressedThisFrame) return;
            if (_mapUI == null) _mapUI = FindFirstObjectByType<RoomMapUI>(FindObjectsInactive.Include);
            if (_mapUI != null) _mapUI.Toggle();
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

            // No keys at all — nothing to choose either: straight into the default room.
            if (!HasAnyKeys())
            {
                _nextBiome = DefaultBiome();
                StartCoroutine(Transition(exitDir));
                return;
            }

            if (draftUI == null) // late lookup — the UI may live on an initially inactive object
                draftUI = FindFirstObjectByType<RoomDraftUI>(FindObjectsInactive.Include);
            if (draftUI != null)
            {
                _drafting = true;
                _pendingDoorPos = playerWorldPos;
                draftUI.Open();
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
            if (_inventory == null) _inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
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
            snapshot.MainUpgradeIndex = _currentMainUpgradeIndex;
            snapshot.MainFillerIndex = _currentMainFillerIndex;
            snapshot.MainFillerCells = _currentMainFillerCells; // remembers which fillers are still intact
            snapshot.VaseCells = _currentVaseCells;             // smashed Lands vases stay gone
            snapshot.GrassVaseCells = _currentGrassVaseCells;
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
                _currentMainUpgradeIndex = snapshot.MainUpgradeIndex;
                _currentMainFillerIndex = snapshot.MainFillerIndex;
                _currentMainFillerCells = snapshot.MainFillerCells; // already-broken fillers stay gone
                _currentVaseCells = snapshot.VaseCells;             // already-smashed vases stay gone
                _currentGrassVaseCells = snapshot.GrassVaseCells;
            }
            else
            {
                roomBiome = _nextBiome;
                var grid = new RoomGenerator(roomBiome).Generate(RoomSeed.Rng(worldSeed, coord));
                painter.Paint(grid, roomBiome);
                _currentIslandDecor = grid.IslandDecor;
                enemyPositions = PickEnemyPositions(grid, roomBiome, enterFrom);
                _currentMainArea = grid.MainIgroomArea;          // roll the big igroom's content (fresh only)
                DecideMainContent(grid.MainIgroomArea, roomBiome, out _currentMainUpgradeIndex, out _currentMainFillerIndex);
                _currentMainFillerCells = _currentMainFillerIndex >= 0 ? BuildFillerCells(_currentMainArea) : null;
                _currentVaseCells = grid.VaseSpots; // Lands vase patches (snapshot rides the same list)
                _currentGrassVaseCells = grid.GrassVaseSpots;
                // An upgrade room also gets the small-igroom scatter decor (kept off the upgrade's centre).
                if (_currentMainUpgradeIndex >= 0) _currentIslandDecor.AddRange(grid.MainIgroomDecor);
            }
            _currentBiome = roomBiome;

            // Rebuild the nav grid from the painted room so enemies can path around water/walls.
            _nav.Rebuild(painter, roomBiome.Width, roomBiome.Height);
            RoomNav.Current = _nav;

            // Swap the pooled objects over to this room (each releases the previous room's first).
            if (decorPool != null) decorPool.Show(_currentIslandDecor, painter, roomBiome);
            SpawnMainContent(roomBiome); // big-igroom upgrade/filler — AFTER Show (filler adds to the pool)
            SpawnVases(roomBiome);       // Lands vase patches — AFTER Show (also adds to the pool)
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

        }

        /// <summary>Roll the big main igroom's content for a FRESH Halls room. Upgrades take PRIORITY:
        /// while ANY one-per-run upgrade (<see cref="BiomeConfig.MainRoomUpgrades"/>) is still uncollected,
        /// the room is ALWAYS an upgrade room (random among the uncollected ones). Only once EVERY upgrade
        /// has been collected does it fall back to a random filler (<see cref="BiomeConfig.MainRoomFillers"/>
        /// — crates/vases). Outputs the chosen index into ONE array (the other stays -1); both -1 = nothing
        /// (no main area / nothing configured).</summary>
        private void DecideMainContent(RectInt area, BiomeConfig biome, out int upgradeIndex, out int fillerIndex)
        {
            upgradeIndex = -1;
            fillerIndex = -1;
            if (area.width <= 0 || biome == null) return;

            // Still-uncollected upgrades — one entry each. If any exist, the room is guaranteed an upgrade.
            var upgradeIndices = new List<int>();
            var upgrades = biome.MainRoomUpgrades;
            if (upgrades != null)
                for (int i = 0; i < upgrades.Length; i++)
                {
                    if (upgrades[i] == null) continue;
                    if (upgrades[i].TryGetComponent<Upgrade>(out var up) &&
                        PlayerUpgrades != null && PlayerUpgrades.HasCollected(up.Id))
                        continue; // collected this run — can't appear again
                    upgradeIndices.Add(i);
                }

            if (upgradeIndices.Count > 0)
            {
                upgradeIndex = upgradeIndices[Random.Range(0, upgradeIndices.Count)];
                return;
            }

            // Every upgrade collected → a random filler instead.
            var fillerIndices = new List<int>();
            var fillers = biome.MainRoomFillers;
            if (fillers != null)
                for (int i = 0; i < fillers.Length; i++)
                    if (fillers[i] != null) fillerIndices.Add(i);

            if (fillerIndices.Count > 0)
                fillerIndex = fillerIndices[Random.Range(0, fillerIndices.Count)];
        }

        /// <summary>Spawn the current room's rolled big-igroom content: a single upgrade pickup at the
        /// room centre, OR fill the interior with the chosen filler decor (crates/vases). Destroys the
        /// previous room's upgrade instance first; an upgrade collected since the room was generated is
        /// not re-spawned. Uses the cached area/indices (fresh on generation, restored from the snapshot
        /// on revisit). No-op for non-Halls rooms (no main area).</summary>
        private void SpawnMainContent(BiomeConfig biome)
        {
            if (_mainUpgradeInstance != null) { Destroy(_mainUpgradeInstance); _mainUpgradeInstance = null; }
            if (_currentMainArea.width <= 0 || biome == null || painter == null) return;

            if (_currentMainUpgradeIndex >= 0)
            {
                var upgrades = biome.MainRoomUpgrades;
                if (upgrades == null || _currentMainUpgradeIndex >= upgrades.Length) return;
                var prefab = upgrades[_currentMainUpgradeIndex];
                if (prefab == null) return;
                // Don't re-spawn an upgrade collected since this room was first generated.
                if (prefab.TryGetComponent<Upgrade>(out var up) &&
                    PlayerUpgrades != null && PlayerUpgrades.HasCollected(up.Id))
                    return;
                int cx = _currentMainArea.xMin + _currentMainArea.width / 2;
                int cy = _currentMainArea.yMin + _currentMainArea.height / 2;
                _mainUpgradeInstance = Instantiate(prefab);
                _mainUpgradeInstance.transform.position = painter.CellCenterWorld(cx, cy);
            }
            else if (_currentMainFillerIndex >= 0)
            {
                var fillers = biome.MainRoomFillers;
                if (fillers == null || _currentMainFillerIndex >= fillers.Length) return;
                var prefab = fillers[_currentMainFillerIndex];
                if (prefab == null || decorPool == null || _currentMainFillerCells == null) return;
                // Fill mutates the cell list (drops a cell when its crate breaks); the snapshot rides
                // the same list, so a smashed crate stays gone when the player returns to the room.
                decorPool.Fill(prefab, _currentMainFillerCells, painter);
            }
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
        /// tiles). Shuffled and halved so crates/vases are sparse and randomly placed, not a packed wall.</summary>
        private List<Vector2Int> BuildFillerCells(RectInt area)
        {
            var cells = new List<Vector2Int>(area.width * area.height);
            for (int x = area.xMin; x < area.xMax; x++)
                for (int y = area.yMin; y < area.yMax; y++)
                    if (painter == null || !painter.HasSolidAt(x, y))
                        cells.Add(new Vector2Int(x, y));

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

            int n = Mathf.Min(biome.EnemiesPerRoom, pickFrom.Count + 1);
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
