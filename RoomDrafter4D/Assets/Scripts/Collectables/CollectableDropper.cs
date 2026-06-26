using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Holds a list of possible <see cref="Collectable"/> drops, each with its OWN adjustable chance,
    /// and rolls them when the thing it's authored on is destroyed — e.g. a broken island-decor piece
    /// calls <see cref="TryDrop"/>. Entries are rolled INDEPENDENTLY, so one break can yield several
    /// pickups, one, or none. Per-prefab and biome-agnostic; spawns through the scene's
    /// <see cref="CollectablePool"/>. Optional component: decor without one drops nothing.
    /// </summary>
    public class CollectableDropper : MonoBehaviour
    {
        /// <summary>One possible drop: a pickup prefab and its independent chance to drop.</summary>
        [System.Serializable]
        public class DropEntry
        {
            [Tooltip("The collectable prefab to drop (must have a Collectable component).")]
            public GameObject Prefab;

            [Tooltip("Independent chance this entry drops: 0 = never, 1 = always.")]
            [Range(0f, 1f)] public float Chance = 0.25f;
        }

        [Tooltip("Possible drops — each rolled INDEPENDENTLY, so a break can yield several, one, or none.")]
        [SerializeField] private DropEntry[] drops;

        [Tooltip("Random spread around the drop point so multiple drops don't perfectly overlap (world units).")]
        [SerializeField, Min(0f)] private float scatterRadius = 0.25f;

        /// <summary>Roll every entry; spawn each that hits at a (scattered) <paramref name="position"/>.</summary>
        public void TryDrop(Vector3 position)
        {
            if (drops == null || drops.Length == 0) return;
            var pool = CollectablePool.Instance;
            if (pool == null) return;

            foreach (var drop in drops)
            {
                if (drop == null || drop.Prefab == null || drop.Chance <= 0f) continue;
                if (Random.value > drop.Chance) continue; // this entry didn't roll
                var offset = (Vector3)(Random.insideUnitCircle * scatterRadius);
                pool.Spawn(drop.Prefab, position + offset);
            }
        }
    }
}
