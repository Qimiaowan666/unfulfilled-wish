using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 背包页 View（挂在 prefab 的背包页节点）。布局画好，这里只填数据。
// 中栏网格 Instantiate ItemSlot.prefab；右栏用 DetailPanelView。
public class InventoryPageView : CharacterPageView
{
    [Header("中栏网格")]
    public Transform gridContainer;       // GridLayoutGroup 容器
    public GameObject itemSlotPrefab;     // ItemSlot.prefab
    [Header("右栏详情")]
    public DetailPanelView detail;

    [Header("状态(合并:立绘下五行属性, 同 StatRow 格式)")]
    public Transform statsContainer;
    public GameObject statRowPrefab;

    [Header("分页")]
    public Button prevButton;
    public Button nextButton;
    public TMP_Text pageLabel;
    public int pageSize = 30;

    ItemData selected;
    int currentPage;

    public override void Refresh()
    {
        RefreshStatus();
        WirePager();
        var inv = InventorySystem.Instance;
        ClearChildren(gridContainer);
        if (inv == null) { detail?.ShowEmpty("背包", "未找到背包系统。"); UpdatePager(1, 1); return; }

        var stacks = BuildStacks(inv);
        if (selected != null && !stacks.Exists(s => s.item == selected)) selected = null;
        if (selected == null && stacks.Count > 0) selected = stacks[0].item;

        int size = Mathf.Max(1, pageSize);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(inv.maxSlots / (float)size));
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
        int start = currentPage * size;

        // 每页固定渲染 size 个格子；超出物品数的格子显示空凹槽
        for (int i = 0; i < size; i++)
        {
            if (itemSlotPrefab == null) break;
            var go = Instantiate(itemSlotPrefab, gridContainer);
            var slot = go.GetComponent<ItemSlotView>();
            if (slot == null) continue;

            int idx = start + i;
            if (idx < stacks.Count)
            {
                var captured = stacks[idx].item;
                slot.Setup(stacks[idx].item.icon, stacks[idx].count, stacks[idx].item == selected,
                           () => { selected = captured; Refresh(); });
            }
            else
            {
                slot.Setup(null, 0, false, null); // 空凹槽
            }
        }

        UpdatePager(currentPage + 1, pageCount);
        RefreshDetail();
    }

    void WirePager()
    {
        if (prevButton != null) { prevButton.onClick.RemoveAllListeners(); prevButton.onClick.AddListener(PrevPage); }
        if (nextButton != null) { nextButton.onClick.RemoveAllListeners(); nextButton.onClick.AddListener(NextPage); }
    }

    void UpdatePager(int page, int total)
    {
        if (pageLabel != null) pageLabel.text = $"{page} / {total}";
        if (prevButton != null) prevButton.interactable = currentPage > 0;
        if (nextButton != null) nextButton.interactable = currentPage < total - 1;
    }

    void PrevPage() { currentPage = Mathf.Max(0, currentPage - 1); Refresh(); }
    void NextPage() { currentPage++; Refresh(); }

    void RefreshStatus()
    {
        if (statsContainer == null || statRowPrefab == null) return;
        ClearChildren(statsContainer);
        var stats = Object.FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;
        AddStat("血量", $"{stats.CurrentHP:F0} / {stats.maxHP:F0}");
        AddStat("体力", $"{stats.CurrentStamina:F0} / {stats.maxStamina:F0}");
        AddStat("攻击", $"{stats.attack:F0}");
        AddStat("防御", $"{stats.defense:F0}");
        AddStat("金钱", $"{stats.gold}");
    }

    void AddStat(string label, string value)
    {
        var go = Instantiate(statRowPrefab, statsContainer);
        var row = go.GetComponent<StatRowView>();
        if (row != null) row.Setup(label, value);
    }

    void RefreshDetail()
    {
        if (detail == null) return;
        if (selected == null) { detail.ShowEmpty("背包", "选择一个道具查看详情。"); return; }

        var item = selected;
        detail.SetHeader(item.itemName, string.IsNullOrWhiteSpace(item.description) ? "" : item.description, item.icon);
        detail.ClearStats();
        if (item.healAmount > 0f) detail.AddStat("回复生命", $"+{item.healAmount:F0}");
        if (!Mathf.Approximately(item.attackBonus, 0f))  detail.AddStat("攻击", $"{(item.attackBonus > 0 ? "+" : "")}{item.attackBonus:F0}");
        if (!Mathf.Approximately(item.defenseBonus, 0f)) detail.AddStat("防御", $"{(item.defenseBonus > 0 ? "+" : "")}{item.defenseBonus:F0}");
        if (!Mathf.Approximately(item.maxHPBonus, 0f))   detail.AddStat("最大生命", $"{(item.maxHPBonus > 0 ? "+" : "")}{item.maxHPBonus:F0}");

        bool canUse = item.healAmount > 0f || item.attackBonus != 0f || item.defenseBonus != 0f || item.maxHPBonus != 0f;
        var target = item;
        detail.SetAction("使用", canUse, () =>
        {
            var stats = Object.FindAnyObjectByType<PlayerStats>();
            InventorySystem.Instance?.UseItem(target, stats);
            selected = null;
            Refresh();
        });
    }

    struct Stack { public ItemData item; public int count; }

    static List<Stack> BuildStacks(InventorySystem inv)
    {
        var stacks = new List<Stack>();
        foreach (var it in inv.items)
        {
            if (it == null) continue;
            int idx = stacks.FindIndex(s => s.item == it);
            if (idx >= 0) { var s = stacks[idx]; s.count++; stacks[idx] = s; }
            else stacks.Add(new Stack { item = it, count = 1 });
        }
        return stacks;
    }
}
