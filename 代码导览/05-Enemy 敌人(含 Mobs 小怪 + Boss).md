## Enemy 敌人(含 Mobs 小怪 + Boss)

**定位**:游戏里所有"会打你的东西"。一套数据驱动的统一攻击系统(招池 + 连段 + 命中 + 动画)上移到公共基类 `EnemyBase`,小怪(`GroundEnemy` 一族)和关卡 boss(`MinotaurBoss`)共用同一套出招/命中/连段/驱动逻辑,只在状态机的"AI 决策层"上各走各的;再叠存档、处决、韧性破防、boss 登场/二阶段演出。

**关键脚本**

核心层(Mobs+Boss 共用,直接放在 `Enemy/` 根):
- `Assets/Scripts/Enemy/EnemyBase.cs:6` — 所有敌人的基类(继承 `Entity`)。承载血量/攻击/掉金、命中处理 `ApplyHitToCollider`(:393)、受伤死亡 `TakeDamage`/`Die`(:446/:470)、存档接口 `LoadSaveState`/`Respawn`(:508/:521),以及**整套数据化攻击系统**:招 `EnemyAttack`/命中 `HitProfile`/连段 `EnemyCombo`/段 `ComboStep`(:147-205)、连段抽选 `TryPickCombo`/`AdvanceCombo`(:242/:276)、动画事件入口 `Fire(i)`/`AnimFinish`(:306/:380)。
- `Assets/Scripts/Enemy/AttackRunner.cs:7` — **统一攻击运行器**,`EnemyBase` 持有一个(:377)。把"一招/一套连段"跑成相位机 `PreMove→Swing→Gap`(:12),小怪和 boss 攻击态都只是调它的薄壳。
- `Assets/Scripts/Enemy/StepMovers.cs:6` — 出招【前】的位移驱动 `StepMover`(可插拔多态):`ApproachMover` 逼近、`RetreatMover` 后撤、`TeleportMover` 瞬移闪身、`JumpMover` 跳劈接近。挂在 `ComboStep.preMove`。
- `Assets/Scripts/Enemy/AttackDrivers.cs:7` — 出招【中】的编排驱动 `AttackDriverBase`(可插拔多态):`LungeForward/Backward` 前冲后撤、`JumpDriver` 边跳边劈、`LaunchDriver` 横劈挑飞+下砸。挂在 `ComboStep.driver`。

Mobs 小怪(`Enemy/Mobs/`):
- `Assets/Scripts/Enemy/Mobs/GroundEnemy.cs:8` — 地面小怪共享基类。在 `EnemyBase` 之上加状态机(Idle/Move/Chase/Attack/Stunned/Dead)、横向矩形探测 `DetectPlayer`(:175)、巡逻/墙/悬崖检测 `WallAhead`/`LedgeAhead`(:66/:75)、开发期招式自检 `ValidateAttacks`(:112)。
- 状态机各态(`Enemy/Mobs/States/`):`Enemy_GroundedState`(感知到玩家→Chase 的公共父类)、`Enemy_IdleState`/`Enemy_MoveState`(待命+巡逻踱步)、`Enemy_ChaseState.cs:3`(交战核心:维持距离、脱战计时、抽连段)、`Enemy_AttackState`(薄壳,驱动运行器)、`Enemy_StunnedState`(破韧硬直)、`Enemy_DeadState`。
- 具体怪种:`AoTenguEnemy`(最简单 1 招,只覆盖 clip 名)、`DemonSamuraiEnemy.cs:5`(50% 血吼叫变身火焰形态,clip 加 `_flame` 后缀 + 攻防速强化)、`ArcherEnemy.cs:6`(远程射手,半血一次性后撤步闪避 + 大招悬空放箭)、`SamuraiEliteEnemy`(精英,行为全在 Inspector 数据里)。
- `Assets/Scripts/Enemy/Mobs/Arrow.cs:6` — 箭矢投射物,命中后复用 `owner.ApplyHitToCollider` 走同一套格挡/识破/击退,并吸附跟随被击退的玩家。

