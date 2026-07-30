## Skills 技能

**定位**:玩家技能系统。这个文件夹里其实并存着**两套互不相干、却共用"技能"名字的子系统**——(1) 战技(`Skill_Base` 家族):挂在 Player 身上的 MonoBehaviour 主动战技,玩家按 Q/E 触发动画+位移+伤害(突刺斩、治疗);(2) 学习型技能库(`SkillSystem` 单例 + `SkillData` ScriptableObject):一个常驻的、可在商店购买/被存档持久化的"已学技能"集合,目前只兑现成**被动加成**(攻/防百分比),主动型 `SkillData` 仅在角色面板里展示。理解本系统的关键就是分清这两条线。

**关键脚本**:
- `Assets/Scripts/Skills/Skill_Base.cs:5` —— 战技基类(MonoBehaviour)。统一管冷却(`OnCooldown`/`SetSkillOnCooldown`,基于 `Time.time`)、体力门槛(`staminaCost` + `CanUseSkill` 查 `player.Stats.HasStamina`)、图标/伤害倍率等通用字段。子类 override `TryUseSkill()` 写具体效果。
- `Assets/Scripts/Skills/Skill_DashStrike.cs:4` —— 战技:突刺斩。一大堆 `[SerializeField]` 参数(位移距离/时长、hitbox、前冲能量 VFX、动画 state 名+倍速)通过只读属性暴露给状态类读;`TryUseSkill` 只做一件事:`player.dashStrikeState.Configure(this)` + 切状态。带 `OnDrawGizmosSelected` 在编辑器里画 hitbox 预览(`Skill_DashStrike.cs:66`)。
- `Assets/Scripts/Skills/Skill_Heal.cs:5` —— 战技:治疗。参数(回血量、施法时长、HealAura 粒子调色/偏移);`TryUseSkill` 同样是 `Configure` + 切到 `healState`。
- `Assets/Scripts/Skills/SkillType.cs:5` —— 战技用的枚举 `PlayerSkillType { DashStrike, Heal }`。**刻意**取名 PlayerSkillType,以和学习库那套 `SkillType { Active, Passive }` 区分(注释明确写了 Dash/Counter/Execute 不走技能系统,留在 PlayerController 当普通动作)。
- `Assets/Scripts/Skills/SkillSystem.cs:6` —— 学习型技能库单例(`DontDestroyOnLoad`,放在 Bootstrap 场景,`Bootstrap.unity:601`)。持有 `List<SkillData> learnedSkills`,提供 `HasSkill`/`LearnSkill`/`LoadSkills`,并把被动加成汇总后推给 `PlayerStats`(`ReapplyPassives`/`GetPassiveBonusPercents`)。

相关但不在本文件夹、必须一起看的:
- `Assets/Scripts/Player/Player_SkillManager.cs:6` —— 战技**注册中心**,挂 Player 的 "Skills" 子节点。`Awake` 用 `GetComponentsInChildren<Skill_Base>()` 收集所有战技,`GetSkillByType`/`TryUseSkill(PlayerSkillType)` 按枚举派发。
- `Assets/Scripts/Data/SkillData.cs:6` —— 学习库的数据资产(SO),含 `saveID`(存档键)、`type`(Active/Passive)、`price`(商店价)、被动加成字段 `attackPercent`/`defensePercent` 等。
- `Assets/Scripts/Player/States/Player_DashStrikeState.cs:5`、`Player_HealState.cs:6` —— 战技真正的执行体(见下)。

**怎么工作**:

