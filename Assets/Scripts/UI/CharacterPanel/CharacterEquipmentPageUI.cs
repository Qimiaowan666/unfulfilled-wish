using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class CharacterEquipmentPageUI : CharacterPanelPage
{
    CharacterPanelUIFactory ui;
    Transform slotsRoot;
    Transform listContent;

    public override void Build(Transform parent, CharacterPanelUIFactory uiFactory)
    {
        ui = uiFactory;
        Root = ui.CreateRoot("EquipmentPage", parent);

        var equippedLabel = ui.CreateText("EquippedLabel", Root.transform, "已装备", 20, TextAnchor.MiddleLeft);
        ui.SetRect(equippedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 0f));

        var slotsObject = ui.CreateUIObject("SlotsGrid", Root.transform);
        slotsRoot = slotsObject.transform;
        ui.SetRect(slotsObject.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -158f), new Vector2(0f, -40f));
        var slotGrid = slotsObject.AddComponent<GridLayoutGroup>();
        slotGrid.cellSize = new Vector2(154f, 52f);
        slotGrid.spacing = new Vector2(10f, 8f);
        slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        slotGrid.constraintCount = 2;
        slotGrid.childAlignment = TextAnchor.UpperLeft;

        var divider = ui.CreateDivider("Divider", Root.transform);
        ui.SetRect(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -174f), new Vector2(0f, -173f));

        var ownedLabel = ui.CreateText("OwnedLabel", Root.transform, "背包中的装备", 20, TextAnchor.MiddleLeft);
        ui.SetRect(ownedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -210f), new Vector2(0f, -182f));

        var scroll = ui.CreateScrollArea("OwnedEquipmentScroll", Root.transform, out listContent);
        ui.SetRect(scroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -222f));
        ui.ConfigureVerticalContent(listContent, 8f, 0f);
    }

    public override void Refresh()
    {
        var equipment = EquipmentSystem.Instance;
        ui.ClearChildren(slotsRoot);
        ui.ClearChildren(listContent);

        if (equipment == null)
        {
            CreateEmptyMessage(listContent, "未找到装备系统。");
            return;
        }

        BuildEquippedSlot("武器", equipment.weapon, EquipmentSlot.Weapon);
        BuildEquippedSlot("防具", equipment.armor, EquipmentSlot.Armor);
        BuildEquippedSlot("饰品 1", equipment.accessory1, EquipmentSlot.Accessory);
        BuildEquippedSlot("饰品 2", equipment.accessory2, EquipmentSlot.Accessory);

        if (equipment.ownedEquipment.Count == 0)
        {
            CreateEmptyMessage(listContent, "暂无拥有装备。");
            return;
        }

        foreach (var item in equipment.ownedEquipment)
        {
            if (item == null) continue;
            BuildEquipmentRow(equipment, item);
        }
    }

    void BuildEquippedSlot(string slotName, EquipmentData equipment, EquipmentSlot fallbackSlot)
    {
        var root = ui.CreatePanel(slotName + "Slot", slotsRoot, ui.PanelAltColor);
        var rect = root.rectTransform;
        ui.SetFixedHeight(rect, 52f);

        var iconBlock = ui.CreateIconBlock("Icon", root.transform, new Vector2(40f, 40f));
        var iconRect = iconBlock.RootImage.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(6f, 0f);
        var slot = equipment != null ? equipment.slot : fallbackSlot;
        ui.SetIcon(iconBlock, equipment != null ? equipment.icon : null, ui.GetEquipmentColor(slot), ui.GetEquipmentPlaceholder(slot));

        var label = ui.CreateText("Label", root.transform, slotName, 14, TextAnchor.UpperLeft);
        label.color = ui.MutedTextColor;
        ui.SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(52f, -4f), new Vector2(-6f, -4f));

        var name = ui.CreateText("Name", root.transform, equipment != null ? equipment.equipmentName : "未装备", 15, TextAnchor.LowerLeft);
        ui.SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(52f, 4f), new Vector2(-6f, 2f));
    }

    void BuildEquipmentRow(EquipmentSystem system, EquipmentData equipment)
    {
        var row = ui.CreatePanel(equipment.equipmentName + "Row", listContent, ui.PanelAltColor);
        ui.SetFixedHeight(row.rectTransform, 58f);

        var iconBlock = ui.CreateIconBlock("Icon", row.transform, new Vector2(42f, 42f));
        var iconRect = iconBlock.RootImage.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(8f, 0f);
        ui.SetIcon(iconBlock, equipment.icon, ui.GetEquipmentColor(equipment.slot), ui.GetEquipmentPlaceholder(equipment.slot));

        var name = ui.CreateText("Name", row.transform, equipment.equipmentName, 17, TextAnchor.UpperLeft);
        ui.SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(58f, -6f), new Vector2(-100f, -4f));

        var stats = ui.CreateText("Stats", row.transform, FormatEquipmentStats(equipment), 14, TextAnchor.LowerLeft);
        stats.color = ui.MutedTextColor;
        ui.SetRect(stats.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(58f, 4f), new Vector2(-100f, 2f));

        bool equipped = IsEquipped(system, equipment);
        var button = ui.CreateButton("ActionButton", row.transform, equipped ? "卸下" : "装备", 15);
        var buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-8f, 0f);
        buttonRect.sizeDelta = new Vector2(82f, 34f);

        var captured = equipment;
        button.onClick.AddListener(() =>
        {
            if (IsEquipped(system, captured))
                Unequip(system, captured);
            else
                system.EquipOwned(captured);

            Refresh();
        });
    }

    void CreateEmptyMessage(Transform parent, string message)
    {
        var text = ui.CreateText("EmptyMessage", parent, message, 18, TextAnchor.MiddleCenter);
        ui.SetFixedHeight(text.rectTransform, 48f);
        text.color = ui.MutedTextColor;
    }

    static bool IsEquipped(EquipmentSystem system, EquipmentData equipment)
    {
        return system.weapon == equipment
            || system.armor == equipment
            || system.accessory1 == equipment
            || system.accessory2 == equipment;
    }

    static void Unequip(EquipmentSystem system, EquipmentData equipment)
    {
        if (equipment.slot == EquipmentSlot.Accessory)
        {
            if (system.accessory1 == equipment) system.UnequipAccessory(1);
            else if (system.accessory2 == equipment) system.UnequipAccessory(2);
            return;
        }

        system.Unequip(equipment.slot);
    }

    static string FormatEquipmentStats(EquipmentData equipment)
    {
        var sb = new StringBuilder();
        AppendStat(sb, "ATK", equipment.attackBonus);
        AppendStat(sb, "DEF", equipment.defenseBonus);
        AppendStat(sb, "HP", equipment.maxHPBonus);
        return sb.Length > 0 ? sb.ToString() : "无属性加成";
    }

    static void AppendStat(StringBuilder sb, string label, float value)
    {
        if (Mathf.Approximately(value, 0f)) return;
        if (sb.Length > 0) sb.Append("  ");
        sb.Append(label);
        sb.Append(value > 0f ? "+" : "");
        sb.Append(value.ToString("F0"));
    }
}
