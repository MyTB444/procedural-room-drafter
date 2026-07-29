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
        /// Each piece is wired to REMOVE its placement from <paramref name="placements"/> when broken,
        /// so a destroyed piece doesn't respawn when the player returns (the room's snapshot rides the
        /// same list).
        /// </summary>
        /// <summary>Pre-instantiate a batch of each island-decor prefab (weights ignored — every type
        /// can appear). Convenience overload over <see cref="PrefabPool.Prewarm"/>.</summary>
        public void Prewarm(IslandDecorEntry[] entries)
        {
            if (entries == null) return;
            var prefabs = new List<GameObject>(entries.Length);
            foreach (var e in entries)
                if (e != null && e.Prefab != null) prefabs.Add(e.Prefab);
            Prewarm(prefabs);
        }

        /// <summary>Spawn a breakable filler decor (e.g. crates/vases) at each cell — ADDS to the active
        /// set without releasing the previous decor, so call it AFTER <see cref="Show"/>. Each piece
        /// REMOVES its cell from <paramref name="cells"/> when broken (the room's snapshot rides the same
        /// list, so a smashed crate stays gone on revisit). Skips/prunes collision cells. (The next room's
        /// Show releases these too.) For the Halls big-igroom filler.</summary>
        public void Fill(GameObject prefab, IList<Vector2Int> cells, TilemapPainter painter,
                         Vector3 worldOffset = default)
        {
            if (prefab == null || cells == null || painter == null) return;
            foreach (var cell in new List<Vector2Int>(cells)) // copy: the break callback mutates `cells`
            {
                if (painter.HasSolidAt(cell.x, cell.y)) { cells.Remove(cell); continue; } // never on a collision tile
                var go = Rent(prefab);
                if (go == null) continue;
                go.transform.position = painter.CellCenterWorld(cell.x, cell.y) + worldOffset;
                var c = cell; // capture by value for the forget callback
                go.GetComponent<IslandDecorPiece>().Activate(this, () => cells.Remove(c)); // breakable + persisted
                go.SetActive(true);
            }
        }

        public void Show(List<PatchPlacement> placements, TilemapPainter painter, BiomeConfig biome)
        {
            ReleaseAll();
            var entries = biome != null ? biome.IslandDecorObjects : null;
            if (placements == null || painter == null || entries == null) return;

            foreach (var p in placements)
            {
                if (p.PatchIndex < 0 || p.PatchIndex >= entries.Length) continue;
                var prefab = entries[p.PatchIndex]?.Prefab;
                if (prefab == null) continue; // empty entry
                if (painter.HasSolidAt(p.X, p.Y)) continue; // never on a collision tile (e.g. the waterfall base)
                var go = Rent(prefab);
                if (go == null) continue;
                go.transform.position = painter.CellCenterWorld(p.X, p.Y);
                var placement = p; // capture by value for the forget callback
                go.GetComponent<IslandDecorPiece>().Activate(this, () => placements.Remove(placement));
                go.SetActive(true);
            }
        }
    }
}
