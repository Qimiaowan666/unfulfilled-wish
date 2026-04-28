using UnityEngine;

public enum ItemType { Consumable, Passive }

[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public string description;
    public Sprite icon;
    public ItemType type;
    public int price;

    [Header("Consumable Effect")]
    public float healAmount;
    public float ghostHPRestore;

    [Header("Passive Effect")]
    public float attackBonus;
    public float defenseBonus;
    public float maxHPBonus;
}
