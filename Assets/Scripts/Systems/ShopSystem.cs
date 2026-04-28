using UnityEngine;
using System.Collections.Generic;

public class ShopSystem : MonoBehaviour
{
    public List<ItemData> itemStock = new List<ItemData>();
    public List<EquipmentData> equipmentStock = new List<EquipmentData>();
    public List<SkillData> skillStock = new List<SkillData>();

    public bool BuyItem(ItemData item, PlayerStats stats)
    {
        if (stats.gold < item.price) return false;
        if (!InventorySystem.Instance.AddItem(item)) return false;

        stats.gold -= item.price;
        return true;
    }

    public bool BuyEquipment(EquipmentData equipment, PlayerStats stats)
    {
        if (stats.gold < equipment.price) return false;

        stats.gold -= equipment.price;
        EquipmentSystem.Instance.Equip(equipment);
        return true;
    }

    public bool BuySkill(SkillData skill, PlayerStats stats)
    {
        if (stats.gold < skill.price) return false;
        if (!SkillSystem.Instance.LearnSkill(skill)) return false;

        stats.gold -= skill.price;
        return true;
    }
}
