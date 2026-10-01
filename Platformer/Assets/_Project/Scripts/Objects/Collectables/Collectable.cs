using KBCore.Refs;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// One script for every world pickup. Pick a type in the inspector:
    ///   Coin / LuminOrb / Ecliptium — currency: fires misc events (counters, quests)
    ///   Health                     — instant heal on touch (skipped at full HP)
    ///   Item                       — adds an ItemData (weapon/armor/consumable) to the inventory
    /// Replaces the old SwordPickup and HealthCollectible scripts.
    /// </summary>
    public class Collectable : ValidatedMonoBehaviour, IDataPersistence
    {
        [field: Header("Settings")]
        [field: SerializeField] CollectableType collectibleType;
        [field: SerializeField] string id;
        [field: SerializeField] bool isCollected;

        [Header("Health Settings (type = Health)")]
        [SerializeField] float healthValue = 25f;

        [Header("Item Settings (type = Item)")]
        [SerializeField] ItemData itemToGive;
        [SerializeField, Min(1)] int itemQuantity = 1;
        [Tooltip("If the matching equipment slot is empty, equip this item on pickup.")]
        [SerializeField] bool autoEquipIfSlotEmpty = true;

        [field: Header("Visuals")]
        [field: SerializeField, Anywhere] GameObject visualObject;

        [ContextMenu("Generate guid for id")]
        private void GenerateGuid() => id = System.Guid.NewGuid().ToString();

        /// <summary>Call on runtime-spawned drops (enemy loot) so they get a fresh
        /// save-id and never collide with authored pickups in the save file.</summary>
        public void InitAsRuntimeDrop()
        {
            id = System.Guid.NewGuid().ToString();
            isCollected = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;
            if (!other.CompareTag("Player")) return;
            Collect(other.gameObject);
        }

        void Collect(GameObject player)
        {
            switch (collectibleType)
            {
                case CollectableType.Coin:
                    GameEventsManager.instance.miscEvents.CoinCollected();
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.coinCollected, transform.position);
                    break;

                case CollectableType.LuminOrb:
                    GameEventsManager.instance.miscEvents.LuminCollected();
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.luminCollected, transform.position);
                    break;

                case CollectableType.Ecliptium:
                    GameEventsManager.instance.miscEvents.EcliptiumCollected();
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.luminCollected, transform.position);
                    break;

                case CollectableType.Health:
                    var health = player.GetComponentInChildren<Health>();
                    if (health == null) return;
                    if (health.HealthPercent >= 1f) return;   // full HP — leave the pickup in the world
                    health.AddHealth(healthValue);
                    // TODO: dedicated heal-pickup FMOD event
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.luminCollected, transform.position);
                    break;

                case CollectableType.Item:
                    if (!TryGiveItem(player)) return;          // bag full / no inventory — leave the pickup
                    // TODO: dedicated item-pickup FMOD event
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.luminCollected, transform.position);
                    break;
            }

            isCollected = true;
            Destroy(gameObject);
        }

        bool TryGiveItem(GameObject player)
        {
            if (itemToGive == null)
            {
                Debug.LogWarning($"{name}: Collectable type is Item but no ItemData assigned.", this);
                return false;
            }

            // Inventory/equipment live on a child of the Player, so search the hierarchy
            var inventory = player.GetComponentInChildren<Inventory>();
            if (inventory == null) return false;
            if (!inventory.Add(itemToGive, itemQuantity)) return false;

            if (autoEquipIfSlotEmpty)
            {
                var equipment = player.GetComponentInChildren<PlayerEquipment>();
                var slot = SlotFor(itemToGive);
                if (equipment != null && slot != EquipSlot.None && !equipment.HasEquipped(slot))
                {
                    // The most recently added matching entry is at the end of the list.
                    for (int i = inventory.Items.Count - 1; i >= 0; i--)
                    {
                        if (inventory.Items[i].data == itemToGive)
                        {
                            equipment.Equip(inventory.Items[i]);
                            break;
                        }
                    }
                }
            }
            return true;
        }

        static EquipSlot SlotFor(ItemData data)
        {
            if (data is WeaponData weapon) return weapon.slot;
            if (data is ArmorData armor) return armor.slot;
            return EquipSlot.None;   // consumables etc. don't equip
        }

        // ─── IDataPersistence ──────────────────────────────────────────────────

        public void LoadData(GameData data)
        {
            switch (collectibleType)
            {
                case CollectableType.Coin:
                    data.coinsCollected.TryGetValue(id, out isCollected);
                    break;
                case CollectableType.LuminOrb:
                    data.luminCollected.TryGetValue(id, out isCollected);
                    break;
                case CollectableType.Ecliptium:
                    data.ecliptiumCollected.TryGetValue(id, out isCollected);
                    break;
                case CollectableType.Health:
                case CollectableType.Item:
                    data.collectablesCollected.TryGetValue(id, out isCollected);
                    break;
            }

            if (isCollected)
            {
                // Note: Item pickups don't need to re-grant on load — the item itself
                // is restored by Inventory's own persistence.
                if (visualObject != null) visualObject.SetActive(false);
                Destroy(gameObject, 0.5f);
            }
        }

        public void SaveData(GameData data)
        {
            switch (collectibleType)
            {
                case CollectableType.Coin:
                    data.coinsCollected[id] = isCollected;
                    break;
                case CollectableType.LuminOrb:
                    data.luminCollected[id] = isCollected;
                    break;
                case CollectableType.Ecliptium:
                    data.ecliptiumCollected[id] = isCollected;
                    break;
                case CollectableType.Health:
                case CollectableType.Item:
                    data.collectablesCollected[id] = isCollected;
                    break;
            }
        }
    }
}
