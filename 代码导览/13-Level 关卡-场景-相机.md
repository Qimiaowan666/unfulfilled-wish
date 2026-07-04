## Level 关卡/场景/相机

**定位**:负责"一个场景里玩家看到的世界怎么动"——相机怎么跟人、背景怎么铺满和滚视差、关卡 boss 战的胜利判定与切场景编排,以及编辑期摆复活点的可视化辅助。它是连接「玩家位置 / boss 死亡」和「镜头表现 / 流程推进」的胶水层,本身不持有战斗或存档逻辑,只做协调与表现。

**关键脚本**
- `Assets/Scripts/Level/LevelManager.cs:9` — 常驻关卡协调器(DontDestroyOnLoad 单例)。每次场景加载/读档后自动找本场景 boss,监测其死亡并按 boss 自带 `nextSceneOnDefeat` 切场景或触发通关演出。整套 boss 战流程的总闸。
- `Assets/Scripts/Level/CameraFollowTarget.cs:9` — 相机的实际跟随目标(空物体挂它,Cinemachine 的 TrackingTarget 指向它)。X 永远跟玩家,Y 用"纵向死区带"——跳上平台镜头不抬,只有冲出带才上下移。
- `Assets/Scripts/Level/ParallaxLayer.cs:7` — 单张背景 SpriteRenderer 的视差滚动,按 `parallaxFactor` 决定跟相机移动的比例(越小越远)。
- `Assets/Scripts/Level/BackgroundFit.cs:4` — 把一张背景图缩放到正好铺满相机视野,并锁定 X 跟随相机(Y/Z 不变),做无缝远景。
- `Assets/Scripts/Level/RespawnPointPreview.cs:7` — 纯编辑/调试辅助:用半透明主角贴图在复活点画个预览,方便肉眼摆位。`[ExecuteAlways]`,不影响运行逻辑。

**怎么工作**

*相机(三件套各管一段)*:
- `CameraFollowTarget` 是核心。`LateUpdate` 里先按 `Tags.Player` 找玩家并取其 `Entity`(`CameraFollowTarget.cs:26-31`);找到后**等真正落地**(`entity.IsGrounded`)才把当前 Y 锚成 `baseY`(`CameraFollowTarget.cs:32-42`)——这是为了避开传送/出生先悬在半空导致锚错高度。锚定后 `baseY` 固定不动:玩家 Y 与 `baseY` 之差在 `[-downRange, +upRange]` 内镜头 Y 不动,超出才按"超出量"上/下移(`CameraFollowTarget.cs:44-49`)。X 始终等于玩家 X。`captured` 在重新找到玩家(含重生重建)时复位,重新锚高度(`CameraFollowTarget.cs:30`)。`#if UNITY_EDITOR` 的 `OnDrawGizmosSelected` 把死区带画成 Scene 里的横线,方便调参(`CameraFollowTarget.cs:54-64`)。
- 该脚本本身只移动这个空物体,真正的相机由 Cinemachine 驱动:场景里 `CinemachineCamera.TrackingTarget` 指向挂了本脚本的物体(已确认 `ForsakenShrine.unity:16722` 等场景如此接线)。所以平滑/阻尼交给 Cinemachine,这里只决定"目标点在哪"。
- `BackgroundFit` 首帧(`fitted` 一次性)按 `cam.orthographicSize`、`cam.aspect` 与 sprite bounds 算出缩放铺满屏幕,之后每帧只同步 X(`BackgroundFit.cs:8-22`)。
- `ParallaxLayer` 在 `LateUpdate` 记录相机位移 `delta`,本层位移 `delta * (1 - parallaxFactor)`(`ParallaxLayer.cs:30-34`):factor=1 跟地面同滚(无视差),factor=0 完全不动(无穷远),>1 是前景。Y 默认不参与(`parallaxY`)。

*关卡流程(LevelManager)*:
单例在 `Awake` 抢占、`SetParent(null)+DontDestroyOnLoad` 常驻,并订阅两个事件(`LevelManager.cs:19-27`):`SceneManager.sceneLoaded`(切场景)和 `SaveSystem.AfterApply`(同场景原地读档没有 sceneLoaded,靠它补)。两条路最终都走 `RefreshBoss()`:停掉旧监测协程,用 `FindSceneBoss()` 找本场景里 `isBoss && activeInHierarchy && CurrentHP>0` 的敌人(`LevelManager.cs:66-72`),找到就起 `WatchBoss` 协程。场景加载路径多等两帧(`SetupSceneBoss`,`LevelManager.cs:44-49`),确保 SaveSystem 已把"已击败 boss"还原成 `SetActive(false)`,避免把死 boss 误判成存活。
`WatchBoss` 用 `WaitUntil` 挂起到 boss 为 null 或 HP≤0(`LevelManager.cs:74-83`),醒来后只在「对象还在 + HP≤0 + **仍 activeInHierarchy**」时判胜利——这是关键过滤:战斗打死会留着对象播死亡动画(active),而读档还原成已击败会 SetActive(false),借此区分"真的刚打死"和"读了通关档",杜绝开局误触发演出。
`OnBossDefeated`(`LevelManager.cs:85-105`)分两支:有 `nextSceneOnDefeat` → 延迟 `victoryDelay` 后 `GameManager.LoadScene` 切下一关(此时不存档,留到下一场景再存);没有(最终 boss)→ 先 `SaveSystem.Instance.AutoSaveAtPlayer()` 原子写一份完整自动档(boss 已死+背包+状态),再走 `BossFinishUI.Play` 九日式击破演出,完成回调 `ShowEnding` 让 `VictoryGate.Appear()` 出现通关门(没门则兜底直接 `VictoryUI.Show()`)。

