using System.Collections.Generic;
using UnityEngine;

// 装备页 View：左栏 4 槽位，中栏拥有列表，右栏详情。
// 装/卸全走右键：列表右键已装备→卸下 / 未装备武器防具→直接装 / 未装备饰品→进入选槽再左键点槽；左栏槽右键→卸下。
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
    bool placing;   // true=右键饰品后进入“选槽”模式，左栏饰品槽高亮等左键点选；左键选中/其它操作都会清掉
    int hoverSlot;  // 选槽模式下鼠标悬停的饰品槽(1/2)，0=没悬停；换装预览按这个目标槽算

    // 切走/关面板时清掉选中与选槽态，避免再开时残留高亮
    public override void Hide() { selected = null; placing = false; hoverSlot = 0; base.Hide(); }

    public override void Refresh()
    {
        var eq = EquipmentSystem.Instance;
        ClearChildren(listContainer);
        if (eq == null) { detail?.ShowEmpty("装备", "未找到装备系统。"); ClearEquipSlots(); return; }

        if (selected != null && !eq.ownedEquipment.Contains(selected) && !IsEquipped(eq, selected)) selected = null;
        if (selected == null) placing = false;

        RefreshEquipSlots(eq);
        RefreshStats(eq);

        foreach (var e in eq.ownedEquipment)
        {
            if (e == null) continue;
            var captured = e; var sys = eq;
            AddItem(listContainer, e.equipmentName, FormatStats(e), e.icon, e == selected,
                () => { AudioManager.Instance?.PlayUIClick(); selected = captured; placing = false; Refresh(); },   // 左键=仅选中看详情，不装备
                () =>   // 右键：已装备→卸下；未装备武器/防具→直接装；未装备饰品→进入选槽
                {
                    if (IsEquipped(sys, captured)) { AudioManager.Instance?.PlayUIEquip(); UnequipItem(sys, captured); placing = false; }
                    else if (captured.slot != EquipmentSlot.Accessory) { AudioManager.Instance?.PlayUIEquip(); sys.Equip(captured); placing = false; }
                    else { AudioManager.Instance?.PlayUIClick(); selected = captured; placing = true; hoverSlot = 0; }   // 饰品→进入选槽模式
                    Refresh();
                });
        }

        RefreshDetail();
    }

    // 左栏四个装备格：武器/防具/饰品1/饰品2，显示当前穿戴图标，点击选中已装备项（右栏可卸下）
    static readonly EquipmentSlot[] SlotKinds = { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Accessory, EquipmentSlot.Accessory };

    void RefreshEquipSlots(EquipmentSystem eq)
    {
        if (equipSlots == null) return;
        EquipmentData[] worn = { eq.weapon, eq.armor, eq.accessory1, eq.accessory2 };

        // 仅当“右键饰品进入选槽模式”时，两个饰品槽才作为放入目标高亮。
        // 左键选中不进入；武器/防具不进入。
        bool placingAccessory = placing && selected != null && selected.slot == EquipmentSlot.Accessory && !IsEquipped(eq, selected);

        for (int i = 0; i < equipSlots.Length && i < worn.Length; i++)
        {
            var slot = equipSlots[i];
            if (slot == null) continue;
            var e = worn[i];
            var captured = e;
            bool isAccessorySlot = SlotKinds[i] == EquipmentSlot.Accessory;

            if (placingAccessory && isAccessorySlot)
            {
                int accIndex = i == 2 ? 1 : 2; var sys = eq; var target = selected;
                slot.Setup(e != null ? e.icon : null, 0, true,   // 高亮=可放入
                    () => { AudioManager.Instance?.PlayUIEquip(); sys.EquipAccessoryToSlot(target, accIndex); selected = target; placing = false; Refresh(); });
                slot.SetHover(() => { hoverSlot = accIndex; RefreshStats(sys); }, () => { hoverSlot = 0; RefreshStats(sys); });   // 悬停→预览按该槽算
            }
            else
            {
                int si = i; var sys = eq;
                // 仅饰品槽在“选中的正是它穿的那件饰品”时高亮；武器/防具一律不亮
                bool hl = isAccessorySlot && e != null && e == selected;
                slot.Setup(e != null ? e.icon : null, 0, hl,
                    e != null ? (System.Action)(() => { AudioManager.Instance?.PlayUIClick(); selected = captured; placing = false; Refresh(); }) : null,
                    e != null ? (System.Action)(() => { AudioManager.Instance?.PlayUIEquip(); UnequipSlot(sys, si); Refresh(); }) : null);   // 右键卸下
            }
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
        if (selected != null && !IsEquipped(eq, selected) && TryGetSwapTarget(eq, out var cur))
        {
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

    void AddItem(Transform c, string title, string sub, Sprite icon, bool sel, System.Action onClick, System.Action onRightClick = null)
    {
        if (listItemPrefab == null || c == null) return;
        var go = Instantiate(listItemPrefab, c);
        var item = go.GetComponent<ListItemView>();
        if (item != null) item.Setup(title, sub, icon, sel, onClick, onRightClick);
    }

    void RefreshDetail()
    {
        if (detail == null) return;
        if (selected == null) { detail.ShowEmpty("装备", "选择一件装备查看详情。"); return; }

        // 右栏：装备描述(风味) + 属性数值；操作提示是右栏底部的独立文本(prefab 里)
        detail.SetHeader(selected.equipmentName, selected.description, selected.icon);

        detail.ClearStats();
        if (!Mathf.Approximately(selected.attackBonus, 0f))  detail.AddStat("攻击", FmtBonus(selected.attackBonus));
        if (!Mathf.Approximately(selected.defenseBonus, 0f)) detail.AddStat("防御", FmtBonus(selected.defenseBonus));
        if (!Mathf.Approximately(selected.maxHPBonus, 0f))   detail.AddStat("生命", FmtBonus(selected.maxHPBonus));

        // 右栏不再有任何按钮：装备/卸下全部走右键（武器防具右键直接装；饰品右键进入选槽；已装备右键卸下）
        detail.HideAction();
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

    // 换装预览要对比的"当前装备"。返回 false=暂不显示差值(饰品两槽都满又没悬停时，等你悬停某个槽)。
    bool TryGetSwapTarget(EquipmentSystem eq, out EquipmentData cur)
    {
        cur = null;
        if (selected.slot == EquipmentSlot.Weapon) { cur = eq.weapon; return true; }   // 武器：单槽
        if (selected.slot == EquipmentSlot.Armor)  { cur = eq.armor;  return true; }   // 防具：单槽
        if (placing && hoverSlot == 1) { cur = eq.accessory1; return true; }   // 饰品：悬停槽1
        if (placing && hoverSlot == 2) { cur = eq.accessory2; return true; }   // 饰品：悬停槽2
        if (eq.accessory1 == null || eq.accessory2 == null) { cur = null; return true; }   // 有空槽→纯增加
        return false;   // 两槽都满又没悬停 → 先不显示差值
    }

    static bool IsEquipped(EquipmentSystem sys, EquipmentData e) =>
        sys.weapon == e || sys.armor == e || sys.accessory1 == e || sys.accessory2 == e;

    // 右键卸下：按左栏槽位索引(0武器/1防具/2饰品1/3饰品2)卸下
    static void UnequipSlot(EquipmentSystem sys, int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: sys.Unequip(EquipmentSlot.Weapon); break;
            case 1: sys.Unequip(EquipmentSlot.Armor); break;
            case 2: sys.UnequipAccessory(1); break;
            case 3: sys.UnequipAccessory(2); break;
        }
    }

    // 右键卸下：按装备项找到它当前所在的槽再卸（列表里右键已装备项用）
    static void UnequipItem(EquipmentSystem sys, EquipmentData e)
    {
        if (e == sys.weapon) sys.Unequip(EquipmentSlot.Weapon);
        else if (e == sys.armor) sys.Unequip(EquipmentSlot.Armor);
        else if (e == sys.accessory1) sys.UnequipAccessory(1);
        else if (e == sys.accessory2) sys.UnequipAccessory(2);
    }
}
