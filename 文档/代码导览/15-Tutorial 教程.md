## Tutorial 教程

**定位**:一套**数据驱动的关卡内教学引擎**,用最小侵入把"分步教学 → 玩家做对动作 → 解锁前进"串起来。它不锁输入、不暂停游戏、不写新战斗逻辑,而是**复用引擎现成的战斗信号 / 敌人能力 / 存档机制**,纯靠 4 个轻量组件在场景里拼出移动、攻击、格挡、识破、击败、拾取、装备等练习站。是新手引导关(Tutorial.unity)的骨架。

**关键脚本**(都在 `Assets/Scripts/Tutorial/`):
- `TutorialSequence.cs:8` —— **核心编排器**。挂触发区上,玩家踏入后逐条显示步骤、监听完成条件、做完一步推进下一步、全部完成后开门。所有教学站都用这一套。
- `TutorialGate.cs:7` —— **教学门(机关)**。一堵挡路墙,由对应 Sequence 完成后调 `Open()` 打开(关碰撞 + 升闸帧动画 + 穿门分层 + 开门音 + 存档持久化)。门只管"挡/开/演出",条件计数全在 Sequence。
- `TutorialEnemy.cs:7` —— **教学专属靶子行为**。把"被动木桩 / 钉死射手 / 打不死 / 推不动 / 门开后消失"这些只在教程用的开关收成一个组件,不污染核心 `EnemyBase`/`GroundEnemy`。
- `TutorialPromptUI.cs:6` —— **提示框 UI**(uGUI + TMP,木框画风)。静态单例接口 `Show/Hide/SetStatus`,显示标题、正文与进度计数行(如「格挡 2/3」)。

**怎么工作**:

核心是 `TutorialSequence` 的一个**步骤数组 + 游标推进**状态机。每个 `Step`(`TutorialSequence.cs:14`)声明:文案(title/body)、完成方式 `Kind`(`TutorialSequence.cs:11`:Info/Block/PerfectBlock/Counter/DefeatTarget/Pickup/EquipWeapon)、计数 `count`、计数标签 `label`、以及目标引用(targetEnemy/pickup/equipTarget)。

数据流 / 状态流转:
1. 玩家进触发区(`OnTriggerEnter2D` `TutorialSequence.cs:49`),按标签 `Tags.Player` 过滤。
2. **纯 info 单步站**(`IsInfoOnly` `TutorialSequence.cs:45`)走软提示分支:进区域 `Show`、出区域不强制隐藏、可反复触发,像个提示牌,没有门。
3. **多步站**:首次进入 `running=true` 后 `Advance()`(`TutorialSequence.cs:67`)。`Advance` 解绑上一步事件、游标 `idx++`、显示新步文案、`Hook(idx)` 按 `Kind` 订阅对应事件源。
4. **事件回调推进**:`Hook`(`TutorialSequence.cs:79`)把每种 Kind 接到现成信号——格挡/完美/识破接 `CombatSignals`,击败接 `EnemyBase.OnDied`,拾取接 `EquipmentPickup.PickedUp`,装备接 `EquipmentSystem.OnEquipmentChanged`。计数类(`Count` `TutorialSequence.cs:117`)累加 `progress`、刷新计数行,够 `count` 就 `Advance`;一次性类(击败/拾取)直接 `Advance`。
5. **就近判断**:计数类可设 `activeRange`,`PlayerNearby`(`TutorialSequence.cs:147`)用玩家与机关的 x 距离过滤,避免"在别处格挡也被算进这一站"。
6. 全部步骤走完 → `Finish()`(`TutorialSequence.cs:157`)隐藏提示并 `gate.Open()`。

两个**幂等/读档自洽**设计:
- `IsSatisfied(idx)`(`TutorialSequence.cs:134`):进入装备/拾取步时若条件**已满足**(武器已装、或读档后已拥有该装备/地上 pickup 已隐藏),立刻递归 `Advance` 跳过——避免卡在"捡不到的捡取步"。
- 门已开的回访:`OnTriggerEnter2D` 里若 `gate.IsOpen` 为真(手动档读回开门),直接 `finished=true` 不再提示(`TutorialSequence.cs:52`)。

`TutorialGate` 的演出与存档:`Open()`(`TutorialGate.cs:43`)幂等(`opened` 守卫),立刻关掉 `blockingCollider` 放行、触发 `Opened` 事件、播开门音,再走升闸帧动画 `PlayOpenFrames`(背景层=门自身 SpriteRenderer,前景层 `frontRenderer` 同步播放,玩家走过时被前景层盖住产生"穿门感")。`LoadOpened(bool)`(`TutorialGate.cs:61`)是**双向**恢复(开→停全开帧+放行,关→回闭合帧+挡路),为防御性自洽——目前教程恒整场景重载,关方向暂用不上。