**入口 & 触发**
- `LevelManager`:按记忆约定属于"常驻单例进 Bootstrap"那一类,挂在 Bootstrap 场景跨场景存活,无需每个战斗场景手动挂。触发点是引擎事件(场景加载)和 `SaveSystem.AfterApply` 事件,以及玩家把 boss 打到 HP≤0 这个战斗结果——玩家"击败 boss"这个行为最终触达它的胜利分支。
- `CameraFollowTarget` / `ParallaxLayer` / `BackgroundFit`:都是场景内组件,Unity 生命周期 `LateUpdate` 每帧自驱,无外部调用方。CameraFollowTarget 的输出经 Cinemachine TrackingTarget 间接驱动主相机。玩家移动/跳跃/坠落直接驱动这三者的表现。
- `RespawnPointPreview`:开发者在编辑器里挂载即生效(`OnEnable`/`OnValidate` 调 `Apply`),运行时也跑但只是显示。

**依赖 & 被依赖**

依赖(它用谁):
- `LevelManager` → `EnemyBase.isBoss / CurrentHP / nextSceneOnDefeat`(`EnemyBase.cs:13-14`)、`SaveSystem.AfterApply` 事件与 `AutoSaveAtPlayer()`(`SaveSystem.cs:209,139`)、`GameManager.LoadScene(string)`(`Core/GameManager.cs:80`)、`BossFinishUI.Play(boss, onComplete)`(`UI/BossFinishUI.cs:73`)、`VictoryGate.Appear()`(`BossFight/VictoryGate.cs:44`)、`VictoryUI.Show()`。
- `CameraFollowTarget` → `Tags.Player`(`Util/GameConstants.cs:5`)、`Entity.IsGrounded`(`StateMachine/Entity.cs:7`);运行时间接被 Unity.Cinemachine 消费。
- `ParallaxLayer` / `BackgroundFit` → 只用 `Camera.main`,无项目内依赖。
- `RespawnPointPreview` → `PlayerController`(`FindAnyObjectByType` 抓主角贴图)。

被依赖(谁用它):
- 没有别的 C# 类直接 `new`/引用这些 Level 脚本(`LevelManager` 是自管单例,其余靠场景挂载)。耦合点反过来在数据/事件层:`EnemyBase` 用 `isBoss`/`nextSceneOnDefeat` 字段"喂"LevelManager;`VictoryGate`、`BossHealthBarUI` 各自独立用同样的 `isBoss && active && HP>0` 规则找 boss(`VictoryGate.cs:36-42`、`BossHealthBarUI.cs:49-53`),与 LevelManager 并行而非调用它。

**关键设计 / 易错点**
- **死区带的 baseY 永不跟平台**:`CameraFollowTarget` 故意只跟"进场时锚的初始高度",跳上高台镜头不抬人只是靠上;改成跟随会失去魂类那种纵向稳定感。注意 `baseY` 只在落地一帧锚一次,且重生(重新找到玩家)才复位——若玩家从未落地(纯悬空场景)会一直走"落地前先跟玩家"分支不锚定(`CameraFollowTarget.cs:36-38`)。
- **WatchBoss 的"仍 active"过滤是防误触核心**:区分"战斗打死(留尸播动画,active)"vs"读档还原成已击败(SetActive false)"全靠这一个 `active` 判断(`LevelManager.cs:78-81`)。读档把死 boss 设为 inactive 的约定若被破坏,会导致一进场就放通关演出。
- **存档时机分叉**:有下一关的 boss **不在 OnBossDefeated 里存档**(注释 `LevelManager.cs:96-98`),否则"继续游戏"会落回已清空、无法再触发切场景的旧 boss 场景;只有最终 boss 才即时 `AutoSaveAtPlayer`。这是个容易踩的顺序坑。
- **同场景原地读档没有 sceneLoaded**:所以 LevelManager / VictoryGate 都额外订阅 `SaveSystem.AfterApply` 来重新接管 boss / 收放通关门,二者用同一事件保持一致。
- **BackgroundFit 假设单图铺满**:首帧按相机算缩放,之后不再重算;若运行中改相机正交尺寸或分辨率,背景不会重新 fit(`fitted` 锁死)。
- `RespawnPointPreview` 是纯编辑期辅助(半透明绿主角贴图),不要误当作运行时复活逻辑——真正的复活点放置/落地由别处负责,它只画个标记。
