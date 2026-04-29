using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class CharacterInventoryPageUI : CharacterPanelPage
{
    CharacterPanelUIFactory ui;
    Transform gridContent;
    Text headerText;
    Text detailText;
    Button useButton;
    ItemData selectedItem;

    public override void Build(Transform parent, CharacterPanelUIFactory uiFactory)
    {
        ui = uiFactory;
        Root = ui.CreateRoot("InventoryPage", parent);

        headerText = ui.CreateText("Header", Root.transform, "背包", 20, TextAnchor.MiddleLeft);
        ui.SetRect(headerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 0f));

        var scroll = ui.CreateScrollArea("InventoryScroll", Root.transform, out gridContent);
        ui.SetRect(scroll.GetComponent<RectTransform>(), new Vector2(0f, 0.36f), Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -40f));
        ui.ConfigureGridContent(gridContent, new Vector2(76f, 76f), new Vector2(8f, 8f), 4);

        var divider = ui.CreateDivider("Divider", Root.transform);
        ui.SetRect(divider.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 0.34f), Vector2.zero, new Vector2(0f, 1f));

        var detailPanel = ui.CreatePanel("DetailPanel", Root.transform, ui.PanelAltColor);
        ui.SetRect(detailPanel.rectTransform, Vector2.zero, new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);

        detailText = ui.CreateText("DetailText", detailPanel.transform, "选择一个道具查看详情。", 17, TextAnchor.UpperLeft);
        detailText.lineSpacing = 1.15f;
        ui.SetRect(detailText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 46f), new Vector2(-112f, -12f));

        useButton = ui.CreateButton("UseButton", detailPanel.transform, "使用", 16);
        var buttonRect = useButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-14f, 14f);
        buttonRect.sizeDelta = new Vector2(88f, 36f);
        useButton.onClick.AddListener(UseSelectedItem);
    }

    public override void Refresh()
    {
        var inventory = InventorySystem.Instance;
        ui.ClearChildren(gridContent);

        if (inventory == null)
        {
            if (headerText != null) headerText.text = "背包";
            if (detailText != null) detailText.text = "未找到背包系统。";
            ui.SetButtonInteractable(useButton, false);
            return;
        }

        List<ItemStack> stacks = BuildStacks(inventory);

        if (selectedItem != null && !HasStack(stacks, selectedItem))
            selectedItem = null;

        if (selectedItem == null && stacks.Count > 0)
            selectedItem = stacks[0].Item;

        if (headerText != null)
            headerText.text = $"背包  {inventory.items.Count} / {inventory.maxSlots}";

        int slotCount = Mathf.Max(inventory.maxSlots, stacks.Count);
        for (int i = 0; i < slotCount; i++)
        {
            if (i < stacks.Count)
                BuildSlot(stacks[i].Item, stacks[i].Count);
            else
                BuildSlot(null, 0);
        }

        RefreshDetail();
    }

    void BuildSlot(ItemData item, int quantity)
    {
        bool selected = item != null && item == selectedItem;
        var slot = ui.CreatePanel(item != null ? item.itemName + "Slot" : "EmptySlot", gridContent,
            selected ? ui.ButtonSelectedColor : item != null ? ui.PanelAltColor : new Color(0.10f, 0.11f, 0.14f, 0.62f));

        var button = slot.gameObject.AddComponent<Button>();
        button.interactable = item != null;

        var iconBlock = ui.CreateIconBlock("Icon", slot.transform, new Vector2(46f, 46f));
        var iconRect = iconBlock.RootImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -6f);

        if (item != null)
            ui.SetIcon(iconBlock, item.icon, ui.GetItemColor(item), ui.GetItemPlaceholder(item));
        else
            ui.SetIcon(iconBlock, null, new Color(0.16f, 0.17f, 0.20f, 0.65f), "");

        var name = ui.CreateText("Name", slot.transform, item != null ? item.itemName : "", 11, TextAnchor.MiddleCenter);
        name.color = item != null ? ui.TextColor : ui.MutedTextColor;
        ui.SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 2f), new Vector2(-4f, 22f));

        if (item != null && quantity > 1)
            BuildQuantityBadge(slot.transform, quantity);

        if (item != null)
        {
            var captured = item;
            button.onClick.AddListener(() =>
            {
                selectedItem = captured;
                Refresh();
            });
        }
    }

    void RefreshDetail()
    {
        if (selectedItem == null)
        {
            if (detailText != null) detailText.text = "选择一个道具查看详情。";
            ui.SetButtonInteractable(useButton, false);
            return;
        }

        if (detailText != null) detailText.text = BuildItemDetail(selectedItem);
        ui.SetButtonInteractable(useButton, CanUseItem(selectedItem));
    }

    string BuildItemDetail(ItemData item)
    {
        var sb = new StringBuilder();
        sb.AppendLine(item.itemName);

        int count = GetItemCount(InventorySystem.Instance, item);
        if (count > 1)
            sb.AppendLine($"数量: {count}");

        if (!string.IsNullOrWhiteSpace(item.description))
            sb.AppendLine(item.description);

        if (item.healAmount > 0f) sb.AppendLine($"回复 HP: {item.healAmount:F0}");
        if (item.attackBonus != 0f) sb.AppendLine($"攻击: {FormatSigned(item.attackBonus)}");
        if (item.defenseBonus != 0f) sb.AppendLine($"防御: {FormatSigned(item.defenseBonus)}");
        if (item.maxHPBonus != 0f) sb.AppendLine($"最大 HP: {FormatSigned(item.maxHPBonus)}");
        return sb.ToString();
    }

    void UseSelectedItem()
    {
        if (!CanUseItem(selectedItem)) return;

        var stats = Object.FindAnyObjectByType<PlayerStats>();
        InventorySystem.Instance?.UseItem(selectedItem, stats);
        selectedItem = null;
        Refresh();
    }

    void BuildQuantityBadge(Transform parent, int quantity)
    {
        var badge = ui.CreatePanel("QuantityBadge", parent, new Color(0.04f, 0.05f, 0.07f, 0.92f));
        var badgeRect = badge.rectTransform;
        badgeRect.anchorMin = new Vector2(1f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(-4f, 4f);
        badgeRect.sizeDelta = new Vector2(30f, 18f);

        var countText = ui.CreateText("Text", badge.transform, quantity.ToString(), 11, TextAnchor.MiddleCenter);
        countText.fontStyle = FontStyle.Bold;
        countText.horizontalOverflow = HorizontalWrapMode.Overflow;
        countText.verticalOverflow = VerticalWrapMode.Overflow;
        ui.SetRect(countText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static bool CanUseItem(ItemData item)
    {
        if (item == null) return false;

        return item.healAmount > 0f
            || item.attackBonus != 0f
            || item.defenseBonus != 0f
            || item.maxHPBonus != 0f;
    }

    static int GetItemCount(InventorySystem inventory, ItemData item)
    {
        if (inventory == null || item == null) return 0;

        int count = 0;
        foreach (var ownedItem in inventory.items)
        {
            if (ownedItem == item)
                count++;
        }

        return count;
    }

    static List<ItemStack> BuildStacks(InventorySystem inventory)
    {
        var stacks = new List<ItemStack>();
        if (inventory == null) return stacks;

        foreach (var item in inventory.items)
        {
            if (item == null) continue;

            int index = FindStackIndex(stacks, item);
            if (index >= 0)
            {
                ItemStack stack = stacks[index];
                stack.Count++;
                stacks[index] = stack;
            }
            else
            {
                stacks.Add(new ItemStack(item, 1));
            }
        }

        return stacks;
    }

    static bool HasStack(List<ItemStack> stacks, ItemData item)
    {
        return FindStackIndex(stacks, item) >= 0;
    }

    static int FindStackIndex(List<ItemStack> stacks, ItemData item)
    {
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].Item == item)
                return i;
        }

        return -1;
    }

    static string FormatSigned(float value)
    {
        return value > 0f ? $"+{value:F0}" : value.ToString("F0");
    }

    struct ItemStack
    {
        public ItemData Item;
        public int Count;

        public ItemStack(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}
