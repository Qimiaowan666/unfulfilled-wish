## Util & Editor 工具/特性/编辑器扩展

**定位**:一组横切性的"基础设施"——运行时通用工具(按权重抽签)、集中常量表(标签/层/场景名)、自定义 Inspector 特性(MinMaxSlider / SubclassSelector),以及一批纯编辑器扩展(开发期一键搭建、Play 启动流程、特性对应的 PropertyDrawer)。它本身不实现任何玩法,而是让数据驱动的攻击系统在 Inspector 里"可配",让常量改一处全生效,让开发期搭场景/测当前场景更顺手。

**关键脚本**

运行时(`Assets/Scripts/Util/`,无 `#if UNITY_EDITOR`,会进包):
- `Util/WeightedPicker.cs:3` —— 静态 `WeightedPicker.Pick(float[] weights)`:按权重比例随机抽一个索引,全 0/负返回 -1。唯一调用方是敌人攻击选段(`Enemy/EnemyBase.cs:137`)。
- `Util/GameConstants.cs:3` —— 集中常量表,三个静态类:`Tags`(`Player`)、`Layers`(`Ground`/`Player`)、`SceneNames`(四个场景名 + `nonGameplay` 白名单 + `IsNonGameplay`/`IsGameplay` 判定,`GameConstants.cs:22-31`)。把散落的裸字符串收口,改一处全生效。
- `Util/MinMaxSliderAttribute.cs:5` —— `PropertyAttribute` 子类,带 `min`/`max` 两个只读字段。标在 `Vector2`(x=min, y=max)上,把一对区间画成单条双手柄滑块。
- `Util/SubclassSelectorAttribute.cs:6` —— 空 `PropertyAttribute`,纯标记。给 `[SerializeReference]` 多态字段加"类型下拉选择器"用。

编辑器(`Assets/Scripts/Editor/`,大多包 `#if UNITY_EDITOR`,不进包):
- `Editor/MinMaxSliderDrawer.cs:6` —— `[CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]`,把 `Vector2` 画成"左数字框 | 双手柄滑块 | 右数字框";类型用错时退回默认绘制(`MinMaxSliderDrawer.cs:11`)。
- `Editor/SubclassSelectorDrawer.cs:10` —— `[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]`,`[SerializeReference]` 字段的核心绘制器:第一行 label + 类型下拉,下面缩进展开选中类型的参数。下拉项靠 `TypeCache.GetTypesDerivedFrom` 自动列出所有非抽象子类(`SubclassSelectorDrawer.cs:101`),中文友好名在 `Nice` 字典(`SubclassSelectorDrawer.cs:13`)。
- `Editor/SetPlayModeStartScene.cs:8` —— `[InitializeOnLoad]` 静态类:开编辑器即把 Play Mode Start Scene 锁成 `Bootstrap.unity`,并在 `ExitingEditMode` 时把当前场景名写进 `SessionState`(键 `Bootstrap_ReturnScene`)。
- `Editor/CameraSetup.cs:8` —— 菜单 `Tools/Setup/Add Cinemachine Camera (Player Follow)`,一键搭 Cinemachine 相机(正交 + 横版阻尼:横快纵慢、关 Lookahead),自动绑场景里的 `PlayerController` 为 TrackingTarget。
- `Editor/AudioManagerSetup.cs:7` —— 菜单 `Tools/Audio/Setup AudioManager Clips` 和 `Tools/Audio/Validate Audio Setup`:用一张 `字段名→资产路径` 的字典(`AudioManagerSetup.cs:18`)批量把 wav/ogg/mp3 通过 `SerializedObject` 赋给 `AudioManager.prefab` 的各 clip 字段;Validate 检查必填 clip、场景里恰好 1 个 `AudioListener`、至少 1 个 `AudioManager`。

**怎么工作**

可分两条独立的线:

1. 数据驱动攻击的"可配 Inspector"链 —— 这是本子系统在玩法侧的主要价值。攻击系统(`Enemy/`)用 `[SerializeReference, SubclassSelector]` 暴露多态字段(`Enemy/EnemyBase.cs:179` 的 `preMove`、`:181` 的 `driver`)。`SubclassSelectorAttribute` 只是标记,真正干活的是 `SubclassSelectorDrawer`:它读 `fieldInfo.FieldType` 拿基类,`TypeCache.GetTypesDerivedFrom(baseType)` 枚举所有非抽象子类生成下拉菜单;选中后通过 `Activator.CreateInstance(t)` 实例化并写回 `managedReferenceValue`。注意菜单回调延后执行,`SerializedProperty` 句柄可能失效,所以 `Set` 用 `serializedObject + propertyPath` 重取(`SubclassSelectorDrawer.cs:112-118`)。同理 `MinMaxSlider` 特性 + Drawer 让 `Enemy/EnemyBase.cs:195`/`:197` 的 `range`(0–30)、`hpPercent`(0–1)在 Inspector 上变成双手柄滑块。设计意图:新增一个攻击位移/编排类只要标 `[Serializable]`,下拉里自动多一项、所有怪立即可用,无需改 Drawer(只需在 `Nice` 字典补个中文名)。

2. 编辑器开发期工具链 —— 与运行时解耦。`SetPlayModeStartScene` 和运行时的 `Bootstrapper` 构成一对握手:编辑器侧在按 Play 离开编辑模式前把"当前场景名"写进 `SessionState["Bootstrap_ReturnScene"]`,并强制所有 Play 都先进 `Bootstrap.unity`;`Bootstrap` 场景里的 `Bootstrapper.Start()`(`Core/Bootstrapper.cs:18`)读这个键,跳回开发者按 Play 前打开的那个场景——这样常驻单例(GameManager/AudioManager)永远先在 Bootstrap 完成 `DontDestroyOnLoad`,而开发者又能直接测当前场景,不被踢回 MainMenu。`CameraSetup`/`AudioManagerSetup` 则是纯一次性搭建/校验工具,通过 `SerializedObject` 改 prefab、`MenuItem` 触发。

