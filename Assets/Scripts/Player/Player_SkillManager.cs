using UnityEngine;

// 技能注册中心：挂在 Player 的 "Skills" 子节点上
// Awake 自动收集子物体下所有 Skill_Base 组件
// 添加新技能：1) Skills 节点下加新 Skill_X 组件  2) 在 PlayerSkillType 枚举加值  3) Skill_X 继承 Skill_Base
public class Player_SkillManager : MonoBehaviour
{
    public Skill_Base[] allSkills { get; private set; }

    void Awake()
    {
        allSkills = GetComponentsInChildren<Skill_Base>();
    }

    public Skill_Base GetSkillByType(PlayerSkillType type)
    {
        foreach (var s in allSkills)
            if (s.GetSkillType() == type) return s;
        return null;
    }

    public bool TryUseSkill(PlayerSkillType type)
    {
        var skill = GetSkillByType(type);
        if (skill == null || !skill.CanUseSkill()) return false;
        skill.TryUseSkill();
        return true;
    }
}
