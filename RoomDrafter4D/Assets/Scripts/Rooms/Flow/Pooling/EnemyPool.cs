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
        private static EnemyPool _instance;

        /// <summary>The scene's enemy pool (fake-null re-finds it after a reload). Null if none —
        /// lets non-room code (e.g. the player) ask whether the room still has enemies.</summary>
        public static EnemyPool Instance =>
            _instance != null ? _instance : (_instance = FindAnyObjectByType<EnemyPool>());

        /// <summary>True when the room still has live enemies (a dying enemy counts until its fade ends).</summary>
        public static bool RoomHasEnemies => Instance != null && Instance.ActiveCount > 0;

        /// <summary>
        /// Release the previous room's enemies, then spawn one enemy at each world position from
        /// <paramref name="biome"/>: a random <see cref="BiomeConfig.EnemyPrefabs"/> per slot, except
        /// ONE random slot may be a miniboss (max 1 per room, rolled by <see cref="BiomeConfig.MinibossChance"/>
        /// when <see cref="BiomeConfig.MinibossPrefabs"/> is set). Pass null/empty to just clear.
        /// </summary>
        public void Populate(IReadOnlyList<Vector3> positions, BiomeConfig biome)
        {
            ReleaseAll();
            if (positions == null || positions.Count == 0 || biome == null) return;

            // At most ONE miniboss per room — it takes slot 0, which RoomManager fills with the room's
            // LARGEST open walking space (so the miniboss spawns where it has the most room).
            var miniboss = PickMiniboss(biome);
            int minibossSlot = miniboss != null ? 0 : -1;

            for (int i = 0; i < positions.Count; i++)
            {
                var prefab = i == minibossSlot ? miniboss : PickRandom(biome.EnemyPrefabs);
                if (prefab == null) continue;
                var go = Rent(prefab);
                if (go == null) continue;
                go.transform.position = positions[i];
                if (go.TryGetComponent<EnemyController>(out var enemy))
                    enemy.AssignPool(this);
                go.SetActive(true);
            }
        }

        /// <summary>A random miniboss prefab if the biome has any AND the chance rolls in; else null.</summary>
        private static GameObject PickMiniboss(BiomeConfig biome)
        {
            var minibosses = biome.MinibossPrefabs;
            if (minibosses == null || minibosses.Length == 0) return null;
            if (Random.value > biome.MinibossChance) return null;
            return PickRandom(minibosses);
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
