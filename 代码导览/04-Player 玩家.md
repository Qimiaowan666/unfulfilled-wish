## Player 玩家

**定位**:玩家主角的"大脑+身体"——一个 `DontDestroyOnLoad` 常驻单例,内部跑一套状态机,把输入、移动、战斗(普攻/连段/格挡/识破/处决/技能)、数值(HP/虚血/体力/钱)、动画事件全部串起来。它是整个游戏战斗循环的玩家一侧,所有敌人攻击、UI 血条、存档、商店/装备/技能加成最终都打到它身上。

**关键脚本**
- `Assets/Scripts/Player/PlayerController.cs:7` — 主控。继承 `Entity`,持有单例 `Instance`、状态机和全部状态实例、所有 Inspector 战斗参数(连段倍率/格挡窗口/处决范围等)、冷却计时器。负责跨场景常驻/重复实例自毁、复活、被挑起(Lift)位移、把动画事件转发给当前状态。
- `Assets/Scripts/Player/PlayerStats.cs:5` — 玩家数值中枢。HP、虚血(ghost HP)、体力(stamina)、金币、攻防;暴露 `OnHPChanged/OnGhostHPChanged/OnStaminaChanged/OnStatsChanged/OnDamaged/OnDeath` 事件,`TakeDamage`(负数=回血)、`OnNormalBlock`、`RedeemGhostHP`、装备/技能加成的重算。
- `Assets/Scripts/Player/PlayerInput.cs:4` — 输入采样层。每帧读 `Keyboard/Mouse.current`,产出 `MoveInput/JumpPressed/AttackPressed/BlockHeld/BlockPressed/DashPressed/CounterPressed/ExecutePressed/Skill1/2Pressed` 等只读属性;并在暂停/商店/对话/boss 演出等时机统一清空输入(`InputBlocked`)。
- `Assets/Scripts/Player/PlayerBaseState.cs:5` — 主角状态基类。在公共 `EntityState` 上加 `player/input` 引用 + `CheckGlobalTransitions()`(识破/冲刺/技能这种"哪个态都能打断"的全局过渡)+ 动画事件虚回调(`OnAnimationFinished/OnHitFrame/OnCounterWindowClosed`)。
- `Player_SkillManager.cs:6`、`PlayerAnimationEvents.cs:4` — 玩家状态机直接用通用 `StateMachine`(旧空壳子类 `PlayerStateMachine` 已删,无命名子类);`Player_SkillManager` 挂在 "Skills" 子节点,`Awake` 收集所有 `Skill_Base`,按 `PlayerSkillType` 查找/施放;`PlayerAnimationEvents` 挂在带 Animator 的物体上,把 Unity 动画事件(`AnimationHit/Finish/CounterWindowClosed/Footstep`)桥接回 `PlayerController`。
- `Assets/Scripts/Player/States/*.cs`(13 个态)— 移动类:`GroundedState`(idle/move 共享落地态过渡逻辑)、`Idle/Move`、`AiredState`(jump/fall 共享空中过渡)、`Jump/Fall`、`Dash`;战斗类:`Attack`(连段)、`Block`(完美格挡防刷)、`Counter`(识破)、`Execute`(处决);技能/反应类:`DashStrike`、`Heal`、`Stunned`、`Knocked`、`Dead`。

**怎么工作**
- 状态机骨架来自通用 `StateMachine.cs:3`:`currentState` + `canChangeState` 开关。`ChangeState` 在 `Lock()` 时被忽略(只有死亡/某些施法用)。`PlayerController.Update` 每帧驱动 `stateMachine.Update()`,当前态的 `Update` 里判断过渡。
- **过渡分三层**(都在态的 `Update` 里串):①`CheckGlobalTransitions()`(`PlayerBaseState.cs:36`)——识破→冲刺→技能 Q/E,任意可操作态都能触发(dash/dead/stunned/counter/heal 自己屏蔽);②地面/空中专属过渡(`Player_GroundedState.cs:15` / `Player_AiredState.cs:15`)——掉落、普攻、格挡、跳跃、处决/空中移动;③各态自身收尾逻辑。统一回家口子是 `PlayerController.GroundedOrFall`(`:31`):落地回 idle,半空回 fall。
- **连段**(`Player_AttackState.cs`):`Enter` 按 `comboStep` 设 `AttackStep` 整型驱动 animator,`comboResetTime` 超时归零。命中由动画事件 `OnHitFrame`→`DoHit` 做 `OverlapBoxAll`(hitbox = `hitboxOffset/Size`,朝向翻转),对 `EnemyBase.TakeDamage(dmg, poise)`,并 `Stats.OnAttackHit()` 把虚血赎回一点。攻击中再按左键 → `comboQueued`,动画结束时经 `EnterAttackStateWithDelay`(`PlayerController.cs:178`,延一帧确认仍在攻击态)接下一段。巧妙点:whoosh 音效延到首个 `Update`(在 global 过渡之后)才播 —— 被识破/冲刺秒断的"幽灵攻击"不出声、不推进连段(`Exit` 里用 `!whooshPending` 判定)。
- **格挡/识破/处决三段防守循环**:格挡(`Player_BlockState.cs`)每次"按下边沿"`ArmWindow` 开一个完美窗,仿只狼防刷——连按缩窗(`spamStacks`)、静默或成功精防清零;敌人命中时由 `EnemyBase` 反调 `ReceiveBlockHit`→`ReceiveAttack`,窗口内=完美(回虚血+攒体力+削敌韧+特效),窗口外=普通格挡(伤害转虚血)。识破(`Player_CounterState.cs`)是按住右键再点左键触发的反击态,窗口由动画事件 `OnCounterWindowClosed` 关闭;敌人危招(unblockable)命中时反调 `TryCounter`,成功则 `enemy.OnCountered()` 打断敌人攻击。处决(`Player_ExecuteState.cs`)按 R,对范围内 `IsExecutable` 的敌人 `OnExecuted`,自带无敌帧+收尾后摇无敌(`SetInvulnerableFor`)。
- **数值/虚血系统**(`PlayerStats.cs`):普通格挡把伤害按 `ghostHPBlockRatio` 转成"虚血"(掉真血但可赎回);完美格挡/识破/攻击命中调 `RedeemGhostHP` 把虚血变回真血。体力(stamina)起始为 0,只能靠完美格挡/识破攒,供技能消耗。`TakeDamage(负数)` 当回血用(治疗就走这条)。HP 归零→`Die()`→ 触发 `OnDeath`(状态机切 `deadState`)+ `GameManager.TriggerGameOver()`。
- **跨场景常驻**:`Awake` 里 `DontDestroyOnLoad` + 单例。新场景如果又有个玩家预制,重复实例不创建第二个,而是把常驻玩家挪到这个出生点后自毁(`PlayerController.cs:103`)。回主菜单/Bootstrap 这种非游玩场景则销毁常驻玩家(`OnSceneLoadedCleanup`),保证下次开局全新。带死亡态进新场景(读档/重生)会自动 `Revive`。

