using UnityEngine;
using System.Collections.Generic;
using System;

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance { get; private set; }

    public int maxSlots = 20;
    public List<ItemData> items = new List<ItemData>();

    public event Action OnInventoryChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool AddItem(ItemData item)
    {
        if (items.Count >= maxSlots) return false;
        items.Add(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void RemoveItem(ItemData item)
    {
        items.Remove(item);
        OnInventoryChanged?.Invoke();
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= items.Count) return;
        if (toIndex < 0 || toIndex >= items.Count) return;
        if (fromIndex == toIndex) return;

        ItemData item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);
        OnInventoryChanged?.Invoke();
    }

    public void DiscardItem(ItemData item)
    {
        RemoveItem(item);
    }

    public void UseItem(ItemData item, PlayerStats stats)
    {
        if (!items.Contains(item)) return;
        if (stats == null) return;

        if (item.healAmount > 0)
            stats.TakeDamage(-item.healAmount);

        if (item.attackBonus != 0f)
            stats.attack += item.attackBonus;

        if (item.defenseBonus != 0f)
            stats.defense += item.defenseBonus;

        if (item.maxHPBonus != 0f)
        {
            stats.maxHP += item.maxHPBonus;
            if (item.maxHPBonus > 0f)
                stats.TakeDamage(-item.maxHPBonus);
        }

        RemoveItem(item);
    }
}
