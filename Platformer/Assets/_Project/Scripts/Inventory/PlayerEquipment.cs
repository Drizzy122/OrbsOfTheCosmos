using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Tracks which items from the Inventory are equipped per slot, spawns their
    /// visual prefabs on the right sockets, and broadcasts equip/unequip events.
    /// Items stay in the Inventory while equipped (Destiny-style).
    /// </summary>
    public class PlayerEquipment : MonoBehaviour, IDataPersistence
    {
        [Header("References")]
        [SerializeField] Inventory inventory;
        [SerializeField] Transform weaponSocket;   // hand bone — active weapon prefab spawns here
        [SerializeField] Transform shieldSocket;   // off-hand bone — shield prefab spawns here
        // Future (Phase 4 armor visuals): per-slot armor sockets.

        readonly Dictionary<EquipSlot, InventoryItem> equipped = new();
        readonly Dictionary<EquipSlot, GameObject> spawnedVisuals = new();

        /// <summary>Which weapon slot is currently in hand (Weapon or SecondaryWeapon).
        /// Both can be equipped; only the active one is visible and used for damage.
        /// The shield is independent — always out when equipped.</summary>
        public EquipSlot ActiveWeaponSlot { get; private set; } = EquipSlot.Weapon;

        /// <summary>The item in hand right now (null when the active slot is empty).</summary>
        public InventoryItem ActiveWeapon => GetEquipped(ActiveWeaponSlot);

        public event System.Action<EquipSlot> OnActiveWeaponChanged;

        /// <summary>The hand bone the active weapon is parented to.</summary>
        public Transform WeaponSocket => weaponSocket;

        void Reset() => inventory = GetComponent<Inventory>();

        public InventoryItem GetEquipped(EquipSlot slot)
            => equipped.TryGetValue(slot, out var item) ? item : null;

        public GameObject GetEquippedVisual(EquipSlot slot)
            => spawnedVisuals.TryGetValue(slot, out var go) ? go : null;

        public bool HasEquipped(EquipSlot slot) => equipped.ContainsKey(slot);
        public bool IsEquipped(InventoryItem item) => item != null && equipped.ContainsValue(item);

        /// <summary>Equip an inventory item into its appropriate slot. Swaps out anything already there.</summary>
        public bool Equip(InventoryItem item)
        {
            if (item?.data == null) return false;
            var slot = DetermineSlot(item.data);
            if (slot == EquipSlot.None) return false;

            Unequip(slot);

            equipped[slot] = item;

            // Held weapons (primary/secondary) only show their visual while ACTIVE.
            // Shields and everything else spawn immediately.
            bool isHeldWeaponSlot = slot == EquipSlot.Weapon || slot == EquipSlot.SecondaryWeapon;
            if (!isHeldWeaponSlot || slot == ActiveWeaponSlot)
                SpawnVisual(slot, item);

            GameEventsManager.instance?.inventoryEvents.ItemEquipped(item, slot);
            return true;
        }

        /// <summary>Toggle the in-hand weapon between Primary and Secondary.
        /// Does nothing if the other slot is empty.</summary>
        public void SwapActiveWeapon()
        {
            var other = ActiveWeaponSlot == EquipSlot.Weapon
                ? EquipSlot.SecondaryWeapon
                : EquipSlot.Weapon;
            var otherItem = GetEquipped(other);
            if (otherItem == null) return;

            DestroyVisual(ActiveWeaponSlot);   // stow the current weapon (if any)
            ActiveWeaponSlot = other;
            SpawnVisual(other, otherItem);     // draw the new one

            OnActiveWeaponChanged?.Invoke(other);
        }

        public void Unequip(EquipSlot slot)
        {
            if (!equipped.TryGetValue(slot, out var item)) return;
            equipped.Remove(slot);
            DestroyVisual(slot);
            GameEventsManager.instance?.inventoryEvents.ItemUnequipped(item, slot);
        }

        EquipSlot DetermineSlot(ItemData data)
        {
            if (data is WeaponData weapon) return weapon.slot;
            if (data is ArmorData armor) return armor.slot;
            return EquipSlot.None;
        }

        void SpawnVisual(EquipSlot slot, InventoryItem item)
        {
            GameObject prefab = null;
            Transform parent = null;

            if (item.data is WeaponData weapon)
            {
                prefab = weapon.weaponPrefab;
                if (weapon.slot == EquipSlot.Shield)
                {
                    parent = shieldSocket;
                    if (shieldSocket == null)
                        Debug.LogWarning("PlayerEquipment: no shieldSocket assigned — shield visual skipped.", this);
                }
                else
                {
                    parent = weaponSocket;
                }
            }
            else if (item.data is ArmorData armor)
            {
                prefab = armor.armorPrefab;
                // Armor parent / mesh swap: implemented in Phase 4.
            }

            if (prefab == null || parent == null) return;

            // Instantiate(prefab, parent) keeps the prefab's own local position,
            // rotation and scale relative to the socket, so the grip authored on
            // the prefab is respected. The weapon data can nudge it on top.
            var visual = Instantiate(prefab, parent);
            if (item.data is WeaponData weaponData)
            {
                visual.transform.localPosition += weaponData.gripPositionOffset;
                visual.transform.localRotation *= Quaternion.Euler(weaponData.gripRotationOffset);
            }
            spawnedVisuals[slot] = visual;
        }

        void DestroyVisual(EquipSlot slot)
        {
            if (!spawnedVisuals.TryGetValue(slot, out var visual)) return;
            if (visual != null) Destroy(visual);
            spawnedVisuals.Remove(slot);
        }

        // ─── IDataPersistence ──────────────────────────────────────────────────
        // We can't equip during LoadData because Inventory.LoadData might run
        // after ours. Stash the equipped ID map and resolve it in Start().

        Dictionary<string, string> pendingEquipped;

        public void SaveData(GameData data)
        {
            data.equippedSlots = new SerializableDictionary<string, string>();
            foreach (var kvp in equipped)
            {
                var item = kvp.Value;
                if (item?.data == null || string.IsNullOrEmpty(item.data.id)) continue;
                data.equippedSlots[kvp.Key.ToString()] = item.data.id;
            }
        }

        public void LoadData(GameData data)
        {
            // Defer to Start() so Inventory has finished its own LoadData.
            pendingEquipped = new Dictionary<string, string>();
            if (data.equippedSlots == null) return;
            foreach (var kvp in data.equippedSlots)
                pendingEquipped[kvp.Key] = kvp.Value;
        }

        void Start()
        {
            if (pendingEquipped == null || pendingEquipped.Count == 0) return;
            if (inventory == null) { pendingEquipped = null; return; }

            foreach (var kvp in pendingEquipped)
            {
                if (!System.Enum.TryParse<EquipSlot>(kvp.Key, out var slot)) continue;

                // Find the matching item in the bag and equip it.
                foreach (var item in inventory.Items)
                {
                    if (item?.data != null && item.data.id == kvp.Value)
                    {
                        Equip(item);
                        break;
                    }
                }
            }
            pendingEquipped = null;
        }
    }
}
