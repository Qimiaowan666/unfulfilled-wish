using UnityEngine;

// 突刺斩：朝玩家朝向位移 distance 单位，期间无敌 + Kinematic
// 一次性扫整条路径造成伤害；可选刀光 + 沿移动逐个生成的残影 VFX
public class Player_DashStrikeState : PlayerBaseState
{
    Vector2 startPos;
    Vector2 endPos;
    Skill_DashStrike skill;
    SpriteRenderer cachedPlayerSr;
    int afterimagesSpawned;

    public Player_DashStrikeState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isAttacking") { }   // 让 isAttacking=true 维持 animator 在挥剑 state

    public void Configure(Skill_DashStrike skill)
    {
        this.skill = skill;
    }

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
            var wallHit = Physics2D.BoxCast(startPos, boxSize, 0f, dir, skill.Distance, player.groundLayer);
            if (wallHit.collider != null)
            {
                float stopDist = Mathf.Max(0f, wallHit.distance - 0.05f);
                endPos = startPos + dir * stopDist;
            }
        }

        stateTimer        = skill.Duration;
        rb.bodyType       = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        player.Stats.SetInvulnerable(true);

        AudioManager.Instance?.PlayDash();

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
            var enemy = hit.GetComponent<EnemyBase>() ?? hit.GetComponentInParent<EnemyBase>();
            if (enemy == null) continue;
            enemy.TakeDamage(dmg);
            var fb = hit.GetComponent<DamageFeedback>() ?? hit.GetComponentInParent<DamageFeedback>();
            fb?.ApplyKnockback(player.transform.position, 4f);
        }

        // 月牙刀光：挂在玩家朝向一侧，跟着玩家移动
        if (skill.SlashEnabled)
        {
            var go = new GameObject("Vfx_SlashLine");
            go.AddComponent<Vfx_SlashLine>().Init(
                player.transform,
                new Vector2(skill.SlashOffset, 0f),
                skill.SlashHeight,
                skill.SlashBulge,
                player.FacingDir,
                skill.SlashColor, skill.SlashWidth, skill.SlashDuration);
        }

        // 残影缓存：在 Update 里按进度逐个生成
        cachedPlayerSr     = player.GetComponent<SpriteRenderer>();
        afterimagesSpawned = 0;
    }

    public override void Update()
    {
        base.Update();
        if (skill == null) return;

        float t = skill.Duration > 0f ? 1f - Mathf.Clamp01(stateTimer / skill.Duration) : 1f;
        player.transform.position = Vector2.Lerp(startPos, endPos, t);

        // 沿移动逐个生成残影（按 t 进度均匀触发）
        TrySpawnAfterimages(Mathf.FloorToInt(t * skill.AfterimageCount));

        if (stateTimer < 0f)
        {
            player.transform.position = endPos;
            TrySpawnAfterimages(skill.AfterimageCount); // 补齐
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
        }
    }

    void TrySpawnAfterimages(int target)
    {
        if (!skill.AfterimageEnabled || cachedPlayerSr == null || cachedPlayerSr.sprite == null) return;
        while (afterimagesSpawned < target && afterimagesSpawned < skill.AfterimageCount)
        {
            var ghost = new GameObject("Vfx_Afterimage");
            ghost.transform.position = player.transform.position;
            ghost.AddComponent<Vfx_Afterimage>()
                 .Init(cachedPlayerSr, skill.AfterimageTint, skill.AfterimageDuration);
            afterimagesSpawned++;
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
    }
}