Boss(`Enemy/Boss/`):
- `Assets/Scripts/Enemy/Boss/MinotaurBoss.cs:4` — 牛头人 boss(直接继承 `EnemyBase`,不走 `GroundEnemy`)。自带 9 个状态、二阶段判定 `CheckPhase2`(:117)、二阶段过渡演出协程 `Phase2TransitionRoutine`(:134)、登场唤醒 `Activate`(:262,强制开场连段)、处决/识破反应 `OnExecuted`/`OnCountered`(:302/:292)、怒气染色与粒子。
- Boss 状态(`Enemy/Boss/States/`):`Boss_BattleState.cs:5`(**路由 hub**,按距离/冷却分发 attack/chase/wait)、`Boss_ChaseState`/`Boss_WaitState`(追击/站等)、`Boss_AttackState`(薄壳)、`Boss_EnragedState`(二阶段入口,触发演出)、`Boss_StunnedState`(破韧/处决硬直)、`Boss_StaggerState`(识破停顿)、`Boss_IdleState`/`Boss_DeadState`。

**怎么工作**

1) 攻击是纯数据。在 Inspector 上每只怪配 `attacks[]`(招池,每招一个 `id` = animator 状态名 + 一串 `HitProfile` 命中)和 `combos[]`(连段,每段 `ComboStep` 引用招 id,可挂 `preMove`/`driver`,带 `range`/`hpPercent`/`weight`)。"打什么"是数据,"怎么打/何时打"由 step 上的两个可插拔槽决定(`EnemyBase.cs:174-205`)。

2) 选招:`TryPickCombo(dist)`(`EnemyBase.cs:242`)先看有没有强制连段(`forcedComboName`,开场/转阶段用),否则按"距离区间 + 血量区间 + 权重"用 `WeightedPicker` 抽一套;`noRepeat` 是软偏好(排除上一套后抽不到就放开,避免卡住)。抽中后载入第一段,推进靠 `AdvanceCombo`。

3) 执行:攻击态 `Enter` 调 `Attack.Begin()`,运行器走相位机(`AttackRunner.cs`):
   - **PreMove**:跑 `step.preMove`(逼近/瞬移/跳),`Tick` 返回 true 表示到位 → 转 Swing。
   - **Swing**:`SetOnlyAnimBool("isAttacking")` + `PlayCurrentAttack()` 强播该招 clip;`step.driver` 每帧驱动位移(前冲/跳劈/挑飞)。命中由动画事件 `Fire(i)` 触发(:306),有 driver 转给 `driver.OnFire`,否则 `DoFireHit(i)` 实打第 i 个 `HitProfile`(近战 `PerformAttack` 或远程 `SpawnProjectile`)。退出靠 clip 末帧事件 `AnimFinish` 或超时兜底。
   - **Gap**:段间停顿,然后 `AdvanceCombo` 推进下一段;打完 `Finish` 进冷却。

4) 命中与防御(剪刀石头布):`ApplyHitToCollider`(`EnemyBase.cs:393`)统一处理——白招(可格挡)被 `IsBlocking` 挡 → `ReceiveBlockHit` 削耐久不吃伤;红招 `red`(不可格挡)只能被 `IsCountering` 识破(`TryCounter`)免伤;都没防住则扣血 + 先把玩家切硬直态再给击退(顺序很关键,见易错点)。红招统一视觉(`DangerTint`/`DangerScale`)+ 预警闪红用成对动画事件 `Warn`/`WarnEnd`。

5) 韧性/破防:`PoiseMeter`(`Combat/PoiseMeter.cs`)被削满 → `OnPoiseBroken` → 进硬直态(可被处决)。玩家完美格挡/识破会 `ApplyPoiseDamage` 削怪韧性。

6) AI 决策层(状态机各异):
   - 小怪 `Enemy_ChaseState`(`Mobs/States/Enemy_ChaseState.cs:34`):靠 `DetectPlayer`(横向矩形)感知 + `battleTimeDuration` 脱战计时;够得到(高差 ≤ `chaseVerticalLimit`)+ CD 好就 `TryPickCombo` 进攻,否则按 `preferredCombatDistance`/`retreatDistance` 维持身位。
   - boss `Boss_BattleState`(`Boss/States/Boss_BattleState.cs:9`)是无动画的 hub:`KeepEngaged` 锁定玩家永不脱战;CD 好抽到连段 → attack,否则超出 `MaxComboRange` → chase,在范围内 → wait。"打哪招"完全交给各连段自己的 `range` gating。

