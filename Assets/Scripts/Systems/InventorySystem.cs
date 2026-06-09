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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
            stats.ApplyStatBonus(item.attackBonus, 0f, 0f);

        if (item.defenseBonus != 0f)
            stats.ApplyStatBonus(0f, item.defenseBonus, 0f);

        if (item.maxHPBonus != 0f)
            stats.ApplyStatBonus(0f, 0f, item.maxHPBonus, true);

        RemoveItem(item);
    }

    public void LoadItems(IEnumerable<ItemData> savedItems)
    {
        items.Clear();
        if (savedItems != null)
        {
            foreach (var item in savedItems)
            {
                if (item != null && items.Count < maxSlots)
                    items.Add(item);
            }
        }

        OnInventoryChanged?.Invoke();
    }
}
