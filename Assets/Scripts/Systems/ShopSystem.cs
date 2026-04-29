using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ShopItemEntry
{
    public ItemData item;
    public int quantity = -1;

    public bool IsAvailable => item != null && quantity != 0;
    public bool IsLimited => quantity > -1;
}

[Serializable]
public class ShopEquipmentEntry
{
    public EquipmentData equipment;
    public int quantity = 1;

    public bool IsAvailable => equipment != null && quantity != 0;
    public bool IsLimited => quantity > -1;
}

[Serializable]
public class ShopSkillEntry
{
    public SkillData skill;
    public int quantity = 1;

    public bool IsAvailable => skill != null && quantity != 0;
    public bool IsLimited => quantity > -1;
}

public class ShopSystem : MonoBehaviour
{
    [Header("Save")]
    public string shopID;

    [Header("Legacy Stock")]
    public List<ItemData> itemStock = new List<ItemData>();
    public List<EquipmentData> equipmentStock = new List<EquipmentData>();
    public List<SkillData> skillStock = new List<SkillData>();

    [Header("Runtime Stock")]
    public List<ShopItemEntry> itemEntries = new List<ShopItemEntry>();
    public List<ShopEquipmentEntry> equipmentEntries = new List<ShopEquipmentEntry>();
    public List<ShopSkillEntry> skillEntries = new List<ShopSkillEntry>();
    public string SaveID => SaveIdUtility.GetSceneObjectID(this, shopID);

    public IEnumerable<ShopItemEntry> AvailableItems
    {
        get
        {
            EnsureEntriesFromLegacyLists();
            foreach (var entry in itemEntries)
                if (entry != null && entry.IsAvailable)
                    yield return entry;
        }
    }

    public IEnumerable<ShopEquipmentEntry> AvailableEquipment
    {
        get
        {
            EnsureEntriesFromLegacyLists();
            foreach (var entry in equipmentEntries)
                if (entry != null && entry.IsAvailable)
                    yield return entry;
        }
    }

    public IEnumerable<ShopSkillEntry> AvailableSkills
    {
        get
        {
            EnsureEntriesFromLegacyLists();
            var skillSystem = SkillSystem.GetOrCreate();
            foreach (var entry in skillEntries)
            {
                if (entry == null || !entry.IsAvailable) continue;
                if (skillSystem.HasSkill(entry.skill)) continue;
                yield return entry;
            }
        }
    }

    public bool BuyItem(ShopItemEntry entry, PlayerStats stats)
    {
        if (entry == null || !entry.IsAvailable || stats == null) return false;
        if (InventorySystem.Instance == null) return false;
        if (stats.gold < entry.item.price) return false;
        if (!InventorySystem.Instance.AddItem(entry.item)) return false;

        stats.AddGold(-entry.item.price);
        Consume(entry);
        return true;
    }

    public bool BuyEquipment(ShopEquipmentEntry entry, PlayerStats stats)
    {
        if (entry == null || !entry.IsAvailable || stats == null) return false;

        var equipmentSystem = EquipmentSystem.Instance;
        if (equipmentSystem == null) return false;

        if (equipmentSystem.HasEquipment(entry.equipment))
        {
            equipmentSystem.EquipOwned(entry.equipment);
            Consume(entry);
            return true;
        }

        if (stats.gold < entry.equipment.price) return false;
        stats.AddGold(-entry.equipment.price);
        equipmentSystem.AddEquipment(entry.equipment, true);
        Consume(entry);
        return true;
    }

    public bool BuySkill(ShopSkillEntry entry, PlayerStats stats)
    {
        if (entry == null || !entry.IsAvailable || stats == null) return false;

        var skillSystem = SkillSystem.GetOrCreate();
        if (skillSystem.HasSkill(entry.skill))
        {
            Consume(entry);
            return true;
        }

        if (stats.gold < entry.skill.price) return false;
        if (!skillSystem.LearnSkill(entry.skill)) return false;

        stats.AddGold(-entry.skill.price);
        Consume(entry);
        return true;
    }

    public bool BuyItem(ItemData item, PlayerStats stats)
    {
        return BuyItem(FindOrCreateEntry(item), stats);
    }

    public bool BuyEquipment(EquipmentData equipment, PlayerStats stats)
    {
        return BuyEquipment(FindOrCreateEntry(equipment), stats);
    }

    public bool BuySkill(SkillData skill, PlayerStats stats)
    {
        return BuySkill(FindOrCreateEntry(skill), stats);
    }

    public string FormatPrice(int price, int quantity)
    {
        return quantity > -1 ? $"{price}G x{quantity}" : $"{price}G";
    }

    public ShopSaveData CaptureSaveData()
    {
        EnsureEntriesFromLegacyLists();
        return new ShopSaveData
        {
            id = SaveID,
            itemEntries = CaptureItemEntries(),
            equipmentEntries = CaptureEquipmentEntries(),
            skillEntries = CaptureSkillEntries()
        };
    }

