using System.Collections;
using UnityEngine;

// 恶魔武士：多招(atk1/2/3 近战 + jumpattack 突进)，HP 降到 50% 吼叫变身 → 火焰形态(换 _flame 动画 + 攻防速强化)。
public class DemonSamuraiEnemy : GroundEnemy
{
    [Header("变身 (50% HP → 火焰形态)")]
    [Range(0f, 1f)] public float transformHpThreshold = 0.5f;
    public float attackBuff   = 1.4f;   // 变身后攻击 ×
    public float speedBuff    = 1.3f;   // 变身后移动速度 ×
    public float shoutFreeze  = 0.7f;   // 变身吼叫定身最小时长(实际取与 shout 动画时长的较大者，保证 shout 播完)

    bool transformed;
    bool transforming;

    public override string MoveClip => "run";
    public override string HurtClip => "hurt";

    // 变身后：有火焰变体的动作自动加 _flame 后缀；death/defend/shout 没有火焰版，保持原样。
    static readonly string[] FlameVariants = { "idle", "run", "hurt", "atk1", "atk2", "atk3", "jumpattack" };
    protected override string ResolveClip(string baseName)
    {
        if (!transformed || string.IsNullOrEmpty(baseName)) return baseName;
        foreach (var v in FlameVariants) if (v == baseName) return baseName + "_flame";
        return baseName;
    }

    public override void TakeDamage(float damage, float poiseDamage)
    {
        base.TakeDamage(damage, poiseDamage);
        if (!transformed && !transforming && CurrentHP > 0f && CurrentHP <= maxHP * transformHpThreshold)
            StartCoroutine(TransformRoutine());
    }

    IEnumerator TransformRoutine()
    {
        transforming = true;
        Invincible = true;                                   // 变身吼叫期间不可被打断
        if (Rb != null) Rb.linearVelocity = Vector2.zero;
        PlayClip("shout");                                   // 吼叫(无火焰版)
        AudioManager.Instance?.PlayBossRoar();               // 复用吼叫音
        yield return new WaitForSeconds(Mathf.Max(shoutFreeze, ClipLength("shout")));   // 等 shout 整段播完

        transformed      = true;                             // 之后所有动作走火焰版
        attack          *= attackBuff;
        patrolMoveSpeed *= speedBuff;
        battleMoveSpeed *= speedBuff;
        Invincible       = false;
        transforming     = false;
        // 干净地回到追击/待机(火焰形态)
        stateMachine.ChangeState(player != null ? (EnemyBaseState)chaseState : idleState);
    }

    protected override void Update()
    {
        if (transforming)   // 变身定身：清水平速度，不跑状态机(让 shout 完整播)
        {
            if (Rb != null) Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
            return;
        }
        base.Update();
    }

    // 复活/读档：退回基础形态并撤销强化
    protected override void ResetForm()
    {
        if (transformed)
        {
            attack          /= attackBuff;
            patrolMoveSpeed /= speedBuff;
            battleMoveSpeed /= speedBuff;
        }
        transformed  = false;
        transforming = false;
    }
}
