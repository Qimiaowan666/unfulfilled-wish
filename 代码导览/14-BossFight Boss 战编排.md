## BossFight Boss 战编排

**定位**:负责"一场 boss 战的开场—封锁—结束"全流程编排。它本身不写战斗逻辑(那在 Enemy/Boss),只管类暗魂的仪式感:玩家踏入 boss 房 → 沉睡 boss 苏醒 + 登场演出 + 推镜吼叫 → 雾门封住出口 → 击破后开门/出现通关门。一组挂在场景物体上的"导演脚本",把相机/对话/音频/名牌/血条/雾门/通关 UI 串成一条时间线,并和存档读档保持一致。

**关键脚本**

- `Assets/Scripts/BossFight/BossIntroTrigger.cs:8` —— 登场总导演。挂在 boss 房入口触发区(`[RequireComponent(typeof(Collider2D))]`)。玩家首次进入触发 `IntroSequence()` 协程:推镜对准 boss → 对话 → 吼叫(名牌扫入/血条充满/震屏/zoom 抖/放射 FX)→ 拉回相机 → `StartCombat()` 正式开打。暴露静态 `Sequencing`(`:31`)供全局屏蔽输入/暂停。
- `Assets/Scripts/BossFight/BossFogGate.cs:6` —— 雾门(出口封锁)。开战 `Close()` 时启用实体碰撞挡路 + 雾墙淡入;boss 死亡/消失或读档重置时自动 `Open()`。挂在出口竖墙物体上。
- `Assets/Scripts/BossFight/EnemyClearGate.cs:10` —— 清场雾门(精英怪房)。和 BossFogGate 同构,但触发条件是"指定的一组 `enemies` 全部死亡",订阅每个怪的 `OnDied` 事件实时判定。和 boss 无关,用于小怪封锁房。
- `Assets/Scripts/BossFight/VictoryGate.cs:8` —— 通关门。继承 `InteractTrigger`,默认隐藏;最终 boss 击破演出后由 `LevelManager.ShowEnding()` 调 `Appear()` 出现 → 玩家走近按 F → `VictoryUI`。

**怎么工作**

核心是 `BossIntroTrigger.IntroSequence()`(`:84`)这条协程时间线:

1. **进场冻结**:置 `Sequencing=true`,静音 BGM,boss 转向玩家;清玩家速度、切回 idleState、`Rigidbody2D.simulated=false` 把玩家原地钉死(`:90-96`)。
2. **推镜(不硬切相机)**:不手动接管 CinemachineBrain,而是把场景里预摆的低优先级 `bossIntroVcam` 的 `Priority` 临时抬到 100(`:121`),让 Cinemachine 自己 blend 过去;同时把 Brain 的 `DefaultBlend` 临时改成 `camMoveDuration`(场景默认 2s 太慢)(`:103-112`)。等 `WaitForSecondsRealtime(camMoveDuration)`。
3. **对话**:调 `DialogueUI.Play(sequence, 回调)`,内部会把 `timeScale=0`,协程 `while(!dialogueDone) yield`(`:128-133`)。
4. **吼叫高潮**(`:135-160`):`AudioManager.PlayBossPhaseChange()` + `nameCard.Play(boss.profile, roarDuration)`(九日式黑条名牌)+ `Bar().Reveal()`(血条缓露充满)+ `BossScalePunch()`(boss squash 弹一下)+ `ScreenRoarFx.Burst()`(全屏黑色放射线)。`roarDuration` 内每帧对 `bossIntroVcam` 做衰减随机位移(震屏)+ 正弦 OrthographicSize 抖动(zoom punch)——注意是抖 **vcam** 而非直接动相机,避免破坏 blend。
5. **拉回 + 还原**:把 vcam 优先级降回 `prevPrio`,Cinemachine 平滑混回玩家 vcam,再还原 Brain 的 `DefaultBlend`(`:162-167`)。
6. **开打** `StartCombat()`(`:196`):清 `Sequencing`,恢复玩家 `simulated=true` 并清残速,`boss.Activate()`(把 `combatEnabled` 置 true 唤醒),`fogGate.Close()` 封门,`AudioManager.PlayBossBGM()` 起 boss 曲。

**雾门状态机**(BossFogGate / EnemyClearGate 同思路):一个 `closed`/`opened` 布尔,开=禁用挡路碰撞 + 雾墙 α→0,关=启用碰撞 + 雾墙 α→1;淡入淡出走协程且用 `Time.unscaledDeltaTime`(`:76`/`:89`),因为演出常把 timeScale 改了。BossFogGate 在 `Update()` 里持续看护 boss——一旦 boss 为 null / 失活 / `CurrentHP<=0` 就自动开门放玩家出去(`:36`)。EnemyClearGate 则靠 `OnDied` 事件 + `Start()`/`OnAfterApply()` 的 `Sync()` 双重对齐"怪是否全死"。

