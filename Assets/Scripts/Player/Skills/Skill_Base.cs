using UnityEngine;

// 技能基类：所有技能（Skill_DashStrike / Skill_Heal 等）继承这个
// 挂载：Player 下 "Skills" 子节点（挂 Player_SkillManager）→ 每个技能一个 Skill_X 组件
public class Skill_Base : MonoBehaviour
{
    public PlayerController player { get; private set; }

    [Header("General details")]
    [SerializeField] protected PlayerSkillType skillType;
    [SerializeField] protected Sprite          icon;                    // 技能图标（UI 显示）
    [SerializeField] protected float           cooldown         = 1f;
    [SerializeField] protected float           damageMultiplier = 1f;
    [SerializeField] protected float           staminaCost      = 0f;   // 0 = 不消耗

    float lastTimeUsed;

    protected virtual void Awake()
    {
        player       = GetComponentInParent<PlayerController>();
        lastTimeUsed = -cooldown;   // 进游戏时可立即使用
    }

    // 子类 override 具体效果（动画 / 位移 / 状态切换），末尾调 SetSkillOnCooldown()
    public virtual void TryUseSkill()
    {
        if (!CanUseSkill()) return;
    }

    public virtual bool CanUseSkill()
    {
        if (OnCooldown())
        {
            Debug.Log($"[Skill] {skillType} 冷却中");
            return false;
        }
        if (staminaCost > 0f && player != null && player.Stats != null && !player.Stats.HasStamina(staminaCost))
        {
            Debug.Log($"[Skill] {skillType} 体力不足（需要 {staminaCost}）");
            return false;
        }
        return true;
    }

    public PlayerSkillType GetSkillType() => skillType;
    public Sprite GetIcon()            => icon;
    public float GetCooldown()         => cooldown;
    public float GetStaminaCost()      => staminaCost;

    bool OnCooldown() => Time.time < lastTimeUsed + cooldown;
    protected void SetSkillOnCooldown()
    {
        lastTimeUsed = Time.time;
        if (staminaCost > 0f) player?.Stats?.SpendStamina(staminaCost);
    }
}
