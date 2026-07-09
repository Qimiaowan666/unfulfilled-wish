## Save 存档

**定位**:整个游戏的存读档中枢。负责把"玩家(属性/血量/位置/金币)+ 背包/装备/技能 + 场景态(敌人死活/门开关/商店库存/已解锁火堆)"序列化成 JSON 落盘,并在场景加载、火堆休息、死亡重生、菜单读档时把它还原回去。魂类的"营火存档 + 死亡回火堆满血复活,场景敌人重刷,永久死亡的 boss/精英不刷"循环,核心逻辑就长在这里。

**关键脚本**
- `Assets/Scripts/Save/SaveSystem.cs:77` —— 单例核心。定义全部存档数据结构(`SaveData`/`EnemySaveData`/`DoorSaveData`/`ShopSaveData`,`SaveSystem.cs:11-75`),负责写档(`BuildSaveData`/`WriteSlot`,`SaveSystem.cs:148-195`)、读档分层 apply(`ApplyGlobalState`/`ApplySceneState`,`SaveSystem.cs:254-329`)、自动档 + 3 个手动槽 + 缩略图(`SaveSystem.cs:364-459`)。
- `Assets/Scripts/Save/CheckpointManager.cs:5` —— 火堆/复活点管理。维护"已解锁火堆 ID 集合"和"最后火堆 ID",`ActivateCheckpoint`(`CheckpointManager.cs:25`)是坐火堆的总入口:满血 + 刷新可复活敌人 + 写自动档 + 设复活点。
- `Assets/Scripts/Save/CheckpointTrigger.cs:4` —— 营火交互组件(继承 `InteractTrigger`)。玩家进范围按 F 调 `ActivateCheckpoint`,可挂 `respawnAnchor` 指定精确复活落点(`CheckpointTrigger.cs:20-24`)。
- `Assets/Scripts/Save/SaveIdUtility.cs:4` —— ID 工具。资产 ID(物品/装备/技能用 `saveID` 字段,缺省回退 `asset.name`,`SaveIdUtility.cs:6-21`)与场景物体 ID(层级路径 + `场景名:` 前缀防跨场景撞名,`SaveIdUtility.cs:25-48`)。

**怎么工作**

数据落盘:`BuildSaveData`(`SaveSystem.cs:154`)从 `PlayerStats` + 三大常驻单例(`InventorySystem`/`EquipmentSystem`/`SkillSystem`)+ 当前场景里 Find 到的敌人/门/商店,组装成一个 `SaveData`,`JsonUtility.ToJson` 写文件。注意它存的是"基础属性裸值"(`baseMaxHP` 减掉装备加成,`SaveSystem.cs:160`),避免读档时把装备 buff 重复叠进去。资产引用一律存字符串 ID,读档时 `ResolveAsset<T>`(`SaveSystem.cs:644`)从 `Resources/Data` 全量加载的缓存池里按 ID 反查(发布版可用,编辑器再兜一层 AssetDatabase)。

读档的核心设计是"全局态 vs 场景态分层 + 内存连续":
- **全局态**(`ApplyGlobalState`,`SaveSystem.cs:254`):玩家属性/血量/位置 + 背包/装备/技能。这些挂在 DontDestroyOnLoad 单例上,内存天然连续,所以**只在首次进入 / 显式读档 / 死亡重生时 apply 一次**,普通过门切场景绝不覆盖。判据是 `globalStateLoaded` 标志(`SaveSystem.cs:205`)。
- **场景态**(`ApplySceneState`,`SaveSystem.cs:314`):每个场景里的敌人/门/商店/火堆,每次进场景都恢复。
- 两者的开关由 `globalStateLoaded` / `nextLoadIsRespawn` 两个标志驱动。`RequestFullReload()` 把全局标志清掉强制重读(显式读档前调),`PrepareRespawn()` 额外把落点切到火堆复活点而非读档落点(死亡重生前调,`SaveSystem.cs:247-251`)。

`realLoad` 语义贯穿场景态恢复:真读档(继续/重生/手动读档)时清空内存集合 `runtimeDefeatedEnemyIDs`/`runtimeOpenedDoorIDs` 并从存档重建,让敌人死活/门开关回到存档状态;普通过门切场景则**保留**这两个内存集合(和背包一样内存连续),所以杀过的精英/开过的宝箱过场景不复活、回来不重刷(`ApplyEnemyStates`/`ApplyDoorStates`,`SaveSystem.cs:676-729`)。

死亡重生分支(`SaveSystem.cs:288-309`):`isRespawn` 时落 `respawnX/Y`(火堆点)且**强制满血满体力、虚血清零**——绝不读死亡瞬间存档里的 0 血,否则会 Die→Revive 后空血站着。

OnSceneLoaded 触发(`SaveSystem.cs:110`):每个 gameplay 场景加载后等一帧(让玩家/敌人 Awake 完)再 `ApplyOnSceneLoaded`,不再依赖场景里挂没挂 BossBattleManager。场景态 apply 完会触发静态事件 `AfterApply`(`SaveSystem.cs:209`),供 boss 登场/血条/雾门/通关门等在"同场景原地读档(没有 sceneLoaded)"时重新接管。

