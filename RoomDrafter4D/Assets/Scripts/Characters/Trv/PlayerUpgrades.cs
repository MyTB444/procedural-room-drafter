using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Runtime upgrade state for the player — a MODIFIER LAYER over the base <see cref="TRVStats"/> that
    /// interactable <see cref="Upgrade"/>s feed into (the stats SO asset is never mutated). Holds a
    /// compounding multiplier per <see cref="Stat"/> (1 = unmodified) and a set of unlocked
    /// <see cref="Ability"/>s (locked by default). <see cref="TRVController"/> reads these when applying
    /// its stats. Extend the enums to add new upgradeable stats / abilities — no other plumbing needed.
    /// Auto-added to the player by the controller, so no manual wiring.
    /// </summary>
    public class PlayerUpgrades : MonoBehaviour
    {
        /// <summary>Stats an upgrade can scale (multiplier) and/or add to (bonus).</summary>
        public enum Stat { AttackCooldown, AttackStaminaCost, MaxStamina, StaminaRegen, HealthRegen }

        /// <summary>Abilities an upgrade can unlock (locked until granted).</summary>
        public enum Ability { Dash }

        /// <summary>Identity of each one-per-run upgrade (so a collected one can't appear again).</summary>
        public enum UpgradeId { Dash, Attack, Stamina }

        private readonly Dictionary<Stat, float> _multipliers = new Dictionary<Stat, float>();
        private readonly Dictionary<Stat, float> _bonuses = new Dictionary<Stat, float>();
        private readonly HashSet<Ability> _unlocked = new HashSet<Ability>();
        private readonly HashSet<UpgradeId> _collected = new HashSet<UpgradeId>();

        /// <summary>Fires whenever an upgrade is applied (a modifier changed or an ability unlocked).</summary>
        public event System.Action Changed;

        /// <summary>Compounded multiplier for a stat (1 if untouched).</summary>
        public float Multiplier(Stat stat) => _multipliers.TryGetValue(stat, out var m) ? m : 1f;

        /// <summary>Summed flat bonus added to a stat (0 if untouched).</summary>
        public float Bonus(Stat stat) => _bonuses.TryGetValue(stat, out var b) ? b : 0f;

        /// <summary>The effective value of a stat: <c>base × multiplier + bonus</c>. (Additive bonuses let
        /// an upgrade grant a stat that starts at 0, e.g. health regen, where a multiplier can't.)</summary>
        public float Apply(Stat stat, float baseValue) => baseValue * Multiplier(stat) + Bonus(stat);

        /// <summary>Whether an ability has been unlocked by an upgrade.</summary>
        public bool IsUnlocked(Ability ability) => _unlocked.Contains(ability);

        /// <summary>Compound a multiplier onto a stat (e.g. 0.8 = −20%, 1.25 = +25%).</summary>
        public void ScaleStat(Stat stat, float factor)
        {
            _multipliers[stat] = Multiplier(stat) * factor;
            Changed?.Invoke();
        }

        /// <summary>Add a flat bonus to a stat (e.g. +2 health regen; negative to reduce).</summary>
        public void AddStat(Stat stat, float amount)
        {
            _bonuses[stat] = Bonus(stat) + amount;
            Changed?.Invoke();
        }

        /// <summary>Unlock an ability (no-op if already unlocked).</summary>
        public void Unlock(Ability ability)
        {
            if (_unlocked.Add(ability)) Changed?.Invoke();
        }

        /// <summary>Whether a one-per-run upgrade has already been collected this run.</summary>
        public bool HasCollected(UpgradeId id) => _collected.Contains(id);

        /// <summary>Mark a one-per-run upgrade collected so it can't be offered again.</summary>
        public void MarkCollected(UpgradeId id)
        {
            if (_collected.Add(id)) Changed?.Invoke();
        }
    }
}
