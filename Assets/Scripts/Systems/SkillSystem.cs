using UnityEngine;
using System.Collections.Generic;
using System;

public class SkillSystem : MonoBehaviour
{
    public static SkillSystem Instance { get; private set; }

    public List<SkillData> learnedSkills = new List<SkillData>();
    public event Action OnSkillsChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool LearnSkill(SkillData skill)
    {
        if (learnedSkills.Contains(skill)) return false;
        learnedSkills.Add(skill);
        ApplyPassives(skill);
        OnSkillsChanged?.Invoke();
        return true;
    }

    void ApplyPassives(SkillData skill)
    {
        if (skill.type != SkillType.Passive) return;
        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;

        stats.attack  *= 1f + skill.attackPercent  / 100f;
        stats.defense *= 1f + skill.defensePercent / 100f;

        var block = FindAnyObjectByType<PlayerBlock>();
        if (block != null && skill.perfectBlockWindowBonus > 0f)
            block.perfectBlockWindow += skill.perfectBlockWindowBonus;
    }
}