**入口 & 触发**
- `WeightedPicker.Pick` 在运行时被敌人攻击选段调用(`Enemy/EnemyBase.cs:137`),玩家进入敌人攻击范围、敌人决定打哪一段时触达。
- `Tags`/`Layers`/`SceneNames` 常量遍布全项目,运行时各处直接引用(见下)。
- 两个 PropertyDrawer 由 Unity 在绘制带对应特性的 Inspector 字段时自动调用——开发者在 Inspector 看怪物攻击配置/连段区间时即触发,不被运行时代码调用。
- `SetPlayModeStartScene` 由 `[InitializeOnLoad]` 在编辑器加载/脚本重编译时自动构造;`OnPlayModeChanged` 由开发者按 Play 触发。
- `CameraSetup`/`AudioManagerSetup` 仅由开发者手动点 `Tools/...` 菜单触发,玩家行为永不触达。

**依赖 & 被依赖**

依赖(本子系统用到别的):
- `SubclassSelectorDrawer` 的 `Nice` 字典里硬编码了攻击系统的类型名(ApproachMover/JumpDriver/LungeForward 等,实体在 `Enemy/StepMovers.cs`、`Enemy/AttackDrivers.cs`),但只用于显示中文名;实际子类枚举走 `TypeCache`,新增类不改 Drawer 也能列出。
- `CameraSetup` 依赖 `Unity.Cinemachine` 包与运行时 `PlayerController`;`AudioManagerSetup` 依赖运行时 `AudioManager` 组件及其字段名/资产路径约定;`SetPlayModeStartScene` 依赖 `Bootstrap.unity` 存在并与 `Bootstrapper` 约定 `SessionState` 键名。

被依赖(别的系统反过来用它):
- `WeightedPicker` ← `Enemy/EnemyBase.cs:137`。
- `MinMaxSliderAttribute` ← `Enemy/EnemyBase.cs:195`/`:197`(`MinMaxSliderDrawer` 配套绘制)。
- `SubclassSelectorAttribute` ← `Enemy/EnemyBase.cs:179`/`:181`(`SubclassSelectorDrawer` 配套绘制)。
- `SceneNames` ← 大量调用方:`UI/PauseMenu.cs:175,465,466`、`UI/CharacterPanelUI.cs:89`、`UI/VictoryUI.cs:55`、`UI/GameOverUI.cs:66`、`Save/SaveSystem.cs:112,415`、`Player/PlayerController.cs:161`、`Core/GameManager.cs:48`。
- `Tags.Player` ← `Enemy/EnemyBase.cs:103`、`Enemy/Boss/MinotaurBoss.cs:141`、`BossFight/BossIntroTrigger.cs`(多处)、`Interactables/LockedDoor.cs:68`、`Interactables/InteractTrigger.cs:72,80`、`Tutorial/TutorialSequence.cs`、`Level/CameraFollowTarget.cs:26` 等。
- `Layers` ← `Enemy/StepMovers.cs:125`(`Ground`)、`Enemy/Mobs/GroundEnemy.cs:60`(`Ground`)、`Enemy/EnemyBase.cs:88`(`Player`)。
- `SetPlayModeStartScene` ↔ `Core/Bootstrapper.cs:18`(经 `SessionState["Bootstrap_ReturnScene"]` 间接耦合)。

**关键设计 / 易错点**
- 运行时与编辑器严格分家:`Util/` 的特性类(只是 `PropertyAttribute`)进包,`Editor/` 的 Drawer 用 `using UnityEditor`、`AudioManagerSetup`/`SetPlayModeStartScene` 还包 `#if UNITY_EDITOR`,确保不污染发布版。注意 `MinMaxSliderDrawer.cs`/`SubclassSelectorDrawer.cs` 本身没包 `#if`,但因放在 `Editor/` 文件夹(Unity 自动归到 Editor 程序集)同样不进包。
- `SubclassSelectorDrawer` 刻意不用 foldout(`SubclassSelectorDrawer.cs:8`):foldout 的整行点击区会把下拉按钮的点击吃掉,导致"选了类型却改不了"。这是踩过的坑,改回 foldout 会复现。
- 菜单回调里 `SerializedProperty` 句柄会失效,必须用 `serializedObject + propertyPath` 重取(`SubclassSelectorDrawer.cs:112`)——直接捕获 property 引用会报错或写错对象。
- 新增攻击位移/编排类型:只要标 `[System.Serializable]` 并继承对应基类,下拉自动出现;要中文名就去 `SubclassSelectorDrawer.cs:13` 的 `Nice` 字典补一行,否则回退显示类名。
- `MinMaxSliderDrawer` 只对 `Vector2` 生效,约定 x=min、y=max,且会 `Clamp` 保证 min≤max(`MinMaxSliderDrawer.cs:32-33`);标到非 Vector2 字段会退回默认绘制而非报错,容易标错而无提示。
- `SceneNames.nonGameplay` 是"非游玩场景"白名单(MainMenu/Bootstrap),很多系统靠 `IsNonGameplay`/`IsGameplay` 决定是否暂停、开角色面板、自动读档——新增菜单类场景务必同步加进这个数组,否则会在该场景里误触发游玩逻辑。
- `AudioManagerSetup` 的字典是字段名/资产路径的硬编码"真相源",`AudioManager` 改字段名或挪音频文件后要同步这里,否则 Setup 静默跳过(只 LogWarning 不报错)、Validate 才会暴露缺失。