**入口 & 触发**
- 创建:玩家预制摆在游玩场景里,`Awake` 自注册单例并 `DontDestroyOnLoad`;`Stats/Input/SkillManager` 在 `Awake` 抓取,状态机初始化为 `idleState`。
- 玩家操作触达:WASD/方向键→移动态;空格/W→跳;Shift→冲刺;鼠标左→普攻/连段;右键按住→格挡、右+左→识破;R→处决;Q→突刺斩、E→治疗(经 `SkillManager.TryUseSkill`)。所有按键先过 `PlayerInput.InputBlocked`(暂停/商店/对话/boss 演出时全屏蔽)。
- 动画驱动:Unity 动画事件由 `PlayerAnimationEvents` 转发 → `PlayerController.AnimFinished/AnimHitFrame/AnimCounterClosed` → 当前态的虚回调。退出态、判定命中帧、关识破窗都靠这套(带 `stateTimer` 兜底,防动画事件漏触发卡死)。

**依赖 & 被依赖**
- 依赖:`Entity`/`EntityState`/`StateMachine`(`Assets/Scripts/StateMachine/`)作为通用基座;`Skill_Base`/`Player_SkillManager` + `PlayerSkillType`(`Assets/Scripts/Skills/`)做技能;`AudioManager`、`CameraShake`、`Hitstop`、`VfxManager`、`DamageFeedback`、`CombatSignals` 做表现反馈;`GameManager`(暂停/GameOver)、`ShopUI/CharacterPanelUI/PauseMenu/VictoryUI/DialogueUI/BossIntroTrigger/BossFinishUI/MinotaurBoss`(`PlayerInput.cs:18` 用来判断是否屏蔽输入)。
- 被依赖:`EnemyBase.ApplyHitToCollider`(`Assets/Scripts/Enemy/EnemyBase.cs:393`)是敌人攻击玩家的统一入口,会读 `ctrl.IsBlocking/IsCountering` 并反调 `TryCounter/ReceiveBlockHit/Stun`,再 `stats.TakeDamage`;Boss 挑空攻击调 `BeginLift/SetLiftPosition/EndLift`、吼叫调 `knockedState`(经 `Stun`/状态切换)。UI 血条/体力条(`PlayerHealthBarUI`、`CharacterPanelUI`、HUD)订阅 `PlayerStats` 事件;`SaveSystem`/`CheckpointManager` 通过 `LoadBaseStats/LoadSavedVitals` 读写数值;`ShopSystem`/`EquipmentSystem`/`SkillSystem` 经 `ApplyStatBonus/SetEquipmentBonuses/SetSkillBonusPercent/AddGold` 改数值;交互/宝箱/处决提示 UI(`InteractTrigger`、`ChestInteract`、`ExecutePromptUI`)用 `PlayerController.Instance` 定位玩家。

**关键设计 / 易错点**
- 玩家、小怪、boss 共用同一套 `StateMachine`/`EntityState`(`StateMachine.cs:1` 注释明说),直接实例化通用 `StateMachine`(无命名子类,旧空壳子类已删),各自持有自己的 owner 引用。改通用状态机要顾及三方。
- "幽灵攻击"防护:连段推进、whoosh 音效都绑在 `whooshPending`/`!whooshPending` 上,目的就是让被识破/冲刺秒断的攻击不计连段、不出声 —— 动这块容易把识破打断玩坏。
- 所有战斗态都有 `stateTimer` 兜底退出 + 动画事件正常退出双保险,因为动画事件漏触发会卡死。`Execute/Heal/Counter` 都按这个模式写。
- `Player_KnockedState`/`Player_StunnedState` 故意不清水平速度,承接 `DamageFeedback.ApplyKnockback` 的击退滑行;而 idle/move 每帧重设速度会盖掉击退,所以 `EnemyBase` 推人前必须先 `Stun`(`EnemyBase.cs:423` 注释)。
- `Knocked` 和 `Heal` 复用同一个 `isHealing` 动画 bool(都为播 rest 动画),纯动画用途,别误以为击退态在回血。
- 击杀 boss 的处决要躲开通用顿帧/震屏(`Player_ExecuteState.cs:57`),否则会和 `BossFinishUI` 击破演出抢 `timeScale`。
- 数值改初始值不生效要先删 save.json(读档会覆盖 Inspector;见 `LoadSavedVitals`)。`CurrentStamina` 起始 0 是设计,不是 bug。
