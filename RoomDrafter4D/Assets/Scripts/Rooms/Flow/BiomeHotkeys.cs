using UnityEngine;
using UnityEngine.InputSystem;

namespace TRV
{
    /// <summary>
    /// Picks the biome for the NEXT room with the number keys: press 0 → biome index 0, 1 → index 1,
    /// and so on (via <see cref="RoomManager.SelectBiome(int)"/>). Out-of-range digits are ignored.
    /// Drop this on the RoomManager object. (Placeholder selector until a real in-game UI exists.)
    /// </summary>
    [RequireComponent(typeof(RoomManager))]
    public class BiomeHotkeys : MonoBehaviour
    {
        // Index i = the digit-row key that selects biome i (Key.Digit0 isn't adjacent to Digit1).
        private static readonly Key[] DigitKeys =
        {
            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        private RoomManager _rooms;

        private void Awake() => _rooms = GetComponent<RoomManager>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _rooms == null) return;

            for (int i = 0; i < DigitKeys.Length; i++)
            {
                if (!keyboard[DigitKeys[i]].wasPressedThisFrame) continue;
                if (i < _rooms.Biomes.Count && _rooms.Biomes[i] != null)
                {
                    _rooms.SelectBiome(i);
                    Debug.Log($"[Biome] Next room → biome {i} ({_rooms.Biomes[i].name}).", this);
                }
                break;
            }
        }
    }
}
