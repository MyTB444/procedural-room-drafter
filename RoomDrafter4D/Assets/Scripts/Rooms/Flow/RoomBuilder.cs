using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Test/runtime driver: generates one room for (worldSeed, roomCoord) and paints it.
    /// Use the context-menu "Regenerate" (right-click the component), or change the seed/coord,
    /// to preview different deterministic rooms — even in edit mode.
    /// </summary>
    [RequireComponent(typeof(TilemapPainter))]
    public class RoomBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BiomeConfig biome;
        [SerializeField] private TilemapPainter painter;

        [Header("Which room")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private Vector2Int roomCoord = Vector2Int.zero;

        private void Awake()
        {
            if (painter == null) painter = GetComponent<TilemapPainter>();
        }

        private void Start() => Build();

        [ContextMenu("Regenerate")]
        public void Build()
        {
            if (painter == null) painter = GetComponent<TilemapPainter>();
            if (biome == null)
            {
                Debug.LogError($"[{nameof(RoomBuilder)}] No BiomeConfig assigned on '{name}'.", this);
                return;
            }

            var rng = RoomSeed.Rng(worldSeed, roomCoord);
            var grid = new RoomGenerator(biome).Generate(rng);
            painter.Paint(grid, biome);
        }
    }
}
