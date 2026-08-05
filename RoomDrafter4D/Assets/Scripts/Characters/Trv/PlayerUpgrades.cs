using System.Collections.Generic;
using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Runtime upgrade state for the player — a MODIFIER LAYER over the base <see cref="TRVStats"/> that
    /// interactable <see cref="Upgrade"/>s feed into (the stats SO asset is never mutated). Holds a
    /// summed FLAT BONUS per <see cref="Stat"/> (+number upgrades, 0 = unmodified; negative to
    /// reduce, e.g. attack cooldown) and a set of unlocked <see cref="Ability"/>s (locked by
    /// default). <see cref="TRVController"/> reads these when applying its stats. Extend the enums
    /// to add new upgradeable stats / abilities — no other plumbing needed.
    /// Auto-added to the player by the controller, so no manual wiring.
    /// </summary>
    public class PlayerUpgrades : MonoBehaviour
    {
        /// <summary>Stats an upgrade can add to (flat +number).</summary>
        public enum Stat { AttackCooldown, AttackStaminaCost, MaxStamina, StaminaRegen, HealthRegen, MaxHealth, AttackDamage, MoveSpeed, FireBullets, DodgeStaminaCost }

        /// <summary>Abilities an upgrade can unlock (locked until granted).</summary>
        public enum Ability { Dash, Fire }

        /// <summary>Identity of each one-per-run upgrade (so a collected one can't appear again).</summary>
        public enum UpgradeId { Dash, Attack, Stamina, Fire }

        /// <summary>How many times each book CHOICE can be taken per run (its max level).</summary>
        public const int MaxChoiceLevel = 10;

        private readonly Dictionary<Stat, float> _bonuses = new Dictionary<Stat, float>();
        private readonly HashSet<Ability> _unlocked = new HashSet<Ability>();
        private readonly HashSet<UpgradeId> _collected = new HashSet<UpgradeId>();
        private readonly Dictionary<string, int> _choiceLevels = new Dictionary<string, int>();

        /// <summary>Fires whenever an upgrade is applied (a bonus changed or an ability unlocked).</summary>
        public event System.Action Changed;

        /// <summary>Summed flat bonus added to a stat (0 if untouched).</summary>
        public float Bonus(Stat stat) => _bonuses.TryGetValue(stat, out var b) ? b : 0f;

        /// <summary>The effective value of a stat: <c>base + bonus</c>.</summary>
        public float Apply(Stat stat, float baseValue) => baseValue + Bonus(stat);

        /// <summary>Whether an ability has been unlocked by an upgrade.</summary>
        public bool IsUnlocked(Ability ability) => _unlocked.Contains(ability);

        /// <summary>Add a flat bonus to a stat (e.g. +2 health regen; negative to reduce, e.g.
        /// −0.1 attack cooldown).</summary>
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

        /// <summary>Current level of a book choice = how many times it's been taken this run
        /// (0..<see cref="MaxChoiceLevel"/>). Keyed by the choice's LABEL, so the same choice
        /// offered by different books shares one level.</summary>
        public int ChoiceLevel(string label) =>
            label != null && _choiceLevels.TryGetValue(label, out var level) ? level : 0;

        /// <summary>Record one pickup of a book choice (no-op at <see cref="MaxChoiceLevel"/>).</summary>
        public void IncrementChoiceLevel(string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            int level = ChoiceLevel(label);
            if (level >= MaxChoiceLevel) return;
            _choiceLevels[label] = level + 1;
            Changed?.Invoke();
        }
    }
}
