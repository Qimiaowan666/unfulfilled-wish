using System.Collections;
using UnityEngine;
using System;

[RequireComponent(typeof(PoiseMeter))]
public class EnemyBase : Entity
{
    [Header("Save")]
    public string saveID;
    public bool permanentDeath;

    [Header("Boss")]
    public bool   isBoss;                // 标记为关卡 boss → 常驻 LevelManager 自动接管 BGM / 胜利检测；血条/名牌按此识别
    public string nextSceneOnDefeat;     // 击败后切到的场景（留空 = 不切场景）
    public BossProfile profile;          // 名牌/血条展示数据（名字、罗马名、单字图、配色）
    [Tooltip("false = boss 沉睡，不主动进战；由 BossIntroTrigger 登场后 Activate() 唤醒。普通敌人保持 true")]
    public bool   combatEnabled = true;
    public virtual void Activate() => combatEnabled = true;

    // boss 战进行中：任意 boss 已被唤醒(combatEnabled)且存活 → 此期间禁止存档(火堆/暂停菜单)
    public static bool AnyBossInCombat()
    {
        foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e.isBoss && e.combatEnabled && e.CurrentHP > 0f) return true;
        return false;
    }

    [Header("Stats")]
    public float maxHP    = 50f;
    public float attack   = 8f;
    public int   goldDrop = 10;

    // 命中判定层;运行时 Awake 自动设为 Player 层,不在 Inspector 显示
    [HideInInspector] public LayerMask playerLayer;

    [Header("Attack")]
    public float   attackCooldown        = 1.5f;

    [Header("Special Attack Hit Reaction")]
    public float   specialHitStunDuration = 0.6f;   // 玩家被特殊攻击命中后的硬直时长
    public float   specialHitKnockback    = 8f;     // 击退强度（普通攻击是 5f）

    [System.Serializable]
    public class AttackHitbox
    {
        public Vector2 offset     = new Vector2(0.5f, 0f);
        public Vector2 size       = new Vector2(0.8f, 0.6f);
        public Color   gizmoColor = new Color(1f, 0.3f, 0f, 0.4f);
        public bool    showGizmo  = true;
    }

    public virtual AttackHitbox GetHitbox(string id) => null;
    public AttackHitbox GetHitbox(System.Enum key) => GetHitbox(key.ToString());

    protected void DrawHitboxGizmo(AttackHitbox hb)
    {
        if (hb == null || !hb.showGizmo) return;
        int dir = FacingDir;
        Vector3 center   = transform.position + new Vector3(hb.offset.x * dir, hb.offset.y, 0f);
        Vector3 cubeSize = new Vector3(hb.size.x, hb.size.y, 0.1f);

        Gizmos.color = hb.gizmoColor;
        Gizmos.DrawCube(center, cubeSize);

        var c = hb.gizmoColor;
        Gizmos.color = new Color(c.r, c.g, c.b, 1f);
        Gizmos.DrawWireCube(center, cubeSize);
    }

    [Header("Special Attack")]
    public float specialAttackWarningDuration = 0.5f;

    // ── Runtime State ────────────────────────────────────────────────
    public float     CurrentHP            { get; protected set; }
    public bool      Invincible           { get; set; }   // 位移帧（瞬移/跳跃）期间不可被伤
    public Transform player               { get; protected set; }
    public float     attackCooldownTimer  { get; protected set; }

    public bool IsDefeated          => CurrentHP <= 0f;
    public bool SavesPermanentDeath => permanentDeath || GetComponent<MinotaurBoss>() != null;
    public bool RespawnsAtCheckpoint => !SavesPermanentDeath;
    public bool IsExecutable        => GetComponent<PoiseMeter>().IsBroken && CurrentHP > 0f;

    [Header("处决")]
    [Tooltip("处决提示浮在头顶的高度(世界单位,从敌人原点往上)。逐怪单独调,Scene 里黄线尖端就是它。")]
    public float executePromptHeight = 2f;
    public string SaveID            => SaveIdUtility.GetSceneObjectID(this, saveID);

    // ── Events ───────────────────────────────────────────────────────
    public event Action<float, float> OnHPChanged;
    public event Action OnDied;
    public event Action OnHit;

    // 是否已经初始化过（Awake 跑过）。预留的 inactive 敌人从未 Awake → false，
    // 存档刷新时跳过它们，避免被 Respawn 到未记录的 initialPosition(0,0,0) 飞出去。
    public bool Initialized { get; private set; }

    protected PoiseMeter poiseMeter;
    Vector3    initialPosition;
    Quaternion initialRotation;
    Vector3    initialScale;
    bool       initialFacingRight;
    Coroutine  deathRoutine;

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale    = transform.localScale;
        initialFacingRight = FacingRight;
        Initialized     = true;
        CurrentHP       = maxHP;
        poiseMeter      = GetComponent<PoiseMeter>();
        poiseMeter.OnPoiseBroken += OnPoiseBroken;

        int playerLayerIndex = LayerMask.NameToLayer(Layers.Player);
        if (playerLayerIndex >= 0 && (playerLayer.value & (1 << playerLayerIndex)) == 0)
            playerLayer = 1 << playerLayerIndex;
    }

    // ── AI Utilities (used by enemy states) ──────────────────────────
    // 感知玩家由 GroundEnemy.DetectPlayer 实现(横向矩形)；boss 不感知，靠 tag 锁定。

    public void LoseTarget() => player = null;

    // 一旦开战(combatEnabled)就锁定玩家、永不脱战；玩家真不在场(死亡/不在)才返回 false
    public bool EnsurePlayer()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(Tags.Player);
            if (p != null) player = p.transform;
        }
        return player != null;
    }

    // 是否继续交战(boss 用)：靠 tag 锁定玩家，开战后永不脱战
    public bool KeepEngaged() => EnsurePlayer();

    public float GetHorizontalDistToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Mathf.Abs(player.position.x - transform.position.x);
    }

    // boss / 简单调用：isSpecial 决定击退/硬直(普通 5/0；特殊用 specialHit*)
    public bool PerformAttack(float damage, Vector2 offset, Vector2 size, bool isSpecialAttack = false)
        => PerformAttack(damage, offset, size, isSpecialAttack,
                         isSpecialAttack ? specialHitKnockback : 5f,
                         isSpecialAttack ? specialHitStunDuration : 0f);

    // 地面怪逐招：显式 unblockable + 击退 + 硬直
    public bool PerformAttack(float damage, Vector2 offset, Vector2 size, bool unblockable, float knockback, float hitStun)
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(offset.x * FacingDir, offset.y);
        var hits = Physics2D.OverlapBoxAll(origin, size, 0f, playerLayer);
        foreach (var hit in hits)
            ApplyHitToCollider(hit, damage, unblockable, knockback, hitStun);
        return hits.Length > 0;
    }

    // ── 通用按权重选招（所有敌人共用）─────────────────────────────────
    // 基类只含 weight，各敌人继承加自己的 enum id 和扩展字段
    [System.Serializable]
    public class AttackWeight
    {
        public float weight = 1f;
    }

    // options：候选攻击列表；getWeight：每个攻击的权重（不可用返回 0）
    // 返回中签的 option，全部为 0 时返回 null
    public T PickAttack<T>(T[] options, System.Func<T, float> getWeight) where T : class
    {
        if (options == null || options.Length == 0) return null;
        float[] weights = new float[options.Length];
        for (int i = 0; i < options.Length; i++)
            weights[i] = getWeight(options[i]);
        int idx = WeightedPicker.Pick(weights);
        return idx >= 0 ? options[idx] : null;
    }

    // ══════════════════════════════════════════════════════════════════
    //  统一攻击系统(小怪与 boss 共用):数据化招池 + 连段 + 命中 + 动画。
    //  招 = id(clip)+ hits[](每下命中)+ 位移/节奏;连段 = 按 id 串招。
    // ══════════════════════════════════════════════════════════════════

    // 一「下」命中:自包含。一招(EnemyAttack)挂一串 hits,动画事件 Fire(i) 引用第 i 下。
    [System.Serializable]
    public class HitProfile
    {
        public AttackDelivery delivery = AttackDelivery.Melee;   // 近战命中盒 / 远程发射
        public bool    red = false;                              // 红=不可格挡 + 红视觉;白=可格挡
        public float   damageMultiplier = 1f;                    // 伤害 = 敌人 attack × 此值
        public float   knockback = 5f;
        public float   hitStun   = 0f;
        [Header("近战(delivery=Melee)")]
        public Vector2 hitboxOffset = new Vector2(0.7f, 0f);
        public Vector2 hitboxSize   = new Vector2(1f, 0.8f);
        [Header("远程(delivery=Ranged)")]
        public Arrow   projectilePrefab;
    }

    [System.Serializable]
    // 一招 = 纯打击数据(打什么)。怎么打/何时打 全在 ComboStep / EnemyCombo 上。
    public class EnemyAttack
    {
        public string  id = "attack";                       // 唯一键(连段按它引用,同时也是 animator 状态名)
        [Header("命中:这招打出的每一下(动画事件 Fire(i) 引用第 i 下)")]
        public HitProfile[] hits = new HitProfile[0];
        public bool    showGizmo = true;                     // 画命中盒 gizmo
    }

    // 连段里的一段:打哪招(引用 attacks[].id) + 出招前位移 + 段后停顿
    [System.Serializable]
    public class ComboStep
    {
        public string   attackId = "attack";              // 引用 attacks[].id(招只管"打什么")

        [Header("出招前(把敌人挪到位;留空=不动)")]
        [SerializeReference, SubclassSelector] public StepMover preMove;        // 段前位移:逼近/后撤/瞬移/跳(下拉选)
        [Header("出招时(这一下怎么打;留空=普通挥砍)")]
        [SerializeReference, SubclassSelector] public AttackDriverBase driver;  // 段级编排:前冲/挑飞(下拉选)
        [Tooltip("动画播放速度;<=0 = 用招的/1")]
        public float           animSpeed  = 0f;
        [Tooltip("本段打完后的停顿(秒);<0 = 用连段的 stepGap")]
        public float    gap      = -1f;
    }

    // 连段:把 attacks 里的招按顺序串起来。一只怪可配多套,按权重 + 距离 + 血量抽。
    [System.Serializable]
    public class EnemyCombo
    {
        public string      name     = "combo";
        public float       weight   = 1f;
        [Tooltip("可被选中的距离区间(米)")]
        [MinMaxSlider(0f, 30f)] public Vector2 range = new Vector2(0f, 2f);     // x=min, y=max
        [Tooltip("可被选中的血量区间(0~1 百分比)。配低血段 → 残血才放,不必写阶段状态")]
        [MinMaxSlider(0f, 1f)]  public Vector2 hpPercent = new Vector2(0f, 1f); // x=min, y=max
        [Tooltip("勾上 = 不连续抽到同一套(增加变化)")]
        public bool        noRepeat = false;
        [Tooltip("整套打完的冷却(秒);<=0 = 用敌人默认 attackCooldown")]
        public float       cooldownAfter = 0f;
        [Tooltip("段间默认停顿(秒),0 = 无缝;每段可用自己的 gap 覆盖")]
        public float       stepGap  = 0.08f;
        public ComboStep[] steps    = new ComboStep[0];
    }

    [Header("Attacks (攻击池)")]
    public EnemyAttack[] attacks = { new EnemyAttack() };
    [Header("Combos (连段;留空=只用单招)")]
    public EnemyCombo[] combos;

    // 红色 hit 统一视觉:红染 + 放大(固定约定);数值归 HitProfile 逐 hit 配。
    public static readonly Color DangerTint  = new Color(1f, 0.45f, 0.3f, 1f);
    public const           float DangerScale = 1.4f;

    [Header("远程通用 (delivery=Ranged 的招用)")]
    public Vector2 firePointOffset = new Vector2(0.7f, 0.2f);   // 出箭点(x 随朝向翻转)
    public float   aimHeightOffset = 0f;                         // 瞄准点相对玩家 pivot 的垂直偏移

    public EnemyAttack CurrentAttack { get; private set; }
    public EnemyCombo  CurrentCombo  { get; private set; }   // 非空 = 正在打连段
    public ComboStep   CurrentStep   { get; private set; }   // 当前段(含位移 / 停顿)
    EnemyCombo         lastCombo;                             // 上一套(noRepeat 用)
    int comboStep;

    // 强制下一套连段(开场/转阶段开场用):按名字,无视距离/权重。用过即清。
    string forcedComboName;
    public void ForceCombo(string name) => forcedComboName = name;

    // 出手/追击边界 = 最远连段的 maxRange(取代旧的固定 attackRange);没连段则 1.5。
    // boss 用它决定"打不了时 追还是站等";超出 → 追,在内 → 站等(具体打哪招由各连段 range 选)。
    public float MaxComboRange()
    {
        float r = 0f;
        if (combos != null)
            foreach (var c in combos)
                if (c != null && c.range.y > r) r = c.range.y;
        return r > 0.01f ? r : 1.5f;
    }

    // ── 选连段：先看强制,否则按距离 + 血量 + 权重(可排除上一套)抽一套,载入第一段 ──
    public bool TryPickCombo(float dist)
    {
        if (combos == null || combos.Length == 0) return false;

        EnemyCombo c = null;
        if (!string.IsNullOrEmpty(forcedComboName))
        {
            foreach (var x in combos)
                if (x != null && x.name == forcedComboName && x.steps != null && x.steps.Length > 0) { c = x; break; }
            forcedComboName = null;
        }
        if (c == null)
        {
            float hp = maxHP > 0f ? CurrentHP / maxHP : 1f;
            float Weight(EnemyCombo x, bool blockRepeat)
            {
                if (x == null || x.steps == null || x.steps.Length == 0) return 0f;
                if (dist < x.range.x || dist > x.range.y)               return 0f;
                if (hp  < x.hpPercent.x || hp > x.hpPercent.y)          return 0f;
                if (blockRepeat && x.noRepeat && x == lastCombo)        return 0f;
                return Mathf.Max(0f, x.weight);
            }
            c = PickAttack(combos, x => Weight(x, true));
            // noRepeat 是"软偏好":排除上一套后无招可放 → 放开,允许重复,避免唯一可用时卡住不出招
            if (c == null) c = PickAttack(combos, x => Weight(x, false));
        }
        if (c == null) return false;
        CurrentCombo = c;
        lastCombo    = c;
        comboStep    = 0;
        return AdvanceCombo();
    }

    // 推进连段到下一段;载入成功 = true,连段打完 / 配错 = false(并清空)
    public bool AdvanceCombo()
    {
        if (CurrentCombo == null || CurrentCombo.steps == null || comboStep >= CurrentCombo.steps.Length)
        {
            ResetCombo();
            return false;
        }
        var step = CurrentCombo.steps[comboStep];
        comboStep++;
        var atk = step != null ? FindAttack(step.attackId) : null;
        if (atk == null) { ResetCombo(); return false; }
        CurrentStep   = step;
        CurrentAttack = atk;
        return true;
    }

    public void ResetCombo() { CurrentCombo = null; CurrentStep = null; comboStep = 0; }

    protected EnemyAttack FindAttack(string id)
    {
        if (attacks == null) return null;
        foreach (var a in attacks)
            if (a != null && a.id == id) return a;
        return null;
    }

    // ── 命中:攻击 clip 上的动画事件 Fire(i) 调用(可多帧多次,i=hits 下标) ──
    int attackSwing;
    public void ResetAttackSwings() => attackSwing = 0;
    // 动画事件 Fire(i):统一转给运行器(有驱动→驱动 OnFire,否则实打 hits[i])。小怪与 boss 一套。
    public virtual void Fire(int i) => Attack.OnFire(i);
    // 实打第 i 下(运行器无驱动时调):近战命中盒 / 远程发射
    public void DoFireHit(int i)
    {
        var a = CurrentAttack ?? (attacks != null && attacks.Length > 0 ? attacks[0] : null);
        if (a == null || a.hits == null || i < 0 || i >= a.hits.Length || a.hits[i] == null) return;
        var h = a.hits[i];
        float dmg = attack * DamageMultiplier * h.damageMultiplier;
        if (h.delivery == AttackDelivery.Ranged)
        {
            SpawnProjectile(h.projectilePrefab, dmg, h.red, h.knockback, h.hitStun,
                            h.red ? DangerTint : Color.white, h.red ? DangerScale : 1f);
            AudioManager.Instance?.PlayBow(h.red);
        }
        else
        {
            PerformAttack(dmg, h.hitboxOffset, h.hitboxSize, h.red, h.knockback, h.hitStun);
            AudioManager.Instance?.PlayEnemySwing(attackSwing);
            attackSwing++;
        }
    }

    // 红闪预警:成对事件 Warn/WarnEnd(时长由两事件在时间轴位置决定)。
    public void Warn()    => GetComponent<DamageFeedback>()?.HoldWarning();
    public void WarnEnd() => GetComponent<DamageFeedback>()?.ClearWarning();

    // 远程出箭(朝玩家瞄准带高低差)；Fire 的远程 hit 用
    protected void SpawnProjectile(Arrow prefab, float dmg, bool unblockable, float knockback, float hitStun, Color tint, float scale)
    {
        if (prefab == null) return;
        Vector3 spawn = transform.position + new Vector3(firePointOffset.x * FacingDir, firePointOffset.y, 0f);
        Vector2 aim = Vector2.right * FacingDir;
        if (player != null)
        {
            aim = ((Vector2)player.position + Vector2.up * aimHeightOffset) - (Vector2)spawn;
            if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * FacingDir;
        }
        var arrow = Instantiate(prefab, spawn, Quaternion.identity);
        if (!Mathf.Approximately(scale, 1f)) arrow.transform.localScale *= scale;
        var sr = arrow.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = tint;
        arrow.Launch(this, aim, dmg, unblockable, knockback, hitStun);
    }

    // ── 动画 clip 名 + 播放(统一走 Anim.Play;子类可覆盖 clip 名 / ResolveClip 加后缀) ──
    public virtual string IdleClip  => "idle";
    public virtual string MoveClip  => "walk";
    public virtual string HurtClip  => "hit";
    public virtual string DeathClip => "death";
    protected virtual string ResolveClip(string baseName) => baseName;
    public void PlayClip(string baseName) { if (Anim != null && !string.IsNullOrEmpty(baseName)) Anim.Play(ResolveClip(baseName), 0, 0f); }
    public void PlayIdle()          => PlayClip(IdleClip);
    public void PlayMove()          => PlayClip(MoveClip);
    public void PlayHurt()          => PlayClip(HurtClip);
    public void PlayDeath()         => PlayClip(DeathClip);
    public void PlayCurrentAttack() => PlayClip(CurrentAttack != null ? CurrentAttack.id : "attack");

    // 取 animator 里某 clip 的时长(没有则 0)
    public float ClipLength(string clipName)
    {
        if (Anim != null && Anim.runtimeAnimatorController != null)
            foreach (var c in Anim.runtimeAnimatorController.animationClips)
                if (c.name == clipName) return c.length;
        return 0f;
    }

    // 当前选中招式对应 clip 的时长(含变身后缀解析)——攻击态兜底超时用
    public float CurrentAttackClipLength() => ClipLength(ResolveClip(CurrentAttack != null ? CurrentAttack.id : "attack"));

    // ── 统一攻击运行器 + 钩子(小怪/ boss 攻击态都驱动它)─────────────────
    AttackRunner attackRunner;
    public AttackRunner Attack => attackRunner != null ? attackRunner : (attackRunner = new AttackRunner(this));

    // 动画事件入口(统一转给运行器)。命中走 Fire(i);招放完走 AnimFinish/AnimationTrigger。
    public void AnimAttackEnd()    => Attack.OnAnimEnd();
    public virtual void AnimFinish() => Attack.OnAnimEnd();   // 攻击 clip 末帧事件

    // 子类按需覆盖:伤害总倍率(boss 二阶段)/ 攻击移动速度 / 悬崖检测(小怪)
    public virtual float DamageMultiplier => 1f;
    public virtual float AttackMoveSpeed  => 3.5f;
    public virtual bool  LedgeAhead(float dir) => false;

    SpriteRenderer mainSpriteCache;
    public SpriteRenderer MainSprite => mainSpriteCache != null ? mainSpriteCache : (mainSpriteCache = GetComponentInChildren<SpriteRenderer>());
    public void SetSpriteVisible(bool v) { if (MainSprite != null) MainSprite.enabled = v; }
    public void SetSpriteAlpha(float a)  { if (MainSprite == null) return; var c = MainSprite.color; c.a = Mathf.Clamp01(a); MainSprite.color = c; }

    // public：近战 PerformAttack 与远程 Arrow 共用同一套命中处理(格挡/识破/击退)
    public void ApplyHitToCollider(Collider2D hit, float damage, bool unblockable, float knockback, float hitStun)
    {
        if (hit == null) return;
        var ctrl  = hit.GetComponent<PlayerController>();
        var stats = hit.GetComponent<PlayerStats>();
        if (stats == null || stats.IsInvulnerable) return;

        // 被命中（无论格挡与否）→ 转身面向攻击来源
        if (ctrl != null)
            ctrl.FaceToward(transform.position);

        bool isBlocking   = ctrl != null && ctrl.IsBlocking;
        bool isCountering = ctrl != null && ctrl.IsCountering;
        var  feedback     = hit.GetComponent<DamageFeedback>();

        // ── 防御判定（剪刀石头布）：普通招只能「格挡」防、危招(unblockable)只能「识破」防 ──
        if (unblockable)
        {
            // 危招无视格挡；识破成功 → 免伤(并给敌人 poise)，否则继续往下吃实打
            if (isCountering && ctrl.TryCounter(this)) return;
        }
        else if (isBlocking)
        {
            // 普通招被格挡 → 走格挡处理(削韧/掉耐久)，不吃伤不击退
            ctrl.ReceiveBlockHit(damage, this);
            return;
        }

        // ── 没防住：扣血 + 击退 ──
        // 击退是 DamageFeedback 给的一段会衰减的水平速度，但 idle/move 每帧会重设速度盖掉它；
        // 所以推人前必须先把玩家切进硬直态(StunnedState 不清速度)，击退才滑得出来。
        stats.TakeDamage(damage);
        float stun = hitStun;
        if (knockback > 0f && feedback != null)
            stun = Mathf.Max(stun, feedback.knockbackDuration);            // 至少硬直到击退滑完
        if (stun > 0f) ctrl?.Stun(stun);                                   // 先进硬直(Enter 归零速度)
        feedback?.ApplyKnockback(transform.position, knockback);           // 再给击退速度
    }

    public void StartAttackCooldown()           => attackCooldownTimer = attackCooldown;
    public void StartAttackCooldown(float secs) => attackCooldownTimer = secs;   // 指定时长(per-招冷却用)
    public void ResetPoise()           => poiseMeter?.ResetPoise();

    // 只削韧性、不掉血（完美格挡反震用）：累计破韧 → OnPoiseBroken → 硬直
    public void ApplyPoiseDamage(float poise)
    {
        if (CurrentHP <= 0f || Invincible || poise <= 0f) return;
        poiseMeter?.TakePoiseDamage(poise);
    }

    // ── Damage / Death ───────────────────────────────────────────────
    public virtual void TakeDamage(float damage) => TakeDamage(damage, 0f);

    public virtual void TakeDamage(float damage, float poiseDamage)
    {
        if (CurrentHP <= 0f) return;
        if (Invincible) return;   // 瞬移/跳劈飞行中免伤
        CurrentHP = Mathf.Max(CurrentHP - damage, 0f);
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害，剩余 HP：{CurrentHP}/{maxHP}");
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnHit?.Invoke();
        AudioManager.Instance?.PlayEnemyHit();
        if (poiseDamage > 0f) poiseMeter.TakePoiseDamage(poiseDamage);

        var feedback = GetComponent<DamageFeedback>();
        if (feedback != null) feedback.Flash();

        if (CurrentHP <= 0f) Die();
    }

    public virtual void OnExecuted(float damage)
    {
        CurrentHP = 0f;
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        Die();
    }

    protected virtual void Die()
    {
        if (SavesPermanentDeath)
            SaveSystem.Instance?.MarkEnemyDefeated(SaveID);

        // 死亡音改由死亡动画"摔倒落地"帧的 AnimDeathFall 事件触发，避免和受击音同帧叠掉
        var playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null) playerStats.AddGold(goldDrop);
        OnDied?.Invoke();

        OnDeath();

        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DisableAfterDeath());
    }

    // Override in subclasses to handle death animation via state machine
    protected virtual void OnDeath() => SetAnimBool("isDead");

    // Override in subclasses to reset state machine
    protected virtual void OnRespawn() => SetAnimBool("isIdle");

    protected virtual void OnPoiseBroken() { }

    // 被玩家识破时调用（子类重写来打断当前攻击 / 进入硬直）
    public virtual void OnCountered() { }

    // 死亡后多久消失(子类按死亡动画时长覆盖，避免动画没播完就消失)
    protected virtual float DeathDisableDelay => 1f;

    IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(DeathDisableDelay);
        gameObject.SetActive(false);
        deathRoutine = null;
    }

    // ── Save System ──────────────────────────────────────────────────
    public virtual void LoadSaveState(float savedHP, bool defeated)
    {
        if (defeated)
        {
            CurrentHP = 0f;
            OnHPChanged?.Invoke(CurrentHP, maxHP);
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);
        Respawn(savedHP > 0f ? Mathf.Clamp(savedHP, 1f, maxHP) : maxHP);
    }

    public virtual void Respawn(float hp = -1f)
    {
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        transform.position   = initialPosition;
        transform.rotation   = initialRotation;
        transform.localScale = initialScale;
        FacingRight          = initialFacingRight;   // 跟 localScale 一起复位，避免读档后朝向逻辑与视觉脱节、攻击打反方向
        gameObject.SetActive(true);

        CurrentHP = hp > 0f ? Mathf.Clamp(hp, 1f, maxHP) : maxHP;
        poiseMeter = poiseMeter != null ? poiseMeter : GetComponent<PoiseMeter>();
        poiseMeter?.ResetPoise();

        Rb.linearVelocity    = Vector2.zero;
        attackCooldownTimer  = 0f;
        player               = null;

        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnRespawn();
    }

    // ── Animation Bool Helper (used by BossAI) ───────────────────────
    public virtual void SetAnimBool(string boolName)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return;
        foreach (var param in Anim.parameters)
        {
            // isFlame 是 DemonSamurai 变身的持久标志,不参与"一次只亮一个"清除
            if (param.type == AnimatorControllerParameterType.Bool && param.name != "isFlame")
                Anim.SetBool(param.name, param.name == boolName);
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        DrawDetectionGizmo();

        // 每招命中盒(HitProfile,近战)→ 所有敌人(含 boss)都画,受招的 showGizmo 控制
        if (attacks != null)
        {
            int dir = FacingDir;
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.9f);
            foreach (var a in attacks)
                if (a != null && a.showGizmo && a.hits != null)
                    foreach (var h in a.hits)
                        if (h != null && h.delivery == AttackDelivery.Melee)
                            Gizmos.DrawWireCube(transform.position + new Vector3(h.hitboxOffset.x * dir, h.hitboxOffset.y, 0f),
                                                new Vector3(h.hitboxSize.x, h.hitboxSize.y, 0.1f));
        }

        // 处决提示锚点:原点 → executePromptHeight 处(框就浮在这里),逐怪调高度时看这条线
        Vector3 anchor = transform.position + Vector3.up * executePromptHeight;
        Gizmos.color = new Color(1f, 0.82f, 0.28f, 0.9f);
        Gizmos.DrawLine(transform.position, anchor);
        Gizmos.DrawWireSphere(anchor, 0.12f);
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(anchor + Vector3.up * 0.18f, "处决提示");
#endif
    }

    // 默认不画探测 gizmo(boss 那个 MaxComboRange 红圈太大、没必要显示);GroundEnemy 覆盖为横向探测框
    protected virtual void DrawDetectionGizmo() { }
}
