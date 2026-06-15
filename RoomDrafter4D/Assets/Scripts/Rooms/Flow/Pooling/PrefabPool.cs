using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Generic pool of prefab instances: prewarms a batch of each prefab (disabled) at start and
    /// reuses them, growing on demand. Subclasses supply the prefab set (<see cref="Prefabs"/>) and
    /// a public spawn API built on <see cref="Rent"/>; a pooled object returns itself with
    /// <see cref="Release"/> (the pool tracks which prefab list each instance came from). Backs
    /// <see cref="IslandDecorPool"/> and <see cref="EnemyPool"/>.
    /// </summary>
    public abstract class PrefabPool : MonoBehaviour
    {
        [Tooltip("Parent for pooled instances. Defaults to this object.")]
        [SerializeField] protected Transform container;

        [Tooltip("How many of each prefab to pre-instantiate at start. The pool grows on demand " +
                 "beyond this.")]
        [SerializeField, Min(0)] protected int prewarmPerPrefab = 4;

        private List<GameObject>[] _free;
        private readonly List<(int prefab, GameObject go)> _active = new List<(int, GameObject)>();

        /// <summary>The prefab set this pool draws from (usually a BiomeConfig array).</summary>
        protected abstract GameObject[] Prefabs { get; }

        /// <summary>Number of prefab slots (some may be null/empty).</summary>
        protected int PrefabCount => _free?.Length ?? 0;

        /// <summary>How many instances are currently shown (rented and not yet released).</summary>
        public int ActiveCount => _active.Count;

        protected virtual void Awake()
        {
            if (container == null) container = transform;

            var prefabs = Prefabs;
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

        /// <summary>True when prefab slot <paramref name="index"/> is assigned.</summary>
        protected bool HasPrefab(int index) => index >= 0 && index < PrefabCount && Prefabs[index] != null;

        /// <summary>
        /// Rent a disabled instance of the given prefab (grows the pool if dry) and track it as
        /// active. The caller positions/configures it and calls SetActive(true). Null if the slot
        /// is empty.
        /// </summary>
        protected GameObject Rent(int prefabIndex)
        {
            if (!HasPrefab(prefabIndex)) return null;

            var free = _free[prefabIndex];
            GameObject go;
            if (free.Count > 0) { go = free[free.Count - 1]; free.RemoveAt(free.Count - 1); }
            else go = Create(prefabIndex);

            if (go != null) _active.Add((prefabIndex, go));
            return go;
        }

        /// <summary>Return one instance to the pool (disabled). The pool knows which list it's from.</summary>
        public void Release(GameObject instance)
        {
            if (instance == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].go != instance) continue;
                int prefab = _active[i].prefab;
                _active.RemoveAt(i);
                instance.SetActive(false);
                _free[prefab].Add(instance);
                return;
            }
        }

        /// <summary>Return every active instance to the pool (e.g. when leaving a room).</summary>
        public void ReleaseAll()
        {
            foreach (var (prefab, go) in _active)
            {
                go.SetActive(false);
                _free[prefab].Add(go);
            }
            _active.Clear();
        }

        private GameObject Create(int prefabIndex)
        {
            var go = Instantiate(Prefabs[prefabIndex], container);
            OnCreated(go);
            go.SetActive(false);
            return go;
        }

        /// <summary>Hook to configure a freshly instantiated instance once (e.g. add a component).</summary>
        protected virtual void OnCreated(GameObject instance) { }
    }
}
