using UnityEngine;

public enum SkillType { Active, Passive }

[CreateAssetMenu(fileName = "NewSkill", menuName = "Game/Skill")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public string description;
    public Sprite icon;
    public SkillType type;
    public int price;

    [Header("Active Skill")]
    public float cooldown;
    public float damage;
    public float manaCost;

    [Header("Passive Bonus")]
    public float attackPercent;
    public float defensePercent;
    public float perfectBlockWindowBonus;
}
