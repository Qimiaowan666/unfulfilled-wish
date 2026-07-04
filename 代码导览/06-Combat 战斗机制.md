## Combat 战斗机制

**定位**:这个文件夹是战斗系统里两块"无状态机基建"的存放地——一块是魂类核心的**韧性(Poise)条**机制(`PoiseMeter`),负责把"累计削韧→破韧硬直→可处决"这条链路做成可挂在任意敌人身上的独立组件;另一块是**格挡/识破成功事件的全局广播中枢**(`CombatSignals`),把"玩家做对了某个防御动作"这件事从具体敌人/场景里解耦出来,供教程等逻辑订阅。注意:实际的伤害判定、攻击编排、状态机都不在这个文件夹,这里只放这两个被战斗各方共用的"零件"。

**关键脚本**
- `Assets/Scripts/Combat/PoiseMeter.cs:4` —— 韧性条 MonoBehaviour。维护 `CurrentPoise`,提供 `TakePoiseDamage / ResetPoise`,暴露 `IsBroken`(`CurrentPoise <= 0`)以及 `OnPoiseBroken / OnPoiseChanged` 两个事件;内置"受击后延迟 `regenDelay` 秒再以 `poiseRegenRate` 回复"的自愈逻辑(`PoiseMeter.cs:20`)。
- `Assets/Scripts/Combat/CombatSignals.cs:3` —— 纯静态事件总线。三个事件 `Blocked / PerfectBlocked / Countered` + 对应 `Raise*` 方法(`CombatSignals.cs:9`)。无任何状态,只做"发→订"。

**怎么工作**

*韧性链路(PoiseMeter):*
1. 削韧入口走两条:`TakeDamage(damage, poiseDamage)` 里带伤害一起削韧(`EnemyBase.cs:455`);或纯削韧不掉血的 `ApplyPoiseDamage`(`EnemyBase.cs:437`,完美格挡反震专用)。两者最终都调 `poiseMeter.TakePoiseDamage`。
2. `TakePoiseDamage(PoiseMeter.cs:33)`:扣 `CurrentPoise`,把 `regenTimer` 重置成 `regenDelay`(每次挨削都刷新延迟,所以连段中不会回韧),触发 `OnPoiseChanged`;若这一下削到 0,触发一次性的 `OnPoiseBroken`。
3. `OnPoiseBroken` 被 `EnemyBase.Awake` 订阅并转给虚方法 `OnPoiseBroken()`(`EnemyBase.cs:86 / 492`),`GroundEnemy`(`GroundEnemy.cs:151`)和 `MinotaurBoss`(`MinotaurBoss.cs:286`)各自重写 → 切到硬直状态(StunnedState)。破韧期间 `IsBroken` 为 true,`Update` 的回韧逻辑直接 `return`(`PoiseMeter.cs:22`),韧性卡在 0,这段就是处决窗口。
4. 退出硬直时调 `ResetPoise()` 把韧性满血复位(小怪 `Enemy_StunnedState.cs:33` 调 `enemy.ResetPoise()`,Boss 在 `Boss_StunnedState.cs:32`),避免韧性条永久卡空。

*处决判定:* `EnemyBase.IsExecutable => PoiseMeter.IsBroken && CurrentHP > 0`(`EnemyBase.cs:52`)。这是 poise 机制对外暴露的"是否可处决"布尔,被处决提示 UI、玩家蹲伏/处决态轮询。

*格挡信号(CombatSignals):* 玩家 `Player_BlockState.ReceiveAttack` 命中:落在完美窗口内发 `RaisePerfectBlocked`(`Player_BlockState.cs:67`),普通格挡发 `RaiseBlocked`(`Player_BlockState.cs:75`);识破态 `Player_CounterState.cs:69` 发 `RaiseCountered`。订阅方目前是教程序列 `TutorialSequence`(`TutorialSequence.cs:83-85`),按步骤类型(Block / PerfectBlock / Counter)挂/摘对应回调,做"玩家做对动作才放行"的关卡逻辑。设计上完美格挡只发 `PerfectBlocked` 不发 `Blocked`,让教程能把"普通/完美"分开计数。

