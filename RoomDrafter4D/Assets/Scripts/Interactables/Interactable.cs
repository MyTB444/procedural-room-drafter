using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Base for a world object the player can interact with via the Interact action (no hitbox — purely
    /// PROXIMITY based). Each instance registers in a shared list; the player asks
    /// <see cref="FindNearestAvailable"/> for the closest one in range that <see cref="CanInteract"/>.
    /// A single world-space <see cref="InteractPromptUI"/> shows an "E" over whichever instance is
    /// currently available. Concrete types implement <see cref="CanInteract"/> + <see cref="Interact"/>.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("How close (world units) the player must be to interact / show the prompt.")]
        [SerializeField, Min(0f)] protected float interactRange = 1.5f;

        private static readonly List<Interactable> Active = new List<Interactable>();

        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);

        /// <summary>True when this can currently be used (e.g. not already used, room cleared, has a key).</summary>
        protected abstract bool CanInteract();

        /// <summary>Perform the interaction. Only called when <see cref="IsAvailable"/> was true.</summary>
        public abstract void Interact(TRVController player);

        /// <summary>Available = usable AND the player is within range of it.</summary>
        public bool IsAvailable(Vector2 playerPos) =>
            CanInteract() &&
            ((Vector2)transform.position - playerPos).sqrMagnitude <= interactRange * interactRange;

        /// <summary>The nearest available interactable to <paramref name="pos"/>, or null if none in range.</summary>
        public static Interactable FindNearestAvailable(Vector2 pos)
        {
            Interactable best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                var it = Active[i];
                if (it == null || !it.IsAvailable(pos)) continue;
                float sq = ((Vector2)it.transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = it; }
            }
            return best;
        }
    }
}
