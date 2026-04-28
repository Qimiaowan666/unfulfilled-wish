using UnityEngine;
using System;

public class EquipmentSystem : MonoBehaviour
{
    public static EquipmentSystem Instance { get; private set; }

    public EquipmentData weapon;
    public EquipmentData armor;
    public EquipmentData accessory;

    public event Action OnEquipmentChanged;

    PlayerStats stats;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => stats = FindAnyObjectByType<PlayerStats>();

    public void Equip(EquipmentData equipment)
    {
        Unequip(equipment.slot);

        switch (equipment.slot)
        {
            case EquipmentSlot.Weapon:    weapon = equipment; break;
            case EquipmentSlot.Armor:     armor = equipment; break;
            case EquipmentSlot.Accessory: accessory = equipment; break;
        }

        ApplyBonus(equipment, 1f);
        OnEquipmentChanged?.Invoke();
    }

    public void Unequip(EquipmentSlot slot)
    {
        EquipmentData current = slot switch
        {
            EquipmentSlot.Weapon    => weapon,
            EquipmentSlot.Armor     => armor,
            EquipmentSlot.Accessory => accessory,
            _ => null
        };

        if (current == null) return;
        ApplyBonus(current, -1f);

        switch (slot)
        {
            case EquipmentSlot.Weapon:    weapon = null; break;
            case EquipmentSlot.Armor:     armor = null; break;
            case EquipmentSlot.Accessory: accessory = null; break;
        }

        OnEquipmentChanged?.Invoke();
    }

    void ApplyBonus(EquipmentData eq, float sign)
    {
        if (stats == null) return;
        stats.attack    += eq.attackBonus    * sign;
        stats.defense   += eq.defenseBonus   * sign;
        stats.maxHP     += eq.maxHPBonus     * sign;
        stats.maxGhostHP += eq.maxGhostHPBonus * sign;
    }
}
