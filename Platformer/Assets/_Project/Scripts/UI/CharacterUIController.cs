using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Renders the equipped paper-doll on the Character tab. Every slot (weapons AND
    /// armor) has its own always-visible 3×3 mini-grid of alternatives beside it,
    /// Destiny-style. Hovering a row expands its grid (via USS); clicking a grid tile
    /// equips it; clicking the equipped slot unequips. Hover shows the detail panel.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class CharacterUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PanelRenderer document;
        [SerializeField] PlayerEquipment equipment;
        [SerializeField] Inventory inventory;

        static readonly EquipSlot[] WeaponSlots =
        {
            EquipSlot.Weapon, EquipSlot.SecondaryWeapon, EquipSlot.Shield
        };
        static readonly EquipSlot[] ArmorSlots =
        {
            EquipSlot.Head, EquipSlot.Arms, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.ClassItem
        };

        readonly Dictionary<EquipSlot, VisualElement> slotElements = new();
        readonly Dictionary<EquipSlot, VisualElement> slotGrids = new();

        VisualElement detailPanel;

        void Reset() => document = GetComponent<PanelRenderer>();

        void OnEnable()
        {
            document.RegisterUIReloadCallback(OnUIReload);
            SubscribeEvents();
        }

        void OnDisable()
        {
            document.UnregisterUIReloadCallback(OnUIReload);
            UnsubscribeEvents();
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            detailPanel = root.Q<VisualElement>("detail-panel");
            // Force-hide so it can be left visible in UI Builder while editing.
            ItemDetailPanel.Hide(detailPanel);

            BindEquipSlots(root);
            BindSlotGrids(root);

            Refresh();
            RefreshAllSlotGrids();
        }

        // ─── binding ───────────────────────────────────────────────────────────

        void BindEquipSlots(VisualElement root)
        {
            slotElements.Clear();
            foreach (var slot in AllSlots())
            {
                var element = root.Q<VisualElement>($"slot-{slot}");
                if (element == null) continue;

                // Slot children (label/icon/name/active-tag) are authored in
                // CharacterUI.uxml so they're visible and editable in UI Builder.
                // Only build them here as a fallback if a slot is missing them.
                if (element.Q<VisualElement>("icon") == null)
                {
                    var label = new Label { name = "label" };
                    label.AddToClassList("equip-slot-label");
                    var icon = new VisualElement { name = "icon" };
                    icon.AddToClassList("equip-slot-icon");
                    var nameLabel = new Label("(empty)") { name = "name" };
                    nameLabel.AddToClassList("equip-slot-name");
                    var activeTag = new Label("ACTIVE") { name = "active-tag" };
                    activeTag.AddToClassList("equip-slot-active-tag");
                    element.Add(label);
                    element.Add(icon);
                    element.Add(nameLabel);
                    element.Add(activeTag);
                }

                // Keep the caption in sync with the enum regardless of UXML text.
                var caption = element.Q<Label>("label");
                if (caption != null) caption.text = slot.ToString();

                var captured = slot;
                // Click the equipped slot → unequip (alternatives sit in the grid beside it).
                element.RegisterCallback<ClickEvent>(_ => equipment?.Unequip(captured));
                // Hover → show details for the equipped item.
                element.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    var eq = equipment != null ? equipment.GetEquipped(captured) : null;
                    if (eq != null) ItemDetailPanel.Show(detailPanel, eq);
                });
                element.RegisterCallback<MouseLeaveEvent>(_ => ItemDetailPanel.Hide(detailPanel));

                slotElements[slot] = element;
            }
        }

        void BindSlotGrids(VisualElement root)
        {
            slotGrids.Clear();
            foreach (var slot in WeaponSlots)
                slotGrids[slot] = root.Q<VisualElement>($"weapons-grid-{slot}");
            foreach (var slot in ArmorSlots)
                slotGrids[slot] = root.Q<VisualElement>($"armor-grid-{slot}");
        }

        IEnumerable<EquipSlot> AllSlots()
        {
            foreach (var s in WeaponSlots) yield return s;
            foreach (var s in ArmorSlots) yield return s;
        }

        static bool MatchesSlot(ItemData data, EquipSlot slot)
        {
            if (data is WeaponData weapon) return weapon.slot == slot;
            if (data is ArmorData armor) return armor.slot == slot;
            return false;
        }

        // ─── events ────────────────────────────────────────────────────────────

        void SubscribeEvents()
        {
            if (equipment != null) equipment.OnActiveWeaponChanged += OnActiveWeaponSwapped;
            if (GameEventsManager.instance == null) return;
            var bus = GameEventsManager.instance.inventoryEvents;
            bus.onItemAdded     += OnInventoryChanged;
            bus.onItemRemoved   += OnInventoryChanged;
            bus.onItemEquipped  += OnEquipChanged;
            bus.onItemUnequipped += OnEquipChanged;
        }

        void UnsubscribeEvents()
        {
            if (equipment != null) equipment.OnActiveWeaponChanged -= OnActiveWeaponSwapped;
            if (GameEventsManager.instance == null) return;
            var bus = GameEventsManager.instance.inventoryEvents;
            bus.onItemAdded     -= OnInventoryChanged;
            bus.onItemRemoved   -= OnInventoryChanged;
            bus.onItemEquipped  -= OnEquipChanged;
            bus.onItemUnequipped -= OnEquipChanged;
        }

        void OnActiveWeaponSwapped(EquipSlot _) => Refresh();

        void OnInventoryChanged(InventoryItem _) => RefreshAllSlotGrids();

        void OnEquipChanged(InventoryItem _, EquipSlot __)
        {
            Refresh();
            RefreshAllSlotGrids();
        }

        // ─── per-slot 3×3 grids ────────────────────────────────────────────────

        // Fixed 9-cell grid (Destiny-style). Owned items fill in; remaining cells
        // render as gray empty placeholders.
        const int GridCellCount = 9;

        void RefreshAllSlotGrids()
        {
            foreach (var slot in slotGrids.Keys)
                RefreshSlotGrid(slot);
        }

        void RefreshSlotGrid(EquipSlot slot)
        {
            if (!slotGrids.TryGetValue(slot, out var grid) || grid == null) return;
            grid.Clear();

            int filled = 0;
            if (inventory != null)
            {
                foreach (var item in inventory.Items)
                {
                    // Only items whose declared slot matches this grid's slot.
                    if (!MatchesSlot(item.data, slot)) continue;
                    // Skip the equipped one — it's shown in the slot icon beside the grid.
                    if (equipment != null && equipment.IsEquipped(item)) continue;
                    grid.Add(BuildMiniTile(item));
                    filled++;
                    if (filled >= GridCellCount) break;
                }
            }

            // Pad with empty gray placeholders out to 9 total cells.
            for (int i = filled; i < GridCellCount; i++)
                grid.Add(BuildEmptyMiniTile());
        }

        VisualElement BuildEmptyMiniTile()
        {
            var tile = new VisualElement();
            tile.AddToClassList("item-tile");
            tile.AddToClassList("item-tile--mini");
            tile.AddToClassList("item-tile--empty");
            return tile;
        }

        VisualElement BuildMiniTile(InventoryItem item)
        {
            var tile = new VisualElement();
            tile.AddToClassList("item-tile");
            tile.AddToClassList("item-tile--mini");
            tile.AddToClassList($"item-tile--{item.data.rarity.ToString().ToLower()}");

            var icon = new VisualElement();
            icon.AddToClassList("item-tile-icon");
            if (item.data.icon != null)
                icon.style.backgroundImage = new StyleBackground(item.data.icon);
            tile.Add(icon);

            tile.tooltip = item.data.displayName;
            tile.RegisterCallback<ClickEvent>(_ => equipment?.Equip(item));
            tile.RegisterCallback<MouseEnterEvent>(_ => ItemDetailPanel.Show(detailPanel, item));
            tile.RegisterCallback<MouseLeaveEvent>(_ => ItemDetailPanel.Hide(detailPanel));
            return tile;
        }

        // ─── paper-doll refresh ────────────────────────────────────────────────

        void Refresh()
        {
            foreach (var slot in AllSlots())
            {
                if (!slotElements.TryGetValue(slot, out var element)) continue;
                var equipped = equipment != null ? equipment.GetEquipped(slot) : null;

                var icon = element.Q<VisualElement>("icon");
                var nameLabel = element.Q<Label>("name");

                element.RemoveFromClassList("equip-slot--vacant");
                element.RemoveFromClassList("equip-slot--active");

                if (equipped != null && equipped.data != null)
                {
                    icon.style.backgroundImage = equipped.data.icon != null
                        ? new StyleBackground(equipped.data.icon)
                        : new StyleBackground();
                    nameLabel.text = equipped.data.displayName;

                    // Gold ACTIVE badge on the weapon currently in hand.
                    bool isHeldWeaponSlot = slot == EquipSlot.Weapon || slot == EquipSlot.SecondaryWeapon;
                    if (isHeldWeaponSlot && equipment != null && slot == equipment.ActiveWeaponSlot)
                        element.AddToClassList("equip-slot--active");
                }
                else
                {
                    icon.style.backgroundImage = new StyleBackground();
                    nameLabel.text = "(empty)";
                    element.AddToClassList("equip-slot--vacant");
                }
            }
        }
    }
}
