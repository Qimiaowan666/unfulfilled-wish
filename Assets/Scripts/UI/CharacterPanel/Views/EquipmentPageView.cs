using System.Collections.Generic;
using UnityEngine;

// 装备页 View：左栏 4 槽位，中栏拥有列表，右栏详情 + 同槽属性对比 + 装备/卸下。
public class EquipmentPageView : CharacterPageView
{
    [Header("左栏四装备格(顺序：武器/防具/饰品1/饰品2)")]
    public ItemSlotView[] equipSlots;
    [Header("中栏拥有列表 容器 + 行 prefab")]
    public Transform listContainer;
    public GameObject listItemPrefab;    // ListItem.prefab
    [Header("右栏详情")]
    public DetailPanelView detail;

    [Header("左栏属性(生命/体力/攻击/防御, 选中未装备项时 ▲▼ 换装预览)")]
    public Transform statsContainer;
    public GameObject statRowPrefab;   // StatRow.prefab

    EquipmentData selected;

    public override void Refresh()
    {
        var eq = EquipmentSystem.Instance;
        ClearChildren(listContainer);
        if (eq == null) { detail?.ShowEmpty("装备", "未找到装备系统。"); ClearEquipSlots(); return; }

        if (selected != null && !eq.ownedEquipment.Contains(selected) && !IsEquipped(eq, selected)) selected = null;

        RefreshEquipSlots(eq);
        RefreshStats(eq);

        foreach (var e in eq.ownedEquipment)
        {
            if (e == null) continue;
            var captured = e;
            AddItem(listContainer, e.equipmentName, FormatStats(e), e.icon, e == selected, () => { selected = captured; Refresh(); });
        }

        RefreshDetail(eq);
    }

    // 左栏四个装备格：武器/防具/饰品1/饰品2，显示当前穿戴图标，点击选中已装备项（右栏可卸下）
    void RefreshEquipSlots(EquipmentSystem eq)
    {
        if (equipSlots == null) return;
        EquipmentData[] worn = { eq.weapon, eq.armor, eq.accessory1, eq.accessory2 };
        for (int i = 0; i < equipSlots.Length && i < worn.Length; i++)
        {
            var slot = equipSlots[i];
            if (slot == null) continue;
            var e = worn[i];
            var captured = e;
            slot.Setup(e != null ? e.icon : null, 0, e != null && e == selected,
                       e != null ? (System.Action)(() => { selected = captured; Refresh(); }) : null);
        }
    }

    void ClearEquipSlots()
    {
        if (equipSlots == null) return;
        foreach (var s in equipSlots) if (s != null) s.Setup(null, 0, false, null);
    }

    // 左栏属性：生命/体力/攻击/防御。选中“未装备”的装备时，攻防血显示换装预览（当前 -> 装备后 ▲▼）
    void RefreshStats(EquipmentSystem eq)
    {
        if (statsContainer == null || statRowPrefab == null) return;
        ClearChildren(statsContainer);
        var stats = Object.FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;

        float dAtk = 0f, dDef = 0f, dHP = 0f;
        if (selected != null && !IsEquipped(eq, selected))
        {
            var cur = GetEquippedInSlot(eq, selected);
            dAtk = selected.attackBonus  - (cur != null ? cur.attackBonus  : 0f);
            dDef = selected.defenseBonus - (cur != null ? cur.defenseBonus : 0f);
            dHP  = selected.maxHPBonus   - (cur != null ? cur.maxHPBonus   : 0f);
        }

        AddStatRow("生命", stats.maxHP, dHP);
        AddStatRow("体力", stats.maxStamina, 0f);
        AddStatRow("攻击", stats.attack, dAtk);
        AddStatRow("防御", stats.defense, dDef);
    }

