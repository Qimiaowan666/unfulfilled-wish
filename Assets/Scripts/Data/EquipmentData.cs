using UnityEngine;

public enum EquipmentSlot { Weapon, Armor, Accessory }

[CreateAssetMenu(fileName = "NewEquipment", menuName = "Game/Equipment")]
public class EquipmentData : ScriptableObject
{
    [Header("Save")]
    public string saveID;

    public string equipmentName;
    public string description;
    public Sprite icon;
    public EquipmentSlot slot;
    public int price;

    [Header("Stat Bonuses")]
    public float attackBonus;
    public float defenseBonus;
    public float maxHPBonus;
}
