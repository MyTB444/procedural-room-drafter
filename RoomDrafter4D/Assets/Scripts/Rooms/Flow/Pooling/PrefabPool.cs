using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Generic pool of prefab instances, keyed BY PREFAB so one pool serves multiple biomes — the
    /// prefab set isn't fixed at startup; callers rent whatever prefab the current biome asks for.
    /// Instances are reused and grow on demand; <see cref="Prewarm"/> pre-instantiates a batch (call
    /// it per biome when known). A pooled object returns itself with <see cref="Release"/>. Backs
    /// <see cref="IslandDecorPool"/> and <see cref="EnemyPool"/>.
    /// </summary>
    public abstract class PrefabPool : MonoBehaviour
    {
        [Tooltip("Parent for pooled instances. Defaults to this object.")]
        [SerializeField] protected Transform container;

        [Tooltip("How many of each prefab Prewarm pre-instantiates. The pool still grows on demand.")]
        [SerializeField, Min(0)] protected int prewarmPerPrefab = 4;

        private readonly Dictionary<GameObject, Stack<GameObject>> _free =
            new Dictionary<GameObject, Stack<GameObject>>();
        private readonly List<(GameObject prefab, GameObject go)> _active =
            new List<(GameObject, GameObject)>();

        /// <summary>How many instances are currently shown (rented and not yet released).</summary>
        public int ActiveCount => _active.Count;

        protected virtual void Awake()
        {
            if (container == null) container = transform;
        }

        /// <summary>Pre-instantiate (disabled) a batch of each prefab — e.g. when a biome is selected.</summary>
        public void Prewarm(IReadOnlyList<GameObject> prefabs)
        {
            if (prefabs == null) return;
            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                var free = StackFor(prefab);
                while (free.Count < prewarmPerPrefab) free.Push(CreateDisabled(prefab));
            }
        }

        /// <summary>Rent a disabled instance of the prefab (grows on demand) and track it as active.
        /// The caller positions/configures it and calls SetActive(true). Null if prefab is null.</summary>
        protected GameObject Rent(GameObject prefab)
        {
            if (prefab == null) return null;
            var free = StackFor(prefab);
            var go = free.Count > 0 ? free.Pop() : CreateDisabled(prefab);
            _active.Add((prefab, go));
            return go;
        }

        /// <summary>Return one instance to the pool (disabled).</summary>
        public void Release(GameObject instance)
        {
            if (instance == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].go != instance) continue;
                var prefab = _active[i].prefab;
                _active.RemoveAt(i);
                instance.SetActive(false);
                StackFor(prefab).Push(instance);
                return;
            }
        }

        /// <summary>Return every active instance to the pool (e.g. when leaving a room).</summary>
        public void ReleaseAll()
        {
            foreach (var (prefab, go) in _active)
            {
                go.SetActive(false);
                StackFor(prefab).Push(go);
            }
            _active.Clear();
        }

        private Stack<GameObject> StackFor(GameObject prefab)
        {
            if (!_free.TryGetValue(prefab, out var free))
            {
                free = new Stack<GameObject>();
                _free[prefab] = free;
            }
            return free;
        }

        private GameObject CreateDisabled(GameObject prefab)
        {
            var go = Instantiate(prefab, container);
            OnCreated(go);
            go.SetActive(false);
            return go;
        }

        /// <summary>Hook to configure a freshly instantiated instance once (e.g. add a component).</summary>
        protected virtual void OnCreated(GameObject instance) { }
    }
}
