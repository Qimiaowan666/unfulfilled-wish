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

    // 常驻系统：玩家是 DontDestroyOnLoad 单例，gameplay 间切场景不重建；但经非游戏场景(主菜单)往返会重建。每次场景加载后兜底把被动加成重同步到当前玩家 Stats。
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
        var stats = PlayerController.Instance?.Stats;
        if (stats == null) return;

        GetPassiveBonusPercents(out float attackPercent, out float defensePercent);
        stats.SetSkillBonusPercent(attackPercent, defensePercent);
    }

}