*第一套——战技(数据/状态分离的"配置-执行"模式)*:`Skill_X` 组件只是**一袋参数 + 一个触发器**,自己不写逻辑;真正的位移、伤害、VFX、状态锁全在对应的 `PlayerBaseState` 子类里。流程是 `Skill_DashStrike.TryUseSkill()` → `dashStrikeState.Configure(this)`(把自己塞进状态)→ `stateMachine.ChangeState(dashStrikeState)` → `SetSkillOnCooldown()`。进状态后:
- **突刺斩**(`Player_DashStrikeState`):Enter 时算起点/终点,用 `Physics2D.BoxCast` 过滤实心墙缩短终点(注释专门解释了用 `ContactFilter2D.useTriggers=false` 而非改全局 `queriesHitTriggers`,避免冲刺被"已开门"残留的 trigger 卡住,`Player_DashStrikeState.cs:41`);切 Kinematic + `SetInvulnerable(true)`;一次性 `OverlapBoxAll` 扫整条路径对 `EnemyBase.TakeDamage`;Enter 起一个 `VfxManager` `DashStrike` 前冲能量循环 VFX,Update 里 `Lerp` 推进位置。Exit 强制还原 Dynamic + 关无敌 + 回收该 VFX(旧 `Vfx_Afterimage` 残影已删)。
- **治疗**(`Player_HealState`):持续施法,Update 每帧把水平速度清零(锁移动),并订阅 `player.Stats.OnDamaged`——受击即标记 `interrupted` 打断不回血;`stateTimer` 到点才 `Stats.TakeDamage(-HealAmount)` 回血。靠基类的 `isHealing` animator bool 维持 rest 动画不被退出。

冷却与体力是基类统一兜底:`CanUseSkill` 检查冷却+体力,`SetSkillOnCooldown` 记录时间戳并 `SpendStamina`(目前两个战技的 `staminaCost` 默认 0,即不耗体力)。

*第二套——学习型技能库(被动加成的数据驱动)*:`SkillSystem` 是常驻单例。`learnedSkills` 里每个 `SkillData` 若 `type==Passive`,其 `attackPercent`/`defensePercent` 会被 `GetPassiveBonusPercents` 累加,再由 `ReapplyPassives` → `PlayerStats.SetSkillBonusPercent(...)` 应用。`PlayerStats` 把它当**基础值的百分比**算:`SkillAttackBonus => baseAttack * skillAttackPercent / 100f`(`PlayerStats.cs:40`),最终 `attack = base + equipment + skill`。因为玩家每次切场景都会重建,`SkillSystem` 监听 `sceneLoaded` 在 `OnSceneLoaded` 里重跑 `ReapplyPassives`,把加成重新打到新玩家身上(`SkillSystem.cs:35`)。`LearnSkill`/`LoadSkills` 改动后会触发 `OnSkillsChanged` 事件供 UI 刷新。

**两套的交界**:`SkillData` 里也有 Active 类型和 cooldown/damage 字段,但**目前没有任何运行时逻辑去执行 Active 型 SkillData**——它只在角色面板 `SkillsPageView` 里和战技一起被列出来展示(`SkillsPageView.cs:24` 把 `mgr.allSkills` 的战技 + `learnedSkills` 里的 Active 都归到"主动"分组)。也就是说:能真正打出来的主动技能 = 写死在 Player 上的 `Skill_Base` 战技;商店买来的 `SkillData` 目前只有被动能落地。

**入口 & 触发**:
- *战技*:`PlayerController.Awake/Start` 里 `new Player_DashStrikeState/Player_HealState` 并 `GetComponentInChildren<Player_SkillManager>()` 拿到 SkillManager(可空,`PlayerController.cs:132-136`)。玩家按键由 `PlayerBaseState` 的全局过渡处理:`input.Skill1Pressed`(Q)→ `SkillManager.TryUseSkill(PlayerSkillType.DashStrike)`,`input.Skill2Pressed`(E)→ `Heal`(`PlayerBaseState.cs:66-75`)。`Player_SkillManager.Awake` 自动收集 Player "Skills" 子节点下挂的 `Skill_X` 组件。
- *学习库*:`SkillSystem` 单例由 Bootstrap 场景里的物体创建(也有 `GetOrCreate()` 兜底自建,`SkillSystem.cs:40`)。学习入口是商店:`ShopSystem` 购买技能时 `SkillSystem.GetOrCreate().LearnSkill(entry.skill)`(`ShopSystem.cs:117`),购买前用 `HasSkill` 去重。读档时 `SaveSystem.LoadSkills(ResolveSkills(...))` 重建 `learnedSkills`(`SaveSystem.cs:286`)。