7) boss 二阶段:`CheckPhase2` 血过 50% → 进 `Boss_EnragedState` → `Phase2TransitionRoutine`(`MinotaurBoss.cs:134`):定身 → 吼叫顿帧/震屏/把玩家吼飞 → 蓄力 → 爆开染红 + 切二阶段 BGM → 强制"二阶段开场"连段。`DamageMultiplier`/`AttackMoveSpeed` 二阶段加成。

**入口 & 触发**

- 创建:敌人是场景里预摆的 prefab,`Awake` 建状态机并 `Initialize(idleState)`(`GroundEnemy.cs:93` / `MinotaurBoss.cs:65`)。每帧 `Update` 跑冷却计时 + `stateMachine.Update()`。
- 玩家攻击触达:`Player_AttackState.DoHit`(`Player/States/Player_AttackState.cs:101`)`OverlapBox` 命中 → `enemy.TakeDamage(dmg, poise)`;冲刺斩 `Player_DashStrikeState` 同理。
- 处决:`ExecutePromptUI`(`UI/ExecutePromptUI.cs:48`)扫附近 `IsExecutable`(韧性破 + 存活)的怪在头顶(`executePromptHeight`)显提示;`Player_ExecuteState`(`Player/States/Player_ExecuteState.cs:54`)按 R → `enemy.OnExecuted(dmg)`(boss 不即死,扣大额血)。
- 格挡/识破:`enemy.ApplyHitToCollider` 反向回调 `PlayerController.TryCounter`/`ReceiveBlockHit`(`PlayerController.cs:86-87`)。
- boss 登场:`BossIntroTrigger`(`BossFight/BossIntroTrigger.cs`)`Start` 把 boss 设沉睡(`combatEnabled=false`),玩家进触发区 → 对话/吼叫/推镜演出 → `StartCombat` 调 `boss.Activate()`(:262)唤醒并强制首套连段、关雾门。
- 远程:`EnemyBase.SpawnProjectile`(:333)实例化 `Arrow.Launch`,箭自管飞行/命中/碎裂。

**依赖 & 被依赖**

依赖(它用别人):
- `Entity`/`StateMachine`/`EntityState`(`StateMachine/`)——继承的基类与通用状态机;小怪和 boss 都直接 `new StateMachine()`(`stateMachine` 字段声明在 `EnemyBase`,小怪/boss 共用一处),`EnemyStateMachine`/`BossStateMachine` 空壳子类已删除。
- `PoiseMeter`(`Combat/`)——韧性条,破韧事件驱动硬直/可处决。
- `WeightedPicker`(`Util/`)——连段加权抽选。
- `DamageFeedback`——闪白 `Flash` + 击退 `ApplyKnockback` + 红招预警 `HoldWarning`/`ClearWarning`;boss 还用它做怒气染色 `SetBaseColor`。
- `PlayerController`/`PlayerStats`——命中时读 `IsBlocking`/`IsInvulnerable`、调 `TakeDamage`/`Stun`/`TryCounter`;`LaunchDriver` 调玩家 `BeginLift`/`SetLiftPosition`/`EndLift` 做挑飞。
- `AudioManager`/`VfxManager`/`CameraShake`/`ScreenRoarFx`/`Hitstop`——攻击音、boss 演出特效。
- `SaveSystem`——`Die` 时 `MarkEnemyDefeated`;`BossProfile`(`Data/`)——boss 名牌数据。

被依赖(别人用它,具体到方法):
- `SaveSystem`(`Save/SaveSystem.cs:333/511/690`)——`FindObjectsByType<EnemyBase>` 批量 `Respawn`/`LoadSaveState`,按 `SaveID`/`RespawnsAtCheckpoint` 区分永久死亡 vs 复活点重生。
- `CheckpointManager`(`Save/CheckpointManager.cs:27`)——`EnemyBase.AnyBossInCombat()` 为真时禁用火堆(boss 战不许存档/回血)。
- `LevelManager`(`Level/LevelManager.cs:66`)——按 `isBoss` 找场景 boss,接管 BGM/胜利检测,死亡后按 `nextSceneOnDefeat` 切场景。
- `EnemyHealthBarUI`/`BossHealthBarUI`(`UI/`)——订阅 `OnHPChanged`/`OnDied` 事件画血条。
- `BossIntroTrigger`/`BossFogGate`/`VictoryGate`/`EnemyClearGate`(`BossFight/`)——用 `combatEnabled`/`Activate`/`CurrentHP`/`IsDefeated` 控制登场与门禁。
- `Player_ExecuteState`/`Player_AttackState`/`Player_CounterState`/`Player_BlockState`——玩家侧攻击/处决/识破/格挡的对接点。

