using UnityEngine;
using UnityEngine.InputSystem;

namespace TRV
{
    /// <summary>
    /// TESTING hotkeys for the NEXT room's biome: press 0 → the
    /// Aqua (Corridors) biome, 1 → the Halls biome, 2 → the Anubis (Open) biome (found BY LAYOUT in
    /// <see cref="RoomManager.Biomes"/>, so list order doesn't matter), via
    /// <see cref="RoomManager.SelectBiome(BiomeConfig)"/>. Drop this on the RoomManager object.
    /// (Placeholder selector until a real in-game flow exists.)
    /// </summary>
    [RequireComponent(typeof(RoomManager))]
    public class BiomeHotkeys : MonoBehaviour
    {
        private RoomManager _rooms;

        private void Awake() => _rooms = GetComponent<RoomManager>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _rooms == null) return;

            if (keyboard[Key.Digit0].wasPressedThisFrame) SelectByLayout(BiomeLayout.Corridors); // Aqua
            else if (keyboard[Key.Digit1].wasPressedThisFrame) SelectByLayout(BiomeLayout.Halls);
            else if (keyboard[Key.Digit2].wasPressedThisFrame) SelectByLayout(BiomeLayout.Open); // Anubis
        }

        private void SelectByLayout(BiomeLayout layout)
        {
            var biomes = _rooms.Biomes;
            for (int i = 0; i < biomes.Count; i++)
            {
                if (biomes[i] == null || biomes[i].Layout != layout) continue;
                _rooms.SelectBiome(biomes[i]);
                Debug.Log($"[Biome] Next room → {layout} ({biomes[i].name}).", this);
                return;
            }
            Debug.LogWarning($"[Biome] No {layout} biome in RoomManager's biomes list.", this);
        }
    }
}
