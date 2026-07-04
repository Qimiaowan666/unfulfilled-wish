// 技能类型枚举：每个技能一个值
// 添加新技能时：在此加一个枚举值 + 创建 Skill_X 继承 Skill_Base + 在 Player_SkillManager 注册
// 注：demo 已有 SkillType { Active, Passive }（商店分类用），故本枚举命名为 PlayerSkillType 区分
// Dash / Counter / Execute 留在 PlayerController 作为普通动作，不走技能系统
public enum PlayerSkillType
{
    DashStrike,    // 突刺斩：瞬间砍向前方，路径上敌人受伤，期间无敌
    Heal,          // 治疗：持续施法，期间不能动，受击打断
    // 后续新技能在此添加
}
