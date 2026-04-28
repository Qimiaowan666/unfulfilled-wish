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
        if (Instance != null)
        {
            foreach (var skill in learnedSkills)
            {
                if (skill != null && !Instance.learnedSkills.Contains(skill))
                    Instance.learnedSkills.Add(skill);
            }

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static SkillSystem GetOrCreate()
    {
        if (Instance != null) return Instance;

        Instance = FindAnyObjectByType<SkillSystem>();
        if (Instance != null) return Instance;

        var go = new GameObject("SkillSystem");
        return go.AddComponent<SkillSystem>();
    }

    public bool HasSkill(SkillData skill)
    {
        return skill != null && learnedSkills.Contains(skill);
    }

    public bool LearnSkill(SkillData skill)
    {
        if (skill == null) return false;
        if (learnedSkills.Contains(skill)) return true;

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
    }

    public bool ApplyActiveSkill(SkillData skill, EnemyBase target)
    {
        if (skill == null || target == null) return false;
        if (skill.type != SkillType.Active) return false;
        if (!learnedSkills.Contains(skill)) return false;

        target.TakeDamage(skill.damage, skill.poiseDamage);
        return true;
    }
}
