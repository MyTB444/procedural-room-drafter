using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Pools island-decor objects. Reads its prefab set from <see cref="BiomeConfig.IslandDecorObjects"/>
    /// and auto-adds an <see cref="IslandDecorPiece"/> to each instance so it's hittable. When a room
    /// is shown, <see cref="Show"/> releases the previous room's objects and places pooled ones at
    /// the room's <see cref="RoomGrid.IslandDecor"/> placements. Pooling mechanics live in
    /// <see cref="PrefabPool"/>.
    /// </summary>
    public class IslandDecorPool : PrefabPool
    {
        [Tooltip("Biome whose IslandDecorObjects feed the pool (the same asset RoomManager uses).")]
        [SerializeField] private BiomeConfig biome;

        protected override GameObject[] Prefabs => biome != null ? biome.IslandDecorObjects : null;

        protected override void OnCreated(GameObject instance)
        {
            if (!instance.TryGetComponent<IslandDecorPiece>(out _))
                instance.AddComponent<IslandDecorPiece>(); // makes it hittable → returns to this pool on hit
        }

        /// <summary>
        /// Show decor for a room: release the previous room's objects, then place a pooled instance
        /// at each placement's cell (world position via <paramref name="painter"/>).
        /// </summary>
        public void Show(IReadOnlyList<PatchPlacement> placements, TilemapPainter painter)
        {
            ReleaseAll();
            if (placements == null || painter == null) return;

            foreach (var p in placements)
            {
                var go = Rent(p.PatchIndex);
                if (go == null) continue; // empty prefab slot
                go.transform.position = painter.CellCenterWorld(p.X, p.Y);
                go.GetComponent<IslandDecorPiece>().Activate(this);
                go.SetActive(true);
            }
        }
    }
}
