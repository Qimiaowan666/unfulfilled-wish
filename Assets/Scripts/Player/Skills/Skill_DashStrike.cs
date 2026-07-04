using UnityEngine;

// 突刺斩：朝玩家朝向瞬间位移一段距离，对路径上所有敌人造成伤害，期间无敌
public class Skill_DashStrike : Skill_Base
{
    [Header("DashStrike Settings")]
    [SerializeField] float   distance     = 5f;                          // 位移距离
    [SerializeField] float   duration     = 0.1f;                        // 位移用时（瞬间感）
    [SerializeField] Vector2 hitboxOffset = new Vector2(0.5f, 0f);       // 命中盒中心相对玩家的偏移（x 沿朝向）
    [SerializeField] Vector2 hitboxSize   = new Vector2(2f, 1.5f);       // 命中盒尺寸（x 宽 y 高）
    [SerializeField] bool    showHitbox   = true;                        // ⬛ 显示/隐藏 hitbox（编辑器 Gizmo + 运行时红框）

    [Header("Animation")]
    [SerializeField] string animStateName  = "attack2";  // 播哪个 animator state（攻击 / 自定义）；留空 = 不播
    [SerializeField] float  animSpeedScale = 4f;         // 播放倍速（让短时间内能看完整挥剑动作）

    // 给 state 读取的公开属性
    public float   Distance        => distance;
    public float   Duration        => duration;
    public Vector2 HitboxSize      => hitboxSize;
    public Vector2 HitboxOffset    => hitboxOffset;
    public float   DmgMul          => damageMultiplier;
    public string  AnimStateName   => animStateName;
    public float   AnimSpeedScale  => animSpeedScale;
    public bool    ShowHitbox      => showHitbox;

    public override void TryUseSkill()
    {
        if (!CanUseSkill()) return;
        if (player == null) return;

        player.dashStrikeState.skill = this;
        player.stateMachine.ChangeState(player.dashStrikeState);
        SetSkillOnCooldown();
        Debug.Log($"[Skill] DashStrike! 距离={distance}, 伤害倍率={damageMultiplier}");
    }

    // 编辑器预览：选中此组件时画初始攻击范围 = 玩家位置 + HitboxOffset，尺寸 = HitboxSize
    void OnDrawGizmosSelected()
    {
        if (!showHitbox) return;

        Transform origin = transform;
        var pc = GetComponentInParent<PlayerController>();
        if (pc != null) origin = pc.transform;

        int dir = 1;
        if (Application.isPlaying && pc != null && pc.FacingDir != 0) dir = pc.FacingDir;

        Vector3 center = origin.position + new Vector3(hitboxOffset.x * dir, hitboxOffset.y, 0f);
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        Gizmos.DrawWireCube(center, new Vector3(hitboxSize.x, hitboxSize.y, 0.05f));
    }
}
