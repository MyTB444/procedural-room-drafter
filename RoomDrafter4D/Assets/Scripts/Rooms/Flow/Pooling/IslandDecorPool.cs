using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Pools island-decor objects (auto-adds an <see cref="IslandDecorPiece"/> to each so it's
    /// hittable). <see cref="Show"/> releases the previous room's objects and places pooled ones for
    /// the room's placements, indexing the prefab set of the ROOM'S biome — so one pool serves any
    /// biome. Pooling mechanics live in <see cref="PrefabPool"/>.
    /// </summary>
    public class IslandDecorPool : PrefabPool
    {
        protected override void OnCreated(GameObject instance)
        {
            if (!instance.TryGetComponent<IslandDecorPiece>(out _))
                instance.AddComponent<IslandDecorPiece>(); // makes it hittable → returns to this pool on hit
        }

        /// <summary>
        /// Show decor for a room: release the previous room's objects, then place a pooled instance
        /// at each placement's cell (world position via <paramref name="painter"/>), using
        /// <paramref name="biome"/>'s decor prefabs (the same biome that generated the placements).
        /// </summary>
        public void Show(IReadOnlyList<PatchPlacement> placements, TilemapPainter painter, BiomeConfig biome)
        {
            ReleaseAll();
            var prefabs = biome != null ? biome.IslandDecorObjects : null;
            if (placements == null || painter == null || prefabs == null) return;

            foreach (var p in placements)
            {
                if (p.PatchIndex < 0 || p.PatchIndex >= prefabs.Length) continue;
                var go = Rent(prefabs[p.PatchIndex]);
                if (go == null) continue; // empty prefab slot
                go.transform.position = painter.CellCenterWorld(p.X, p.Y);
                go.GetComponent<IslandDecorPiece>().Activate(this);
                go.SetActive(true);
            }
        }
    }
}
