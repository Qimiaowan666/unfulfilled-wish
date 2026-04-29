using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;

public class EquipmentSystem : MonoBehaviour
{
    public static EquipmentSystem Instance { get; private set; }

    public EquipmentData weapon;
    public EquipmentData armor;
    [FormerlySerializedAs("accessory")]
    public EquipmentData accessory1;
    public EquipmentData accessory2;
    public List<EquipmentData> ownedEquipment = new List<EquipmentData>();

    public event Action OnEquipmentChanged;

    PlayerStats stats;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
        RegisterEquippedAsOwned();
        RebuildEquippedCombatBonuses();
    }

    public bool HasEquipment(EquipmentData equipment)
    {
        return equipment != null && ownedEquipment.Contains(equipment);
    }

    public bool AddEquipment(EquipmentData equipment, bool equipNow = true)
    {
        if (equipment == null) return false;

        if (!ownedEquipment.Contains(equipment))
            ownedEquipment.Add(equipment);

        if (equipNow) Equip(equipment);
        else OnEquipmentChanged?.Invoke();

        return true;
    }

    public void EquipOwned(EquipmentData equipment)
    {
        if (equipment == null) return;
        if (!ownedEquipment.Contains(equipment))
            ownedEquipment.Add(equipment);

        Equip(equipment);
    }

    public void Equip(EquipmentData equipment)
    {
        if (equipment == null) return;

        if (equipment.slot == EquipmentSlot.Accessory)
        {
            EquipAccessory(equipment);
            return;
        }

        if (equipment.slot == EquipmentSlot.Weapon && weapon == equipment) return;
        if (equipment.slot == EquipmentSlot.Armor && armor == equipment) return;

        Unequip(equipment.slot);

        switch (equipment.slot)
        {
            case EquipmentSlot.Weapon:
                weapon = equipment;
                break;
            case EquipmentSlot.Armor:
                armor = equipment;
                break;
        }

        ApplyBonus(equipment, 1f);
        OnEquipmentChanged?.Invoke();
    }

    public void LoadEquipment(IEnumerable<EquipmentData> savedOwnedEquipment, EquipmentData savedWeapon, EquipmentData savedArmor, EquipmentData savedAccessory1, EquipmentData savedAccessory2)
    {
        weapon = null;
        armor = null;
        accessory1 = null;
        accessory2 = null;
        ownedEquipment.Clear();

        var targetStats = ResolveStats();
        if (targetStats != null)
            targetStats.SetEquipmentBonuses(0f, 0f);

        if (savedOwnedEquipment != null)
        {
            foreach (var equipment in savedOwnedEquipment)
            {
                if (equipment != null && !ownedEquipment.Contains(equipment))
                    ownedEquipment.Add(equipment);
            }
        }

        EquipLoaded(savedWeapon, ref weapon);
        EquipLoaded(savedArmor, ref armor);
        EquipLoaded(savedAccessory1, ref accessory1);
        EquipLoaded(savedAccessory2, ref accessory2);

        OnEquipmentChanged?.Invoke();
    }

    public float GetEquippedMaxHPBonus()
    {
        float total = 0f;
        if (weapon != null) total += weapon.maxHPBonus;
        if (armor != null) total += armor.maxHPBonus;
        if (accessory1 != null) total += accessory1.maxHPBonus;
        if (accessory2 != null) total += accessory2.maxHPBonus;
        return total;
    }

    void EquipAccessory(EquipmentData equipment)
    {
        if (accessory1 == equipment || accessory2 == equipment) return;

        if (accessory1 == null)
        {
            accessory1 = equipment;
            ApplyBonus(equipment, 1f);
            OnEquipmentChanged?.Invoke();
            return;
        }

        if (accessory2 == null)
        {
            accessory2 = equipment;
            ApplyBonus(equipment, 1f);
            OnEquipmentChanged?.Invoke();
            return;
        }

        ApplyBonus(accessory1, -1f);
        accessory1 = equipment;
        ApplyBonus(accessory1, 1f);
        OnEquipmentChanged?.Invoke();
    }

    public void Unequip(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon:
                UnequipWeapon();
                break;
            case EquipmentSlot.Armor:
                UnequipArmor();
                break;
            case EquipmentSlot.Accessory:
                UnequipAccessory(1);
                break;
        }
    }

    public void UnequipAccessory(int index)
    {
        EquipmentData current = index == 1 ? accessory1 : accessory2;
        if (current == null) return;

        ApplyBonus(current, -1f);
        if (index == 1) accessory1 = null;
        else accessory2 = null;
        OnEquipmentChanged?.Invoke();
    }

    void UnequipWeapon()
    {
        if (weapon == null) return;
        ApplyBonus(weapon, -1f);
        weapon = null;
        OnEquipmentChanged?.Invoke();
    }

    void UnequipArmor()
    {
        if (armor == null) return;
        ApplyBonus(armor, -1f);
        armor = null;
        OnEquipmentChanged?.Invoke();
    }

    void ApplyBonus(EquipmentData eq, float sign)
    {
        if (eq == null) return;

        var targetStats = ResolveStats();
        if (targetStats == null) return;

        targetStats.ApplyEquipmentBonus(eq.attackBonus * sign, eq.defenseBonus * sign);
        if (!Mathf.Approximately(eq.maxHPBonus, 0f))
            targetStats.ApplyStatBonus(0f, 0f, eq.maxHPBonus * sign);
    }

    PlayerStats ResolveStats()
    {
        if (stats == null)
            stats = FindAnyObjectByType<PlayerStats>();

        return stats;
    }

    void RegisterEquippedAsOwned()
    {
        if (weapon != null && !ownedEquipment.Contains(weapon)) ownedEquipment.Add(weapon);
        if (armor != null && !ownedEquipment.Contains(armor)) ownedEquipment.Add(armor);
        if (accessory1 != null && !ownedEquipment.Contains(accessory1)) ownedEquipment.Add(accessory1);
        if (accessory2 != null && !ownedEquipment.Contains(accessory2)) ownedEquipment.Add(accessory2);
    }

    void RebuildEquippedCombatBonuses()
    {
        var targetStats = ResolveStats();
        if (targetStats == null) return;

        float attackBonus = 0f;
        float defenseBonus = 0f;
        AppendCombatBonus(weapon, ref attackBonus, ref defenseBonus);
        AppendCombatBonus(armor, ref attackBonus, ref defenseBonus);
        AppendCombatBonus(accessory1, ref attackBonus, ref defenseBonus);
        AppendCombatBonus(accessory2, ref attackBonus, ref defenseBonus);
        targetStats.SetEquipmentBonuses(attackBonus, defenseBonus);
    }

    static void AppendCombatBonus(EquipmentData equipment, ref float attackBonus, ref float defenseBonus)
    {
        if (equipment == null) return;

        attackBonus += equipment.attackBonus;
        defenseBonus += equipment.defenseBonus;
    }

    void EquipLoaded(EquipmentData equipment, ref EquipmentData slot)
    {
        if (equipment == null) return;

        slot = equipment;
        if (!ownedEquipment.Contains(equipment))
            ownedEquipment.Add(equipment);

        ApplyBonus(equipment, 1f);
    }
}