    public void LoadSaveData(ShopSaveData data)
    {
        if (data == null) return;

        EnsureEntriesFromLegacyLists();
        ApplyItemEntryQuantities(data.itemEntries);
        ApplyEquipmentEntryQuantities(data.equipmentEntries);
        ApplySkillEntryQuantities(data.skillEntries);
    }

    void Consume(ShopItemEntry entry)
    {
        if (entry.quantity > 0) entry.quantity--;
    }

    void Consume(ShopEquipmentEntry entry)
    {
        if (entry.quantity > 0) entry.quantity--;
    }

    void Consume(ShopSkillEntry entry)
    {
        if (entry.quantity > 0) entry.quantity--;
    }

    ShopItemEntry FindOrCreateEntry(ItemData item)
    {
        EnsureEntriesFromLegacyLists();
        var entry = itemEntries.Find(candidate => candidate != null && candidate.item == item);
        if (entry != null) return entry;

        entry = new ShopItemEntry { item = item, quantity = -1 };
        itemEntries.Add(entry);
        return entry;
    }

    ShopEquipmentEntry FindOrCreateEntry(EquipmentData equipment)
    {
        EnsureEntriesFromLegacyLists();
        var entry = equipmentEntries.Find(candidate => candidate != null && candidate.equipment == equipment);
        if (entry != null) return entry;

        entry = new ShopEquipmentEntry { equipment = equipment, quantity = 1 };
        equipmentEntries.Add(entry);
        return entry;
    }

    ShopSkillEntry FindOrCreateEntry(SkillData skill)
    {
        EnsureEntriesFromLegacyLists();
        var entry = skillEntries.Find(candidate => candidate != null && candidate.skill == skill);
        if (entry != null) return entry;

        entry = new ShopSkillEntry { skill = skill, quantity = 1 };
        skillEntries.Add(entry);
        return entry;
    }

    void EnsureEntriesFromLegacyLists()
    {
        if (itemEntries.Count == 0)
        {
            foreach (var item in itemStock)
                if (item != null)
                    itemEntries.Add(new ShopItemEntry { item = item, quantity = -1 });
        }

        if (equipmentEntries.Count == 0)
        {
            foreach (var equipment in equipmentStock)
                if (equipment != null)
                    equipmentEntries.Add(new ShopEquipmentEntry { equipment = equipment, quantity = 1 });
        }

        if (skillEntries.Count == 0)
        {
            foreach (var skill in skillStock)
                if (skill != null)
                    skillEntries.Add(new ShopSkillEntry { skill = skill, quantity = 1 });
        }
    }

    ShopStockEntrySaveData[] CaptureItemEntries()
    {
        var result = new List<ShopStockEntrySaveData>();
        foreach (var entry in itemEntries)
        {
            if (entry?.item == null) continue;
            result.Add(new ShopStockEntrySaveData
            {
                assetID = SaveIdUtility.GetAssetID(entry.item),
                quantity = entry.quantity
            });
        }

        return result.ToArray();
    }

    ShopStockEntrySaveData[] CaptureEquipmentEntries()
    {
        var result = new List<ShopStockEntrySaveData>();
        foreach (var entry in equipmentEntries)
        {
            if (entry?.equipment == null) continue;
            result.Add(new ShopStockEntrySaveData
            {
                assetID = SaveIdUtility.GetAssetID(entry.equipment),
                quantity = entry.quantity
            });
        }

        return result.ToArray();
    }

    ShopStockEntrySaveData[] CaptureSkillEntries()
    {
        var result = new List<ShopStockEntrySaveData>();
        foreach (var entry in skillEntries)
        {
            if (entry?.skill == null) continue;
            result.Add(new ShopStockEntrySaveData
            {
                assetID = SaveIdUtility.GetAssetID(entry.skill),
                quantity = entry.quantity
            });
        }

        return result.ToArray();
    }

    void ApplyItemEntryQuantities(ShopStockEntrySaveData[] savedEntries)
    {
        if (savedEntries == null) return;

        foreach (var savedEntry in savedEntries)
        {
            if (savedEntry == null) continue;
            var entry = itemEntries.Find(candidate => candidate?.item != null && SaveIdUtility.MatchesAssetID(candidate.item, savedEntry.assetID));
            if (entry != null) entry.quantity = savedEntry.quantity;
        }
    }

    void ApplyEquipmentEntryQuantities(ShopStockEntrySaveData[] savedEntries)
    {
        if (savedEntries == null) return;

        foreach (var savedEntry in savedEntries)
        {
            if (savedEntry == null) continue;
            var entry = equipmentEntries.Find(candidate => candidate?.equipment != null && SaveIdUtility.MatchesAssetID(candidate.equipment, savedEntry.assetID));
            if (entry != null) entry.quantity = savedEntry.quantity;
        }
    }

    void ApplySkillEntryQuantities(ShopStockEntrySaveData[] savedEntries)
    {
        if (savedEntries == null) return;

        foreach (var savedEntry in savedEntries)
        {
            if (savedEntry == null) continue;
            var entry = skillEntries.Find(candidate => candidate?.skill != null && SaveIdUtility.MatchesAssetID(candidate.skill, savedEntry.assetID));
            if (entry != null) entry.quantity = savedEntry.quantity;
        }
    }
}
