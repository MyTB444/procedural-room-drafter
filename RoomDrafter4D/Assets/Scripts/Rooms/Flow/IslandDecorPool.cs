using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Object pool for island decor. At game start it pre-instantiates a batch of each
    /// <see cref="BiomeConfig.IslandDecorObjects"/> prefab (disabled). When a room is shown,
    /// <see cref="Show"/> releases the previous room's objects and enables/repositions pooled ones
    /// to match that room's <see cref="RoomGrid.IslandDecor"/> placements — so decor objects are
    /// reused across rooms instead of being created and destroyed on every generation.
    /// </summary>
    public class IslandDecorPool : MonoBehaviour
    {
        [Tooltip("Biome whose IslandDecorObjects feed the pool (the same asset RoomManager uses).")]
        [SerializeField] private BiomeConfig biome;

        [Tooltip("Parent for pooled instances. Defaults to this object; make it a child of MainGrid " +
                 "if you want the decor to share the grid's tilt.")]
        [SerializeField] private Transform container;

        [Tooltip("How many of each prefab to pre-instantiate at start. The pool still grows on " +
                 "demand if a room needs more than this.")]
        [SerializeField, Min(0)] private int prewarmPerPrefab = 8;

        // One free-list per prefab index; active tracks what's currently shown (with its prefab).
        private List<GameObject>[] _free;
        private readonly List<(int prefab, GameObject go)> _active = new List<(int, GameObject)>();

        private void Awake()
        {
            if (container == null) container = transform;

            var prefabs = biome != null ? biome.IslandDecorObjects : null;
            int n = prefabs != null ? prefabs.Length : 0;
            _free = new List<GameObject>[n];
            for (int i = 0; i < n; i++)
            {
                _free[i] = new List<GameObject>(prewarmPerPrefab);
                if (prefabs[i] == null) continue; // empty slot — nothing to pool
                for (int k = 0; k < prewarmPerPrefab; k++)
                    _free[i].Add(Create(i));
            }
        }

        /// <summary>
        /// Show decor for a room: release the previous room's objects, then place a pooled instance
        /// at each placement's cell (world position via <paramref name="painter"/>).
        /// </summary>
        public void Show(IReadOnlyList<PatchPlacement> placements, TilemapPainter painter)
        {
            ReleaseAll();
            if (placements == null || _free == null || painter == null) return;

            foreach (var p in placements)
            {
                if (p.PatchIndex < 0 || p.PatchIndex >= _free.Length) continue;
                var go = Rent(p.PatchIndex);
                if (go == null) continue; // empty prefab slot
                go.transform.position = painter.CellCenterWorld(p.X, p.Y);
                go.GetComponent<IslandDecorPiece>().Activate(this, p.PatchIndex);
                go.SetActive(true);
                _active.Add((p.PatchIndex, go));
            }
        }

        /// <summary>
        /// Return a single shown piece to the pool (disabled) — called by <see cref="IslandDecorPiece"/>
        /// when the player hits it. No-op if it isn't currently active.
        /// </summary>
        public void Release(int prefabIndex, GameObject instance)
        {
            if (instance == null) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].go != instance) continue;
                _active.RemoveAt(i);
                instance.SetActive(false);
                if (prefabIndex >= 0 && prefabIndex < _free.Length)
                    _free[prefabIndex].Add(instance);
                return;
            }
        }

        /// <summary>Disable all shown decor and return it to the pool (e.g. when leaving a room).</summary>
        public void ReleaseAll()
        {
            foreach (var (prefab, go) in _active)
            {
                go.SetActive(false);
                _free[prefab].Add(go);
            }
            _active.Clear();
        }

        private GameObject Rent(int prefabIndex)
        {
            var free = _free[prefabIndex];
            if (free.Count > 0)
            {
                var go = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
                return go;
            }
            return Create(prefabIndex); // pool ran dry — grow on demand
        }

        private GameObject Create(int prefabIndex)
        {
            var prefab = biome.IslandDecorObjects[prefabIndex];
            if (prefab == null) return null;
            var go = Instantiate(prefab, container);
            if (!go.TryGetComponent<IslandDecorPiece>(out _))
                go.AddComponent<IslandDecorPiece>(); // makes it hittable → returns to this pool on hit
            go.SetActive(false);
            return go;
        }
    }
}
