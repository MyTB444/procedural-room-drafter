using UnityEngine;
using UnityEngine.InputSystem;

namespace TRV
{
    /// <summary>
    /// TESTING hotkeys for the NEXT room's biome: 0 → Aqua (Corridors), 1 → Halls, 2 → Anubis
    /// (Open), 3 → Lands (Plain) — found BY LAYOUT in <see cref="RoomManager.Biomes"/>, so list
    /// order doesn't matter — via <see cref="RoomManager.SelectBiome"/> (a forced pick that
    /// bypasses the draft flow once). Drop this on the RoomManager object.
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
            else if (keyboard[Key.Digit3].wasPressedThisFrame) SelectByLayout(BiomeLayout.Plain); // Lands
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
