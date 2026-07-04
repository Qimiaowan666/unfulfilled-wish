using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;

public class SkillSystem : MonoBehaviour
{
    public static SkillSystem Instance { get; private set; }

    public List<SkillData> learnedSkills = new List<SkillData>();
    public event Action OnSkillsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        ReapplyPassives();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 常驻系统：切场景后玩家重建，重新把被动加成应用到新玩家
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReapplyPassives();
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
        ReapplyPassives();
        OnSkillsChanged?.Invoke();
        return true;
    }

    public void LoadSkills(IEnumerable<SkillData> savedSkills)
    {
        learnedSkills.Clear();
        if (savedSkills != null)
        {
            foreach (var skill in savedSkills)
            {
                if (skill != null && !learnedSkills.Contains(skill))
                    learnedSkills.Add(skill);
            }
        }

        ReapplyPassives();
        OnSkillsChanged?.Invoke();
    }

    public void GetPassiveBonusPercents(out float attackPercent, out float defensePercent)
    {
        attackPercent = 0f;
        defensePercent = 0f;

        foreach (var skill in learnedSkills)
        {
            if (skill == null || skill.type != SkillType.Passive) continue;
            attackPercent += skill.attackPercent;
            defensePercent += skill.defensePercent;
        }
    }

    void ReapplyPassives()
    {
        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;

        GetPassiveBonusPercents(out float attackPercent, out float defensePercent);
        stats.SetSkillBonusPercent(attackPercent, defensePercent);
    }

}
