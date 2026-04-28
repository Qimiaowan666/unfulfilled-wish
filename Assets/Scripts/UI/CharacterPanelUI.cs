using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CharacterPanelUI : MonoBehaviour
{
    public GameObject panel;
    public Text hpText;
    public Text attackText;
    public Text defenseText;
    public Text goldText;
    public Image weaponIcon;
    public Image armorIcon;
    public Image accessoryIcon;

    bool isOpen;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            Toggle();
    }

    void Toggle()
    {
        isOpen = !isOpen;
        panel.SetActive(isOpen);
        if (isOpen) Refresh();
    }

    void Refresh()
    {
        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;
        if (hpText)      hpText.text      = $"HP: {stats.CurrentHP:F0} / {stats.maxHP:F0}";
        if (attackText)  attackText.text  = $"ATK: {stats.attack:F0}";
        if (defenseText) defenseText.text = $"DEF: {stats.defense:F0}";
        if (goldText)    goldText.text    = $"Gold: {stats.gold}";

        var eq = EquipmentSystem.Instance;
        if (eq != null)
        {
            if (weaponIcon)    weaponIcon.sprite    = eq.weapon?.icon;
            if (armorIcon)     armorIcon.sprite     = eq.armor?.icon;
            if (accessoryIcon) accessoryIcon.sprite = eq.accessory?.icon;
        }
    }
}