**关键设计 / 易错点**

- **小怪与 boss 一套攻击系统,只 AI 分叉**:招/连段/命中/驱动全在 `EnemyBase` + `AttackRunner`,小怪的 `Enemy_AttackState` 和 boss 的 `Boss_AttackState` 都只是 `Begin/Tick/Cancel` 薄壳(`Mobs/States/Enemy_AttackState.cs` / `Boss/States/Boss_AttackState.cs`)。改攻击逻辑只动核心层。
- **两个可插拔槽分相位**:`StepMover`(出招【前】挪位)和 `AttackDriverBase`(出招【中】编排)是并列的两个 `[SerializeReference, SubclassSelector]` 多态槽,别混。注意有两份跳劈:`JumpMover`(先跳完再砍,preMove 槽)vs `JumpDriver`(边跳边砍,driver 槽)。
- **命中后必须先硬直再击退**(`EnemyBase.cs:421-429`):idle/move 每帧重设速度会盖掉击退,所以先 `ctrl.Stun(stun)`(进不清速度的 StunnedState)再 `ApplyKnockback`,否则推不动玩家。
- **`Fire(i)` 的下标语义**:`i` 是 `attacks[].hits[]` 的下标,动画 clip 上每帧事件 `Fire(0)/Fire(1)...` 引用第几下;`LaunchDriver` 用 `Fire(0)=横劈挑飞`、`Fire(1)=下砸`。配错下标会打空或不触发,`ValidateAttacks`(`GroundEnemy.cs:112`,仅 Editor)会警告 id 对不上 animator / 远程缺箭矢。
- **`id` 既是连段引用键又是 animator 状态名**:招 id 必须和 Animator 里同名状态对得上,否则 `PlayCurrentAttack` 放不出动画、Fire 事件也不会触发(静默失败,靠自检兜)。`DemonSamurai` 变身后 `ResolveClip` 给部分动作加 `_flame` 后缀。
- **位移帧免伤靠 `Invincible`**:瞬移/跳跃中设 kinematic + `Invincible=true`,`TakeDamage` 直接 return。被破韧/识破/死亡打断时 `Attack.Cancel()`(`AttackRunner.cs:124`)必须恢复物理/无敌/可见/动画速度,否则 boss 会卡在 kinematic 或半透明。
- **boss 永不脱战、小怪会脱战**:boss 用 `KeepEngaged`(tag 锁定)开战即焊死;小怪用 `DetectPlayer` + `battleTimeDuration` 计时脱战,且高差超 `chaseVerticalLimit` 算够不到。
- **存档区分永久死亡 vs 复活**:`SavesPermanentDeath`(`permanentDeath` 或挂了 `MinotaurBoss`)→ 死了记 `MarkEnemyDefeated` 永不复活;其余 `RespawnsAtCheckpoint` → 火堆/读档时 `Respawn` 回初始位置。预留的 inactive 怪从未 `Awake`(`Initialized=false`),刷新时跳过,避免被 Respawn 到 (0,0,0) 飞走。
- **二阶段演出的幂等复原**:`RestorePhase2State`(`MinotaurBoss.cs:201`)正常结束和 `OnDisable` 异常中断都调,复原 timeScale/玩家物理/血条/无敌,防止演出被打断时玩家卡在 `simulated=false`。`OnRespawn` 要 `StopAllCoroutines` + `Attack.Cancel` 清残留协程/连段。
- **`SetOnlyAnimBool` 一次只亮一个 bool**(`EnemyBase.cs:538`):遍历清所有 bool 只留目标,但 `isFlame`(变身持久标志)被排除,不参与互斥清除。攻击/死亡 clip 是 `Anim.Play` 强进的,只靠 SetBool 切不回来,所以 `Boss_DeadState`/`OnRespawn` 显式 `Anim.Play("death"/"idle")`。
