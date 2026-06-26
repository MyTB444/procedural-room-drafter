using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Pool for <see cref="Collectable"/> pickups, keyed BY PREFAB (see <see cref="PrefabPool"/>) so one
    /// pool serves every pickup type. <see cref="Spawn"/> rents + drops an instance at a world position;
    /// the collectable returns itself here when collected. A lazy static <see cref="Instance"/> lets
    /// droppers reach the pool without per-object wiring (mirrors <see cref="PlayerLocator"/>).
    /// </summary>
    public class CollectablePool : PrefabPool
    {
        private static CollectablePool _instance;

        /// <summary>The scene's collectable pool (fake-null re-finds it after a reload). Null if none.</summary>
        public static CollectablePool Instance =>
            _instance != null ? _instance : (_instance = FindFirstObjectByType<CollectablePool>());

        /// <summary>Rent a pooled <paramref name="prefab"/> and drop it at <paramref name="position"/>.</summary>
        public void Spawn(GameObject prefab, Vector3 position)
        {
            var go = Rent(prefab);
            if (go == null) return; // null prefab slot
            if (!go.TryGetComponent<Collectable>(out var collectable))
            {
                Release(go); // prefab isn't a Collectable — undo the rent
                return;
            }
            go.SetActive(true);
            collectable.Drop(this, position);
        }
    }
}
