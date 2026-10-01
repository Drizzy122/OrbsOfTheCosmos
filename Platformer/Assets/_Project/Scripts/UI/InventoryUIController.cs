using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Renders the player's bag as a tile grid in the Inventory tab.
    /// Click a tile → Equip (PlayerEquipment auto-routes to the correct slot).
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class InventoryUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PanelRenderer document;
        [SerializeField] Inventory inventory;
        [SerializeField] PlayerEquipment equipment;

        VisualElement itemGrid;
        Label capacityLabel;
        Label emptyState;

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
            itemGrid = root.Q<VisualElement>("item-grid");
            capacityLabel = root.Q<Label>("items-capacity");
            emptyState = root.Q<Label>("empty-state");
            Refresh();
        }

        void SubscribeEvents()
        {
            if (GameEventsManager.instance == null) return;
            var bus = GameEventsManager.instance.inventoryEvents;
            bus.onItemAdded     += OnItemChanged;
            bus.onItemRemoved   += OnItemChanged;
            bus.onItemEquipped  += OnEquipChanged;
            bus.onItemUnequipped += OnEquipChanged;
        }

        void UnsubscribeEvents()
        {
            if (GameEventsManager.instance == null) return;
            var bus = GameEventsManager.instance.inventoryEvents;
            bus.onItemAdded     -= OnItemChanged;
            bus.onItemRemoved   -= OnItemChanged;
            bus.onItemEquipped  -= OnEquipChanged;
            bus.onItemUnequipped -= OnEquipChanged;
        }

        void OnItemChanged(InventoryItem _) => Refresh();
        void OnEquipChanged(InventoryItem _, EquipSlot __) => Refresh();

        void Refresh()
        {
            if (itemGrid == null || inventory == null) return;

            // Inventory tab shows ONLY consumables (potions, food, etc.)
            // Weapons and armor live in the bag but are managed on the Character tab.
            itemGrid.Clear();
            int shown = 0;
            foreach (var item in inventory.Items)
            {
                if (item.data is ConsumableData)
                {
                    itemGrid.Add(BuildItemTile(item));
                    shown++;
                }
            }

            if (capacityLabel != null)
                capacityLabel.text = $"{shown}/{inventory.Capacity}";
            if (emptyState != null)
                emptyState.style.display = shown == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        VisualElement BuildItemTile(InventoryItem item)
        {
            var tile = new VisualElement();
            tile.AddToClassList("item-tile");
            tile.AddToClassList($"item-tile--{item.data.rarity.ToString().ToLower()}");
            if (equipment != null && equipment.IsEquipped(item))
                tile.AddToClassList("item-tile--equipped");

            var icon = new VisualElement();
            icon.AddToClassList("item-tile-icon");
            if (item.data.icon != null)
                icon.style.backgroundImage = new StyleBackground(item.data.icon);
            tile.Add(icon);

            if (item.quantity > 1)
            {
                var qty = new Label($"x{item.quantity}");
                qty.AddToClassList("item-tile-quantity");
                tile.Add(qty);
            }

            tile.tooltip = item.data.displayName;
            tile.RegisterCallback<ClickEvent>(_ => OnTileClicked(item));
            return tile;
        }

        void OnTileClicked(InventoryItem item)
        {
            // Consumables: use one (heal etc.) and shrink the stack.
            // The inventory is a component on the Player, so its GameObject IS the user.
            if (item.data is ConsumableData consumable)
            {
                if (consumable.Use(inventory.gameObject))
                    inventory.Remove(item.data, 1);
                return;
            }

            // Anything else (shouldn't appear in this tab, but be safe): try to equip.
            equipment?.Equip(item);
        }
    }
}
