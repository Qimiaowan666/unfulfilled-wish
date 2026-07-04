## Audio 音频
**定位**:全局唯一的音频中枢。整个游戏所有 SFX(攻击/受击/格挡/脚步/UI/世界交互/敌人/Boss)和 BGM(菜单/区域/Boss 各阶段)都通过它一个单例播放;它同时是音量设置的运行时落点。是一个纯"被调用"的服务,不含任何游戏逻辑。

**关键脚本**:
- `Assets/Scripts/Audio/AudioManager.cs:6` —— 唯一的脚本。`MonoBehaviour` 单例(`Instance`,`Audio/AudioManager.cs:8`),Inspector 上挂着一大堆 `AudioClip` 字段(按 `[Header]` 分组:Attack/Guard/Hit/Execute/Movement/Misc/World/Enemy/Boss/BGM),对外暴露一组语义化的 `PlayXxx()` 方法。

**怎么工作**:
- **两条独立音轨**:`Awake` 里运行时 `AddComponent` 出两个 `AudioSource`(`Audio/AudioManager.cs:104-111`)。`source` 走 `PlayOneShot` 播一次性 SFX;`bgmSource` 设 `loop=true` 专放背景乐。两者 `spatialBlend=0`(纯 2D 不做空间衰减)。
- **SFX = PlayOneShot + 全局音量**:所有 `PlayXxx()` 最终都汇聚到 `Play(clip)`(`Audio/AudioManager.cs:164`),内部 `source.PlayOneShot(clip, sfxVolume)`。带系数的重载 `Play(clip, volumeScale)`(`:171`)允许个别音(如轻箭 0.7、Boss 砸字 0.5)比全局更轻/响。这意味着同一帧多个音可叠播,不会互相打断。
- **BGM = 切 clip 不重复重启**:`PlayBGM(clip, restart)`(`Audio/AudioManager.cs:145`)的关键设计:`restart=false` 时若当前正放同一首就直接 return,保证过场不打断连续 BGM;`restart=true`(读档专用)强制从头重播。
- **按场景自动选曲**:`Awake` 订阅 `SceneManager.sceneLoaded`(`:113`),每次进场景调 `PlayDefaultBGMForScene`(`:132`)。`MainMenu` 放 `menuBGMClip`,其余场景默认放区域曲 `bgmClip`;**Boss 曲故意不在这里放**,只在 Boss 吼叫开打那刻由 `BossIntroTrigger` 显式触发(注释见 `:140`)。
- **延迟/兜底/序列三个小技巧**:
  - `PlayDelayed`(`:196`)用协程 + `WaitForSecondsRealtime` 错开两声(如识破 `PlayCounter` 延后 0.08s、开宝箱金币声延后 0.28s)——用 Realtime 是因为 hitstop/暂停会把 `Time.timeScale` 压到接近 0。
  - 大量 `PlayXxx` 带 `clip != null ? clip : 兜底clip`(如 `PlayBossRoar` 空了回退 `phaseChange`、UI 各声回退 `uiClickClip`),少配资源也不会哑。
  - `PlayBossExplode`/`PlayBossRageRoar`(`:240-241`)**返回所播片段的 `clip.length`**,让 Boss 转阶段协程能 `yield return new WaitForSeconds(len)` 把演出按音长串起来。
- **脚步节流**:`PlayFootstep`(`:249`)有 0.15s 最小间隔,防止动画事件高频触发把脚步声糊成一片。

**入口 & 触发**:
- **创建**:作为常驻单例摆在 `Assets/Scenes/Bootstrap.unity`(符合项目"DontDestroyOnLoad 单例进 Bootstrap"约定),`Awake` 里 `SetParent(null) + DontDestroyOnLoad` 跨场景存活,重复实例自毁(`:96`)。
- **音量初始化**:`Awake` 从 `PlayerPrefs` 读上次保存的 `KeyBgm`/`KeySfx`(`:101-102`)。
- **谁来调**:约 40 个文件持 `AudioManager.Instance?.PlayXxx()`(全程用 `?.` 空安全),典型触达路径——
  - **玩家行为**:动画事件 `PlayerAnimationEvents.cs:27` → 脚步;各 Player 状态 `Player_JumpState/DashState/AttackState/...` → 跳/冲/挥砍音;`PlayerStats.cs:301` → 死亡音。
  - **Boss 流程**:`BossIntroTrigger.cs:87` 遭遇即 `StopBGM`,`:205` 开打放 `PlayBossBGM`;`MinotaurBoss.cs:189` 转二阶段切 `PlayBossPhase2BGM`;`StepMovers.cs:79` 闪现音等。
  - **世界/UI**:`CheckpointManager`、`LockedDoor`、`ChestInteract`、`ShopUI`、`PauseMenu`、`CharacterPanel/Views/*` 等各自触发存档/开门/开箱/购买/点击/装备音。
  - **读档**:`SaveSystem.cs:419` 调 `RefreshSceneBGM()` 强制 BGM 从头(同场景读档没有 sceneLoaded 事件)。

**依赖 & 被依赖**:
- **它用到**:`GameSettings`(读 `KeyBgm`/`KeySfx` 常量)、Unity 的 `SceneManager`、`PlayerPrefs`、`AudioSource`。无其它游戏系统依赖,刻意零耦合。
- **被谁用**:几乎全项目。`Core/GameSettings.cs:45,52` 的 `SetBGMVolume/SetSFXVolume` 调本类同名方法把设置面板的值实时写进来(双向:`GameSettings` 存 PlayerPrefs + `AudioManager` 改运行时音量);Player/Enemy/Boss 各状态、所有 UI 面板、世界交互物、存档系统都是消费方(见上)。

**关键设计 / 易错点**:
- **加音 = 改两处**:加新音效要(1)在对应 `[Header]` 加 `public AudioClip` 字段、(2)加一个语义化 `PlayXxx()` 包装。调用方永远调 `PlayXxx()` 而非直接传 clip,音效命名/兜底逻辑集中在此。
- **必用 `Instance?.`**:Bootstrap 没先加载(单独跑某场景)时 `Instance` 为 null,所有调用都靠 `?.` 兜空,别写裸 `Instance.Play`。
- **延迟音用 Realtime**:`PlayDelayed` 走 `WaitForSecondsRealtime`,因为 hitstop/暂停会冻结 `Time.timeScale`——这也是 `VictoryUI.cs:44` 注释强调"AudioSource 不受 timeScale 影响"能在 `timeScale=0` 时照响的原因。
- **同曲不重启是默认**:`PlayBGM` 默认不重启同一首是有意为之(过场连续);需要从头(读档)必须走 `restart=true`/`RefreshSceneBGM`,直接 `PlayBGM` 不会生效。
- **Boss 曲生命周期是手动编排**:区域曲交给场景默认逻辑,Boss 曲全靠 `BossIntroTrigger`/`MinotaurBoss`/`BossFinishUI` 在 Stop→BossBGM→Phase2BGM→AreaBGM 之间手动切,AudioManager 自己不知道"现在该不该放 Boss 曲"。
- **字段曾改名**:`bossPhase2BGMClip` 带 `[FormerlySerializedAs("futureBossBGMClip")]`(`:84`),序列化兼容旧 prefab,改名时别丢这个特性。
- **AudioSource 是运行时建的**:不是 Inspector 预挂,`Awake` 里 `AddComponent` 出来,所以 Bootstrap 上的 GameObject 只需挂脚本和填 clip,不用手动加 AudioSource。
