## Core 核心/生命周期

**定位**:游戏的"骨架/总开关"层。负责启动引导(从 Bootstrap 场景进入第一个游玩/主菜单场景)、全局游戏状态机(运行中 / 暂停 / GameOver)、死亡-重生-重载场景的总调度,以及画面/音量等全局设置的读写。它本身不实现玩法,而是被 UI、玩家、存档等系统当作"全局裁判"来调用。

**关键脚本**
- `Assets/Scripts/Core/GameManager.cs:4` —— 全局单例。持有 `IsGameOver`/`IsPaused` 两个状态标志和 `OnGameOver`/`OnGamePaused`/`OnGameResumed` 三个事件;暴露 `TriggerGameOver()`(`:26`)、`TogglePause()`(`:33`)、`RestartScene()`(`:41`)、`LoadScene()`(`:80`)。它是死亡/暂停/换场景一切流程的中枢。
- `Assets/Scripts/Core/Bootstrapper.cs:8` —— 挂在 `Bootstrap.unity` 上的引导器。`Start()` 时跳转:发布版去 `firstScene`(默认 MainMenu),编辑器里则跳回"按 Play 前打开的那个场景"(从 `SessionState` 里读 `Bootstrap_ReturnScene`,`:18`),方便直接测当前场景。
- `Assets/Scripts/Core/PersistentEventSystem.cs:7` —— 保证全局只有一个 UI `EventSystem` 且跨场景常驻。挂在 Bootstrap 的 EventSystem 上,`Awake` 里做单例去重 + `DontDestroyOnLoad`(`:11`)。其它场景自带的 EventSystem 应删掉。
- `Assets/Scripts/Core/GameSettings.cs:7` —— 静态工具类。集中读写 `PlayerPrefs`(BGM/SFX 音量、全屏、分辨率),启动时用 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 自动应用上次保存的全屏/分辨率(`ApplyOnLaunch`,`:25`)。`SetXxx` 由暂停菜单的设置子页调用。

**怎么工作**
- 启动链路:`Bootstrap.unity` 是真正的入口场景。同场景里的常驻单例(GameManager / AudioManager / 这个 EventSystem)在 `Awake` 完成 `DontDestroyOnLoad`,`Bootstrapper.Start` 才发起首场景跳转。`GameSettings.ApplyOnLaunch` 在 `BeforeSceneLoad` 时机更早,确保画面分辨率在第一帧前就位(音量则由 AudioManager 自己读 PlayerPrefs)。
- 状态流转:`GameManager` 用两个 bool + 三个事件做最简状态机。`TogglePause()` 翻转 `IsPaused` 并直接写 `Time.timeScale`(0 暂停 / 1 恢复),再广播 paused/resumed 事件;`TriggerGameOver()` 用 `IsGameOver` 做幂等闸(已死就直接 return),只广播一次。注意它本身不弹任何 UI——UI 是订阅方。
- 死亡-重生总调度(`RestartScene`,`:41`)是这层最有设计意图的部分,分两条路:
  1. **教程场景就地复活**:若当前是 `Tutorial` 且 `RespawnInPlace()`(`:64`)成功,直接 return——不重载场景、不碰存读档。原地把玩家 `Stats.RestoreAll()` + `Revive()` 复位,并只对"会在检查点重生"(非 permadeath)的怪调 `Respawn()`;已击败的练习怪保持 `SetActive(false)` 不复活。这样教程里开过的门、过过的站在内存里原样保留,玩家不走回头路。
  2. **正式死亡重生**:调 `SaveSystem.Instance.PrepareRespawn()`(全局态回存档 + 落火堆复活点),然后读存档的 `sceneName`,加载"上次火堆所在场景";没存档就重载当前场景。
- 设置读写:`GameSettings` 全部走 `PlayerPrefs` + `Save()`,音量改完顺手调 `AudioManager.Instance?.SetXxxVolume` 立即生效;读取属性(`BGMVolume`/`SFXVolume`)以 AudioManager 的默认值兜底。

