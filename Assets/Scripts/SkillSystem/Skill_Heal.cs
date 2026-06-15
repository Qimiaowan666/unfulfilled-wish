using UnityEngine;

// 治疗：持续施法 castDuration 秒，期间不能动，受击会被打断
// 完成后回 healAmount HP。特效走 VfxManager 的粒子预制体 "Vfx/HealAura"（见 Player_HealState）
public class Skill_Heal : Skill_Base
{
    [Header("Heal Settings")]
    [SerializeField] float healAmount   = 30f;     // 回血量
    [SerializeField] float castDuration = 1.5f;    // 持续施法时间（秒）

    [Header("Animation")]
    [SerializeField] string animStateName = "rest";  // 施法期间播的 animator state（留空 = 不播）

    [Header("VFX (HealAura 粒子)")]
    [SerializeField] bool    vfxEnabled     = true;
    [SerializeField] Color   vfxTint        = Color.white;             // 传给 HealAura 的调色
    [SerializeField] Vector2 vfxLocalOffset = new Vector2(0f, 0.45f);  // 相对玩家中心的偏移

    public float   HealAmount     => healAmount;
    public float   CastDuration   => castDuration;
    public string  AnimStateName  => animStateName;
    public bool    VfxEnabled     => vfxEnabled;
    public Color   VfxTint        => vfxTint;
    public Vector2 VfxLocalOffset => vfxLocalOffset;

    public override void TryUseSkill()
    {
        if (!CanUseSkill()) return;
        if (player == null) return;

        player.healState.Configure(this);
        player.stateMachine.ChangeState(player.healState);
        SetSkillOnCooldown();
        Debug.Log($"[Skill] Heal start - cast={castDuration}s, amount={healAmount}");
    }
}
