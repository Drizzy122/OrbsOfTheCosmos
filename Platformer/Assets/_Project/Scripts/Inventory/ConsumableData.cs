using UnityEngine;

namespace Platformer
{
    [CreateAssetMenu(menuName = "Inventory/Consumable", fileName = "NewConsumable")]
    public class ConsumableData : ItemData
    {
        [Header("Consumable")]
        [field: SerializeField, Min(0)] public int healAmount { get; private set; }
        // Future: swap to an effect-strategy ScriptableObject (matches the AbilityStrategy pattern)
        // when consumables need more than just healing.

        /// <summary>
        /// Apply this consumable's effect to the user.
        /// Returns false when the effect would be wasted (e.g. heal at full HP) —
        /// callers should only decrement the stack when this returns true.
        /// </summary>
        public bool Use(GameObject user)
        {
            if (healAmount > 0)
            {
                // The user may be the Inventory child object — Health sits on the Player root
                var health = user.GetComponentInParent<Health>();
                if (health == null) return false;
                if (health.HealthPercent >= 1f) return false;   // full HP — don't waste it
                health.AddHealth(healAmount);
                return true;
            }
            return false;
        }
    }
}
