using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Pools enemies. <see cref="Populate"/> releases the previous room's enemies and spawns a fresh
    /// random one (from the ROOM biome's <see cref="BiomeConfig.EnemyPrefabs"/>) at each world
    /// position — so one pool serves any biome. Enemies return themselves via
    /// <see cref="PrefabPool.Release"/> when killed. Pooling mechanics live in <see cref="PrefabPool"/>.
    /// </summary>
    public class EnemyPool : PrefabPool
    {
        /// <summary>
        /// Release the previous room's enemies, then spawn a random enemy from <paramref name="biome"/>
        /// at each world position. Pass null/empty to just clear (e.g. a cached room).
        /// </summary>
        public void Populate(IReadOnlyList<Vector3> positions, BiomeConfig biome)
        {
            ReleaseAll();
            var prefabs = biome != null ? biome.EnemyPrefabs : null;
            if (positions == null || prefabs == null) return;

            foreach (var pos in positions)
            {
                var prefab = PickRandom(prefabs);
                if (prefab == null) continue;
                var go = Rent(prefab);
                if (go == null) continue;
                go.transform.position = pos;
                if (go.TryGetComponent<EnemyController>(out var enemy))
                    enemy.AssignPool(this);
                go.SetActive(true);
            }
        }

        /// <summary>Random non-null prefab from the set (null if all empty).</summary>
        private static GameObject PickRandom(GameObject[] prefabs)
        {
            int valid = 0;
            foreach (var p in prefabs) if (p != null) valid++;
            if (valid == 0) return null;

            int pick = Random.Range(0, valid);
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                if (pick-- == 0) return p;
            }
            return null;
        }
    }
}