**入口 & 触发**
- 创建:`SaveSystem` 与 `CheckpointManager` 都是 DontDestroyOnLoad 单例,放在 Bootstrap 场景常驻(`SaveSystem.cs:91`、`CheckpointManager.cs:17`)。
- 坐火堆:`CheckpointTrigger.Interact`(F)→ `CheckpointManager.ActivateCheckpoint`(`CheckpointManager.cs:25`)→ `SaveSystem.Save`(写自动档)。boss 战期间禁用(`EnemyBase.AnyBossInCombat()`,`CheckpointManager.cs:27`)。
- 死亡重生:`GameManager.RestartScene`(`Core/GameManager.cs:41`)→ `PrepareRespawn()` + 加载"上次火堆所在场景"。教程场景特例:就地复活不碰存读档(`GameManager.cs:48`)。
- 主菜单:`MainMenuUI.NewGame` → `ResetForNewGame()`(`UI/MainMenuUI.cs:52`);`Continue` → `RequestFullReload()` 后进存档场景(`MainMenuUI.cs:58-60`)。
- ESC 菜单手动存读:`PauseMenu` 调 `SaveToSlot`/`LoadSlot`(0/1/2 三槽,带截图缩略图,`UI/PauseMenu.cs:341/365`)。
- boss 击破:`BossBattleManager`(`BossFight/BossBattleManager.cs:98`)在最终 boss 死后 `AutoSaveAtPlayer()` 原子写一次完整档。
- 标记永久死亡/开门:敌人 `EnemyBase`(`Enemy/EnemyBase.cs:473`)、`LockedDoor`/`ChestInteract`/`TutorialGate` 在死/开时调 `MarkEnemyDefeated`/`MarkDoorOpened` 写内存集合,落盘交给下次正常存档。

**依赖 & 被依赖**

依赖(它读/写的系统):`PlayerStats`(`LoadBaseStats`/`LoadSavedVitals`/`RestoreAll`)、`InventorySystem.LoadItems`、`EquipmentSystem.LoadEquipment`/`GetEquippedMaxHPBonus`、`SkillSystem.LoadSkills`;场景物体 `EnemyBase`(`SaveID`/`SavesPermanentDeath`/`RespawnsAtCheckpoint`/`Initialized`/`LoadSaveState`/`Respawn`)、`LockedDoor`/`ChestInteract`/`TutorialGate`(`LoadOpened`)、`EquipmentPickup`/`ItemPickup`(`SyncToEquipment`/`SyncToInventory`)、`ShopSystem`(`CaptureSaveData`/`LoadSaveData`);`GameManager.LoadScene`、`AudioManager`(`RefreshSceneBGM`/`PlayCheckpoint`)、`SceneNames.IsNonGameplay`/`Tutorial`。

被依赖(反过来用它):`GameManager`(重生)、`BossBattleManager`(boss 击破 autosave + 订阅 `AfterApply` 重接 boss)、`MainMenuUI`/`PauseMenu`(新游戏/继续/手动存读)、`EnemyBase`(标记永久死亡)、各类门/箱/教程门(标记开门)。`AfterApply` 静态事件被 `BossIntroTrigger`/`BossFogGate`/`BossHealthBarUI`/`VictoryGate`/`EnemyClearGate`/`BossBattleManager` 订阅,用于读档后重置 boss 登场/血条/雾门/通关门状态。

**关键设计 / 易错点**
- **全局态/场景态分层 + `globalStateLoaded` 内存连续**是整套设计的灵魂:普通过门不碰背包/玩家属性和已开门集合,只有"真读档/重生"才回滚。改这块务必想清楚 `realLoad` 取值(`SaveSystem.cs:236`)。
- 死亡重生必须满血,绝不能读死时存档的 0 血(`SaveSystem.cs:302-308`)——这是个写过的坑。
- 永久死亡判定:`SavesPermanentDeath = permanentDeath || 是 MinotaurBoss`(`EnemyBase.cs:50`);非永久死亡的小怪坐火堆/读档会全部 `Respawn`。无 autosave 的精英怪"杀了不存档就死、读档复活"是有意行为(`SaveSystem.cs:461-467` 注释)。
- 场景物体 ID 必须带 `场景名:` 前缀(`SaveIdUtility.WithScene`),否则跨场景同名物体会互相污染(A 场景开过的门让 B 场景同 ID 门误判成开)。
- `RefreshRespawnableEnemies` 跳过 `Initialized==false` 的预留 inactive 敌人(`SaveSystem.cs:337`),否则 `Respawn` 会把 `initialPosition` 还是 (0,0,0) 的怪扔到原点。
- 手动 `LoadSlot` 同场景读档没有 `sceneLoaded` 事件,靠 `RefreshSceneBGM()` + `ApplySave` + `AfterApply` 手动补全;教程即便同场景也强制整场景重载(门/靶/序列的内存态原地 apply 复位不了,`SaveSystem.cs:404-423`)。
- 资产引用全部走 ID + `Resources/Data` 缓存池,新加可存档的 ScriptableObject 必须放进 `Resources/Data` 且 `saveID` 唯一,否则发布版 `ResolveAsset` 解析不到。
- 改了 Inspector 里的初始钱/血不生效,是因为存档覆盖了初始值——需先删 `save_auto.json`(见项目记忆)。
