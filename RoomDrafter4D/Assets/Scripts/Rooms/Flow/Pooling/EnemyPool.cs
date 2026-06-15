using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Pools enemies. Reads its spawnable set from <see cref="BiomeConfig.EnemyPrefabs"/>; when a
    /// room is populated, <see cref="Populate"/> releases the previous room's enemies and spawns a
    /// fresh one (a random prefab from the set) at each given world position. Enemies return
    /// themselves via <see cref="PrefabPool.Release"/> when killed. Pooling mechanics live in
    /// <see cref="PrefabPool"/>.
    /// </summary>
    public class EnemyPool : PrefabPool
    {
        [Tooltip("Biome whose EnemyPrefabs feed the pool (the same asset RoomManager uses).")]
        [SerializeField] private BiomeConfig biome;

        private readonly List<int> _validPrefabs = new List<int>(); // indices of assigned slots

        protected override GameObject[] Prefabs => biome != null ? biome.EnemyPrefabs : null;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < PrefabCount; i++)
                if (HasPrefab(i)) _validPrefabs.Add(i);
        }

        /// <summary>
        /// Release the previous room's enemies, then spawn a random enemy at each world position.
        /// Pass null/empty to just clear (e.g. a cached room that shouldn't repopulate).
        /// </summary>
        public void Populate(IReadOnlyList<Vector3> positions)
        {
            ReleaseAll();
            if (positions == null || _validPrefabs.Count == 0) return;

            foreach (var pos in positions)
            {
                int prefab = _validPrefabs[Random.Range(0, _validPrefabs.Count)];
                var go = Rent(prefab);
                if (go == null) continue;
                go.transform.position = pos;
                if (go.TryGetComponent<EnemyController>(out var enemy))
                    enemy.AssignPool(this);
                go.SetActive(true);
            }
        }
    }
}