**读档一致性(本子系统的关键设计)**:四个脚本全部订阅 `SaveSystem.AfterApply`,在场景态恢复完后**双向**重判:
- BossIntroTrigger.`OnSaveApplied()`(`:46`):boss 被还原为存活 → 重置 `played=false`、`combatEnabled=false`、`Bar().HideForIntro()`,让登场能再次触发;boss 已死/不在 → `played=true` 永不再演。
- BossFogGate.`OnSaveApplied()` → `Open()`,之后是否再关由 IntroTrigger 重判。
- EnemyClearGate.`OnAfterApply()` → `Sync(false)` 静默对齐(读旧档怪复活则雾重新淡入挡住,不会泄漏成开)。
- VictoryGate.`OnSaveApplied()`(`:30`):场上有存活 boss → `Hide()`(防不打 boss 直接通关);无存活 boss → `Appear()`(防死 boss 无门卡死)。

**入口 & 触发**

- 这些组件都**预摆在场景里**(ForsakenShrine / Tutorial 等 boss/精英房),由 Inspector 接线,无运行时创建。
- BossIntroTrigger 在 `Start()`(`:35`)把 boss `combatEnabled=false` 先沉睡,并自动把自己的 boss 填进 `fogGate.boss`。登场由玩家 `OnTriggerEnter2D` 进触发区驱动(`:73`);另有 `Update()` 兜底:读档/复活点已落在 boss 一侧(`PlayerPastTrigger`)则跳过演出直接 `WakeImmediate()` 开打(`:61-71`)。
- VictoryGate 由 `LevelManager.ShowEnding()`(`Assets/Scripts/Level/LevelManager.cs:107`)在最终 boss 击破后调 `Appear()`;玩家走近按 F 触发基类 `InteractTrigger` 的交互流 → `Interact()` → `VictoryUI.Instance.Show()`。
- EnemyClearGate 由其 `enemies` 数组里任一怪 `OnDied` 触发判定,玩家"打光一房小怪"即开门。

**依赖 & 被依赖**

依赖(它用的):
- `EnemyBase`:`combatEnabled`/`Activate()`(`Assets/Scripts/Enemy/EnemyBase.cs:17-18`)、`CurrentHP`/`isBoss`/`profile`、`OnDied` 事件、`FaceToward()`(实为 `Entity.FaceToward`,`Assets/Scripts/StateMachine/Entity.cs:39`)。
- `SaveSystem.AfterApply` 静态事件(`Assets/Scripts/Save/SaveSystem.cs:209`)——读档一致性的核心钩子。
- Cinemachine(`CinemachineCamera`/`CinemachineBrain`/`CinemachineBlendDefinition`)、`DialogueUI.Play`、`BossNameCard.Play`、`BossHealthBarUI.Reveal/HideForIntro`、`ScreenRoarFx.Burst`、`AudioManager`(StopBGM/PlayBossPhaseChange/PlayBossBGM/PlayDoorOpen)、`PlayerController`、`InteractTrigger`(VictoryGate 基类)、`VictoryUI`。

被依赖(反过来用它的):
- `LevelManager.ShowEnding()` 调 `VictoryGate.Appear()`;`LevelManager` 注释明确 boss 曲由本子系统的 `BossIntroTrigger.StartCombat` 放、自己不放(`Assets/Scripts/Level/LevelManager.cs:59-61`)。
- `BossIntroTrigger.Sequencing` 被多处当"演出中"开关读取:`PauseMenu.cs:173`(禁暂停)、`PlayerInput.cs:21`(屏蔽输入)、`CharacterPanelUI.cs:80/93`(禁开面板)。

**关键设计 / 易错点**

- **靠 vcam 优先级 blend、不接管 Brain**:登场推镜全程不手动关 Brain、不直接 set 相机 transform,只抬/降一台预摆 vcam 的 Priority,交给 Cinemachine 平滑混合——避免"冷启动硬切突跳"。震屏/zoom 也是抖 vcam(`:147-153`)。取景和抖动基准都在那台 vcam 上,可在编辑器直接调。
- **演出期用 unscaled 时间**:对话会把 `timeScale=0`,所有雾门淡入淡出与等待须用 `WaitForSecondsRealtime` / `Time.unscaledDeltaTime`,否则演出中卡死。
- **EnemyClearGate 必须给怪勾 `permanentDeath`**:它自身不存档,开没开纯靠"怪是否已死"推导。怪若没勾永久死亡,读档/火堆复活后会被判成"还有活怪"→ 雾重新起、把玩家又关进去(脚本注释 `:19` 反复强调)。
- **`AfterApply` 双向对齐**:不是"读档只负责开门",而是开/关都重判。读"杀怪前旧档"会让雾门/通关门正确回到封锁态,防止状态泄漏成"已通关/已开门"。
- **boss 默认沉睡**:`EnemyBase.combatEnabled` 普通怪是 true,boss 在 IntroTrigger.`Start` 被睡掉,只有 `Activate()` 唤醒。改 boss 行为时若发现 boss 进场不动,先确认登场流程是否走到了 `StartCombat`。
- **`played` 的兜底两路**:正常 `OnTriggerEnter2D` 走完整演出;但若读档点/复活点已在 boss 一侧,`Update` 的 `PlayerPastTrigger` 会直接 `WakeImmediate` 跳过演出——避免"站在 boss 跟前还要回头触发触发区"的卡顿。
- BossFogGate 与 EnemyClearGate 高度同构(都是 blocker + fog + 协程淡变),但触发源不同(单 boss 看护 vs 一组怪 OnDied),不要混用。
