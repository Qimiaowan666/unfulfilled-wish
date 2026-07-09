using UnityEngine;

// 突刺斩：朝玩家朝向位移 distance 单位，期间无敌 + Kinematic
// 一次性扫整条路径造成伤害；伴随能量收拢 VFX
public class Player_DashStrikeState : PlayerBaseState
{
    Vector2 startPos;
    Vector2 endPos;
    public Skill_DashStrike skill;   // 由 Skill_DashStrike 在 ChangeState 前直接设
    GameObject dashVfx;   // 能量收拢 VFX 句柄（每帧驱动位置随冲刺前进，Exit 回收）
    static readonly RaycastHit2D[] s_wallHits = new RaycastHit2D[8];   // 撞墙检测复用缓冲，避免每次冲刺分配

    // 能量 VFX 的目标位置：玩家前方一点
    Vector3 DashVfxPos() => player.transform.position + new Vector3(player.FacingDir * 1.8f, 0.3f, 0f);

    public Player_DashStrikeState(PlayerController player, StateMachine sm)
        : base(player, sm, "isAttacking") { }   // 让 isAttacking=true 维持 animator 在挥剑 state

    public override void Enter()
    {
        base.Enter();
        if (skill == null) { stateMachine.ChangeState(player.idleState); return; }

        startPos = player.transform.position;
        endPos   = startPos + new Vector2(player.FacingDir * skill.Distance, 0f);

        // 撞墙检测：用玩家碰撞体沿冲刺方向 BoxCast，撞到 groundLayer 就把终点缩到撞击点前
        var col = player.GetComponent<Collider2D>();
        if (col != null)
        {
            Vector2 boxSize = col.bounds.size * 0.9f;   // 略缩，避免边缘擦碰误判
            Vector2 dir     = new Vector2(player.FacingDir, 0f);
            // 撞墙检测只认实心墙：用 ContactFilter2D.useTriggers=false 按调用作用域排除触发体，
            // 否则会撞到"已开门"残留的交互触发碰撞体（isTrigger 且在 Ground 层，开门只关了实心 blockingCollider）→ 冲刺卡门口。
            // 比临时改全局 Physics2D.queriesHitTriggers 干净：不污染全局状态、过滤随调用走。
            var filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.SetLayerMask(player.groundLayer);
            int n = Physics2D.BoxCast(startPos, boxSize, 0f, dir, filter, s_wallHits, skill.Distance);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (s_wallHits[i].distance < nearest) nearest = s_wallHits[i].distance;   // 取最近实心墙(不依赖缓冲是否按距离排序)
            if (nearest < float.MaxValue)
            {
                float stopDist = Mathf.Max(0f, nearest - 0.05f);
                endPos = startPos + dir * stopDist;
            }
        }

        stateTimer        = skill.Duration;
        rb.bodyType       = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        player.Stats.SetInvulnerable(true);

        AudioManager.Instance?.PlayDashStrike();
        // 突进能量 VFX：向心收拢光线 + 中心闪光，跟在玩家前方随冲刺前进（Update 每帧驱动位置）
        dashVfx = VfxManager.PlayLoop("Vfx/DashStrike", null, DashVfxPos(), 1f,
                                      new Color(0.7f, 0.9f, 1f), player.MainSprite);

        // 强制播挥剑动画 + 调倍速
        if (!string.IsNullOrEmpty(skill.AnimStateName) && anim != null)
        {
            anim.Play(skill.AnimStateName, 0, 0f);
            anim.speed = skill.AnimSpeedScale > 0f ? skill.AnimSpeedScale : 1f;
        }

        // 单 hitbox：玩家起点 + HitboxOffset（沿朝向），尺寸 = HitboxSize
        Vector2 center = startPos + new Vector2(skill.HitboxOffset.x * player.FacingDir, skill.HitboxOffset.y);
        Vector2 size   = skill.HitboxSize;
        var hits = Physics2D.OverlapBoxAll(center, size, 0f, player.enemyLayer);
        float dmg = player.Stats.attack * skill.DmgMul;
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null) continue;
            enemy.TakeDamage(dmg);
            var fb = hit.GetComponent<DamageFeedback>();
            fb?.ApplyKnockback(player.transform.position, 4f);
        }
    }

    public override void Update()
    {
        base.Update();
        if (skill == null) return;

        float t = skill.Duration > 0f ? 1f - Mathf.Clamp01(stateTimer / skill.Duration) : 1f;
        player.transform.position = Vector2.Lerp(startPos, endPos, t);
        if (dashVfx != null) dashVfx.transform.position = DashVfxPos();   // 能量收拢点跟着冲刺前进

        if (stateTimer < 0f)
        {
            player.transform.position = endPos;
            stateMachine.ChangeState(player.GroundedOrFall);
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 强制 Dynamic（避免触发时玩家已是 Kinematic 导致还原后浮空）
        rb.bodyType       = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        player.Stats.SetInvulnerable(false);
        if (anim != null) anim.speed = 1f;
        if (dashVfx != null) { VfxManager.StopLoop(dashVfx); dashVfx = null; }   // 回收能量 VFX
    }
}