`TutorialEnemy`(`TutorialEnemy.cs:20`)在 `Awake` 把开关翻译成现成能力:`passive→GroundEnemy.Passive`、`stationary→GroundEnemy.Stationary`、`immortal→EnemyBase.Invincible`、`immortal||immovable→Rb.constraints |= FreezePositionX`(免伤不挡击退,所以打不死靶还要单独冻结水平位置才不被推动)。并订阅关联门的 `Opened` 事件,在门开后 `Despawn`(`TutorialEnemy.cs:35`)隐藏自己。

**入口 & 触发**:全部在场景里**预置**(Tutorial.unity 编辑器构建),无运行时自动创建。触达链:
- 玩家**走进** `TutorialSequence` 的 trigger 区 → 显示提示。
- 玩家**做出目标动作**(格挡/识破/击败/捡起/装备)→ 对应事件触发回调 → 推进。
- 最后一步完成 → `Sequence` 调 `gate.Open()` → 门升起放行,关联练习敌人消失。
- `TutorialPromptUI` 在 `Awake`(`TutorialPromptUI.cs:17`)登记静态单例并初始隐藏,等首次 `Show`;切场景随场景销毁,`OnDestroy` 清单例。

**依赖 & 被依赖**:

它**用到**的系统:
- `CombatSignals`(`Assets/Scripts/Combat/CombatSignals.cs`)—— 静态战斗信号源。`Player_BlockState.cs:67/75` 发 `RaisePerfectBlocked/RaiseBlocked`、`Player_CounterState.cs:69` 发 `RaiseCountered`,Sequence 订阅这些事件计数。
- `EnemyBase`(`Assets/Scripts/Enemy/EnemyBase.cs`)—— 用其 `OnDied`(`:61`)做击败判定、`Invincible`(`:45`)做免伤、`Rb` 做冻结;`GroundEnemy.Passive/Stationary`(`Assets/Scripts/Enemy/Mobs/GroundEnemy.cs:35/38`)做靶子行为。
- `EquipmentPickup`(`Assets/Scripts/Interactables/EquipmentPickup.cs:10`)的 `PickedUp` 事件 + `EquipmentSystem`(`Assets/Scripts/Economy/EquipmentSystem.cs`)的 `OnEquipmentChanged`/`weapon`/`HasEquipment` —— 拾取与装备步判定。
- `SaveSystem`(`Assets/Scripts/Save/SaveSystem.cs`)—— 门开时 `MarkDoorOpened(SaveID)`(`:469`)持久化;读档时 `SaveSystem` 遍历 `TutorialGate` 调 `LoadOpened`(`:721-722`),与 `LockedDoor`/宝箱共用同一套 `runtimeOpenedDoorIDs` 已开集合。
- `SaveIdUtility.WithScene`(`Assets/Scripts/Save/SaveIdUtility.cs:25`)生成带场景前缀的稳定存档 ID;`Tags.Player`、`AudioManager.PlayDoorOpen`、`TMPro`。

**被依赖**:本子系统是教程关的叶子,基本**不被核心系统反向调用**。唯一外部主动调用是 `SaveSystem.LoadFromData` 对 `TutorialGate.LoadOpened` 的恢复(`SaveSystem.cs:721`)。`TutorialPromptUI` 仅被 `TutorialSequence` 用其静态 `Show/Hide/SetStatus`。

**关键设计 / 易错点**:
- **零侵入复用**是核心原则:教程不写新战斗/敌人逻辑,只订阅现成事件、只翻开现成开关。新增教学类型时,优先看能不能挂到已有信号上,再考虑加 `Kind`。
- **事件订阅必须配对解绑**:`Advance` 先 `Unhook(idx)` 再切步,`OnDisable`(`TutorialSequence.cs:164`)也 `Unhook(idx)`——静态事件(`CombatSignals`)若漏解绑会跨站串扰甚至泄漏到下场景,改 `Hook/Unhook` 时务必两边 `switch` 分支对称。
- **"打不死"≠"推不动"**:`Invincible` 只免伤,玩家攻击仍会调 `ApplyKnockback`,所以格挡/识破靶必须额外 `FreezePositionX`(`TutorialEnemy.cs:28`)才不会被推走——这是注释里专门标注的坑。
- **就近判断默认关**:`activeRange=0` 表示不限距离,任何格挡都计数;需要"只认本站附近"时要显式设范围,否则多站会互相抢计数。
- **幂等读档**:`IsSatisfied` 跳过已满足步、门已开则跳过提示、`Open()`/`LoadOpened` 的状态守卫,共同保证读档后重进教程区不会卡死或重复触发。`Block` 同时订阅 `Blocked` 与 `PerfectBlocked`(普通/完美都算),`PerfectBlock` 只订阅 `PerfectBlocked`——两类要分开计数靠的就是发信号端的拆分(`Player_BlockState` 完美格挡只发完美信号)。
- **UI 是软提示**:`TutorialPromptUI` 不暂停不锁输入,纯 info 站出区域也不强制隐藏(靠下一站的 `Show` 覆盖或 `Finish` 的 `Hide`),设计上像"飘在场景里的提示牌"。