**依赖 & 被依赖**:
- *战技依赖*:`PlayerController`(拿 stateMachine —— 类型是通用 `StateMachine`、各 state、FacingDir、layer)、`PlayerStats`(`HasStamina`/`SpendStamina`/`SetInvulnerable`/`TakeDamage`/`OnDamaged`/`attack`)、`EnemyBase.TakeDamage`、`DamageFeedback.ApplyKnockback`、`VfxManager`(DashStrike/HealAura 粒子)、`AudioManager`(旧 `PlayerStateMachine` 子类与 `Vfx_SlashLine`/`Vfx_Afterimage` 均已删)。
- *战技被依赖*:`PlayerBaseState`(按键派发)、`Player_SkillManager`(注册+派发)、`SkillsPageView`(读 `mgr.allSkills` 列展示)。
- *学习库依赖*:`SkillData`(SO)、`PlayerStats.SetSkillBonusPercent`、`SceneManager.sceneLoaded`、`FindAnyObjectByType<PlayerStats>`。
- *学习库被依赖*:`ShopSystem`(`GetOrCreate`/`HasSkill`/`LearnSkill`)、`SaveSystem`(`Instance.learnedSkills` 取 saveID 存、`LoadSkills` 读)、`SkillsPageView`(列已学技能,按 `SkillType.Active/Passive` 分组)。

**关键设计 / 易错点**:
- **两套"技能"千万别搞混**:`PlayerSkillType`(战技,枚举,Player 身上)vs `SkillType`(学习库,Active/Passive,SO 上)。命名注释在 `SkillType.cs:3` 和 `SkillData.cs:3` 都提醒过。文件夹同名 `SkillSystem.cs`,但和 `Player_SkillManager` 完全是两回事。
- **战技 = 配置 + 执行分离**:`Skill_X` 不写逻辑,所有效果在 `Player_XState`;改数值改 Inspector,改行为改 State。加新战技要四件事:`PlayerSkillType` 加枚举值 → 写 `Skill_X : Skill_Base` → 写对应 State 并在 `PlayerController` 里 new + 字段暴露 → Player "Skills" 子节点挂上组件(`Player_SkillManager.cs` 头注释)。
- **冷却基于 `Time.time`、`lastTimeUsed = -cooldown` 初始化**,所以进游戏可立即用(`Skill_Base.cs:21`);冷却/体力扣减统一在基类,子类别忘了在 `TryUseSkill` 末尾调 `SetSkillOnCooldown()`。
- **被动加成是"基础值的百分比"不是固定值**:`baseAttack * percent / 100`,且必须经 `ReapplyPassives` 才会生效——切场景靠 `sceneLoaded` 自动重打,但如果在没有 `PlayerStats` 的时机调用会静默跳过(`SkillSystem.cs:99` 直接 return)。
- **`SkillData.saveID` 是存档主键**:存档存的是 ID 字符串而非引用,`SaveSystem` 靠它 resolve 回 SO,改 saveID 会让旧存档丢技能。
- **Active 型 SkillData 目前是"半成品"**:有数据、能买、能展示,但没有运行时执行路径。要让商店买的主动技真正能打,需要补一条 `SkillData` → 战技/状态的桥接逻辑(当前不存在)。
- 突刺斩撞墙检测特意用作用域级 `ContactFilter2D.useTriggers=false` 排除 trigger,别图省事改全局 `Physics2D.queriesHitTriggers`(会污染全局状态,`Player_DashStrikeState.cs:40-45`)。