    void AddStatRow(string label, float val, float delta)
    {
        var go = Instantiate(statRowPrefab, statsContainer);
        var row = go.GetComponent<StatRowView>();
        if (row == null) return;
        string v = val.ToString("F0");
        if (!Mathf.Approximately(delta, 0f))
        {
            string hex = delta > 0 ? "7ad97f" : "e57373";
            string arr = delta > 0 ? "▲" : "▼";
            v = $"{val:F0} -> {(val + delta):F0}  <color=#{hex}>{arr}{Mathf.Abs(delta):F0}</color>";
        }
        row.Setup(label, v);
    }

    void AddItem(Transform c, string title, string sub, Sprite icon, bool sel, System.Action onClick)
    {
        if (listItemPrefab == null || c == null) return;
        var go = Instantiate(listItemPrefab, c);
        var item = go.GetComponent<ListItemView>();
        if (item != null) item.Setup(title, sub, icon, sel, onClick);
    }

    void RefreshDetail(EquipmentSystem eq)
    {
        if (detail == null) return;
        if (selected == null) { detail.ShowEmpty("装备", "选择一件装备查看详情。"); return; }

        // 右栏：只显示装备描述(风味) + 属性数值；换装对比已放左栏
        detail.SetHeader(selected.equipmentName, selected.description, selected.icon);
        bool isEq = IsEquipped(eq, selected);

        detail.ClearStats();
        if (!Mathf.Approximately(selected.attackBonus, 0f))  detail.AddStat("攻击", FmtBonus(selected.attackBonus));
        if (!Mathf.Approximately(selected.defenseBonus, 0f)) detail.AddStat("防御", FmtBonus(selected.defenseBonus));
        if (!Mathf.Approximately(selected.maxHPBonus, 0f))   detail.AddStat("生命", FmtBonus(selected.maxHPBonus));

        var target = selected; var sys = eq;
        detail.SetAction(isEq ? "卸下" : "装备", true, () =>
        {
            if (IsEquipped(sys, target)) Unequip(sys, target);
            else sys.EquipOwned(target);
            Refresh();
        });
    }

    static string FmtBonus(float v) => (v >= 0 ? "+" : "") + v.ToString("F0");

    static string FormatStats(EquipmentData e)
    {
        var p = new List<string>();
        if (!Mathf.Approximately(e.attackBonus, 0f))  p.Add($"ATK{(e.attackBonus > 0 ? "+" : "")}{e.attackBonus:F0}");
        if (!Mathf.Approximately(e.defenseBonus, 0f)) p.Add($"DEF{(e.defenseBonus > 0 ? "+" : "")}{e.defenseBonus:F0}");
        if (!Mathf.Approximately(e.maxHPBonus, 0f))   p.Add($"HP{(e.maxHPBonus > 0 ? "+" : "")}{e.maxHPBonus:F0}");
        return p.Count > 0 ? string.Join("  ", p) : "无加成";
    }

    static string SlotName(EquipmentSlot s)
    {
        switch (s) { case EquipmentSlot.Weapon: return "武器"; case EquipmentSlot.Armor: return "防具"; case EquipmentSlot.Accessory: return "饰品"; default: return ""; }
    }

    static EquipmentData GetEquippedInSlot(EquipmentSystem sys, EquipmentData e)
    {
        switch (e.slot)
        {
            case EquipmentSlot.Weapon: return sys.weapon;
            case EquipmentSlot.Armor: return sys.armor;
            case EquipmentSlot.Accessory: return sys.accessory1;
            default: return null;
        }
    }

    static bool IsEquipped(EquipmentSystem sys, EquipmentData e) =>
        sys.weapon == e || sys.armor == e || sys.accessory1 == e || sys.accessory2 == e;

    static void Unequip(EquipmentSystem sys, EquipmentData e)
    {
        if (e.slot == EquipmentSlot.Accessory)
        {
            if (sys.accessory1 == e) sys.UnequipAccessory(1);
            else if (sys.accessory2 == e) sys.UnequipAccessory(2);
            return;
        }
        sys.Unequip(e.slot);
    }
}