**入口 & 触发**
- 这些对象都不是代码 new 的,而是预先摆在 `Bootstrap.unity` 场景里,靠各自 `Awake` 做单例 + `DontDestroyOnLoad` 常驻(符合项目"常驻单例进 Bootstrap"的约定)。
- 玩家死亡:`PlayerStats.cs:303` 在 `Die()` 里调 `GameManager.Instance?.TriggerGameOver()`(任何场景死亡都触发,不依赖关卡级 LevelManager)。
- 暂停:`UI/PauseMenu.cs:112/183` 调 `TogglePause()`;`PauseMenu` 同时订阅 `OnGamePaused/OnGameResumed`(`:158-166`)开关面板。
- 重试/换场景:`UI/GameOverUI.cs:59` 调 `RestartScene()`,`:66` 调 `LoadScene(MainMenu)`;`PauseMenu`、`VictoryUI`、`MainMenuUI`、`SaveSystem.cs:416`、`LevelManager.cs:117`、`Interactables/SceneLoadTrigger.cs:62` 都通过 `LoadScene()` 切场景。
- 设置面板调 `GameSettings.SetBGMVolume/SetSFXVolume/SetFullscreen/SetResolution`(`UI/PauseMenu.cs`)。

**依赖 & 被依赖**
- GameManager 依赖:`SaveSystem`(`PrepareRespawn` `Save/Save.cs:251`、`Load` 取 `sceneName`)、`PlayerController.Revive`(`:167`)/`PlayerStats.RestoreAll`(`:267`)、`EnemyBase`(`Respawn()`、`RespawnsAtCheckpoint`(`Enemy/EnemyBase.cs:51`)、`Initialized`(`:66`))、`SceneNames`(`Util/GameConstants.cs:14`)。
- GameSettings 依赖:`AudioManager.Instance`(`Audio/AudioManager.cs:8`,音量实际生效与默认值兜底)。
- 被依赖:`PlayerStats`(GameOver)、`PauseMenu`/`GameOverUI`/`VictoryUI`/`MainMenuUI`/`CharacterPanelUI`/`ShopUI`(状态查询 + 事件订阅 + 换场景)、`SaveSystem`/`LevelManager`/`SceneLoadTrigger`(换场景)。`SceneNames.IsNonGameplay`(`Util/GameConstants.cs:24`)被各处用来判断"非游玩场景不暂停/不读档"。

**关键设计 / 易错点**
- 单例约定:GameManager / PersistentEventSystem 都不自动创建,必须实例摆在 Bootstrap 场景里;别在别处 new 或用 Resources 另起一个(见项目记忆"常驻单例进 Bootstrap")。`Awake` 里 `transform.SetParent(null)` 后再 `DontDestroyOnLoad`——子物体不能跨场景常驻,必须先脱父。
- 暂停恢复必须走 `TogglePause()`,不要在别处直接写 `Time.timeScale = 1f`,否则 `IsPaused` 状态与时间缩放会脱节(暂停菜单设计文档明确强调过这一点)。
- `RestartScene` 的教程分支是"省心但隐蔽"的特例:Tutorial 死亡完全不落盘、不重载,只在内存复活。改重生逻辑时容易忽略这条早 return,导致教程进度被意外重置。
- `RespawnInPlace` 与 `SaveSystem.RefreshRespawnableEnemies` 必须口径一致:只复活 `RespawnsAtCheckpoint`(即非 permadeath)的怪,permadeath 练习怪保持当前态——两边逻辑要同步改,否则会出现"该死的怪复活了 / 该复活的没复活"。
- `Bootstrapper` 的编辑器返回场景靠 `SessionState` 的 `Bootstrap_ReturnScene`,这是 `SetPlayModeStartScene` 在 `ExitingEditMode` 写入的;发布版没有这套机制,只会走 `firstScene`。
- `GameSettings` 全是静态 + PlayerPrefs,没有运行态对象;改了初始音量等默认值不一定立刻体现,因为已存的 PlayerPrefs 会覆盖默认值(类似"存档覆盖 Inspector 初始值"的坑)。