**入口 & 触发**
- `PoiseMeter` 由 Inspector 直接挂在敌人 prefab 上(`SamuraiElite / DemonSamurai / Archer / AoTengu / MinotaurBoss` 等 prefab 都含此组件),并由 `EnemyBase` 用 `[RequireComponent(typeof(PoiseMeter))]`(`EnemyBase.cs:5`)强制要求;`Awake` 里 `GetComponent` 拿到并订阅 `OnPoiseBroken`(`EnemyBase.cs:85`)。玩家行为触达:挥砍命中削韧、完美格挡反震削韧、破韧后被处决。
- `CombatSignals` 是静态类无需创建;触发点全在玩家防御态(`Player_BlockState` / `Player_CounterState`),玩家成功格挡/弹反/识破即触达。

**依赖 & 被依赖**
- `PoiseMeter` 依赖:仅 UnityEngine,自包含。被依赖:`EnemyBase`(订阅破韧、`ApplyPoiseDamage`、`ResetPoise`、`IsExecutable`)、`GroundEnemy` / `MinotaurBoss`(重写 `OnPoiseBroken`)、三个血条 UI(`PlayerHealthBarUI.cs:71` / `EnemyHealthBarUI.cs:28` / `BossHealthBarUI.cs:67` 订阅 `OnPoiseChanged` 画韧性条)、处决相关(`ExecutePromptUI`、`Player_GroundedState`、`Player_ExecuteState` 经 `IsExecutable`)。
- `CombatSignals` 依赖:无。被依赖:发布方 `Player_BlockState` / `Player_CounterState`;订阅方 `TutorialSequence`。

**关键设计 / 易错点**
- **`regenTimer` 在每次 `TakePoiseDamage` 都被重置**(`PoiseMeter.cs:37`):连续挨打不会回韧,是有意为之,但意味着只要被持续骚扰就一直破不了的反面——也一直回不了。
- **破韧后必须 `ResetPoise` 才能再次受削**:`IsBroken` 时 `TakePoiseDamage` 直接 `return`(`PoiseMeter.cs:35`),`Update` 也不回韧。若某条硬直退出路径忘了 `ResetPoise`,韧性条会永久卡 0(注释 `Enemy_StunnedState.cs:33` 专门点了这个坑)。所以"凡是离开硬直都要复位韧性"。
- **`OnPoiseBroken` 是一次性边沿事件**(只在削到 0 的那一下触发,`PoiseMeter.cs:39`),不是持续状态;持续的"可处决"要查 `IsBroken`/`IsExecutable`,别去重复监听破韧事件。
- **`IsExecutable` 里用 `GetComponent<PoiseMeter>()` 而非缓存字段**(`EnemyBase.cs:52`),被处决逻辑高频轮询,每次都跑组件扫描——`代码审计报告.md:480` 已标记为待优化(`EnemyBase` 已在 `poiseMeter` 缓存,应改用)。`Boss_StunnedState.cs:32` 同样有 `GetComponent<PoiseMeter>()?.ResetPoise()` 的冗余,应改用 `boss.ResetPoise()` 走缓存。
- **`CombatSignals` 是全局静态事件,无自动反订阅**:订阅方(如 `TutorialSequence`)必须自己在退出时 `-=`(`TutorialSequence.cs:98-100`),否则会在场景重载/对象销毁后残留悬挂回调。
- **完美/普通格挡信号互斥**:完美只发 `PerfectBlocked`、不发 `Blocked`(`Player_BlockState.cs:67/75`);教程里若想"完美也算一次格挡",得同时订 `Blocked` 和 `PerfectBlocked`(`TutorialSequence.cs:83` 就是这么做的)。
