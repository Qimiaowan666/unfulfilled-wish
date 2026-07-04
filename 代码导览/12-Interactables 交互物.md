## Interactables 交互物

**定位**:游戏里"靠近 + 按 F 触发"的所有世界物件的统一框架与具体实现——地上拾取(道具/装备)、宝箱、钥匙门、场景传送门都走同一套"进触发区→头顶木框提示→按 F 执行"流程。它把"提示显示/输入仲裁/存档恢复"这些重复逻辑抽到基类,子类只写各自的"按 F 做什么"。

**关键脚本**
- `Assets/Scripts/Interactables/InteractTrigger.cs:10` —— 抽象基类。封装触发区进出、木框提示显隐、F 键检测、"最近优先"仲裁、Gizmo。子类唯一必须实现的是 `Interact()`(`InteractTrigger.cs:98`)。
- `Assets/Scripts/Interactables/ItemPickup.cs:8` —— 地上道具拾取,捡起进 `InventorySystem`,带读档双向同步。
- `Assets/Scripts/Interactables/EquipmentPickup.cs:5` —— 地上装备拾取,捡起进 `EquipmentSystem`(只进背包不自动装),带 `PickedUp` 事件 + 读档双向同步。
- `Assets/Scripts/Interactables/ChestInteract.cs:7` —— 宝箱,按 F 逐帧播掀盖动画并发放 金币/装备/道具,持久化"已开"。
- `Assets/Scripts/Interactables/LockedDoor.cs:8` —— 钥匙门,查背包是否有钥匙道具决定能否开,升闸动画 + 穿门分层 + 实体阻挡。
- `Assets/Scripts/Interactables/SceneLoadTrigger.cs:6` —— 场景传送门,按 F 切场景并把玩家落到目标场景配对的落点(贴地)。

**怎么工作**

*基类生命周期*:`OnTriggerEnter2D`(`InteractTrigger.cs:70`)检测到 Player tag 进入 → 置 `playerInRange=true` 并把自己加进静态表 `s_inRange`;`OnTriggerExit2D`(`:78`)反向移除。每帧 `Update()`(`:59`)先跑 `ResolveWinners()` 仲裁,再决定显示提示和响应 F。

*"最近优先"仲裁(核心设计)*:多个交互区重叠时,如果每个都各自显示提示/各自响应 F,按一下 F 会同时触发一堆。`ResolveWinners()`(`:30`)用静态字段做全局仲裁——每帧只算一次(`s_resolvedFrame == Time.frameCount` 守卫),遍历 `s_inRange` 里所有在范围内的交互物,按到玩家的平方距离挑出两个赢家:`s_promptWinner`(最近的、`ShowPrompt` 为真者)只它显示提示;`s_actionWinner`(最近的、`WantsInteract` 为真者)只它响应 F。其余实例的 `Update` 里走 `InteractPromptUI.Hide(this)` 闭嘴。注意 promptWinner 和 actionWinner 是分开算的,这样"提示隐藏但仍可按 F"的场景(如 LockedDoor 没钥匙时显示提示但不响应)成立。

*可重写的扩展点(模板方法)*:子类通过重写若干虚成员定制行为而不碰仲裁/输入逻辑——
  - `ShowPrompt`(`:101`,默认 `playerInRange && timeScale>0 && !ShopUI.IsOpen`):显示条件。拾取物追加 `!taken && !CharacterPanelUI.IsOpen`,宝箱追加 `!opened`,门追加 `!IsOpen`。
  - `WantsInteract`(`:104`,默认 = `ShowPrompt`):F 生效条件。LockedDoor 重写为 `ShowPrompt && HasKey()`(`LockedDoor.cs:57`)实现"提示照显但没钥匙按了没用"。
  - `PromptPoint()`(`:107`,默认锚精灵顶 + offset):提示框世界锚点。门改挂在实体门框顶(`LockedDoor.cs:73`),传送门无精灵改挂 transform 上方(`SceneLoadTrigger.cs:54`)。
  - `ResolvePromptSprite()` / `OnPlayerEnter()` / `OnPlayerExit()`:锚精灵解析、进出回调。

*提示渲染*:不是每个交互物各自挂 UI,而是共用一个常驻单例 `InteractPromptUI`(`Assets/Scripts/UI/InteractPromptUI.cs:6`,挂 Bootstrap.unity、DontDestroyOnLoad)。`Show(worldPos, label, who)` 设文字("<b>F</b> "+词)并把木框跟随世界点定位;`Hide(who)` 带 owner 校验——只有当前显示者能关,避免实例间互相误关(`:44`)。

*各子类的执行*:
  - 拾取(`ItemPickup.Interact:20` / `EquipmentPickup.Interact:19`):置 `taken`、加进背包系统、放音效、`SetActive(false)`。
  - 宝箱(`ChestInteract.Interact:38`):内容立即发放(保证途中离开也拿到)、`MarkDoorOpened` 持久化、协程 `PlayOpen` 逐帧播掀盖动画(`:65`),放完切空箱图并弹 `RewardPopupUI`(动画期间不能弹,因为弹窗会 timeScale=0 冻住动画)。
  - 钥匙门(`LockedDoor.Open:95`):置 `IsOpen`、`MarkDoorOpened`、关 `blockingCollider` 放行、关交互触发体(让箭/冲刺能穿开着的门,`SetInteractTriggerEnabled:150`)、可选传送玩家到 `destination`、协程播升闸动画前景层同步。
  - 传送门(`SceneLoadTrigger.Interact:56`):把 `targetPortalID` 存进静态 `pendingTargetPortalID`(LoadScene 后保留),调 `GameManager.LoadScene`;新场景里 `portalID == pendingTargetPortalID` 的传送门在 `Start()`(`:23`)把玩家贴地放到自己位置(`GroundedDropPoint` 向下射线找地面,`:39`)。

*存档恢复(由 SaveSystem 外部驱动,见下)*:门/箱用 `SaveID`(`SaveIdUtility`)登记到"已开集合",`LoadOpened` 双向恢复(开/关都处理,支持原地读旧档把门关回来);拾取物用 `SyncToInventory`/`SyncToEquipment` 按背包是否拥有做双向同步。

**入口 & 触发**
- 这些组件直接挂在场景物体上(各自带 `[RequireComponent(typeof(Collider2D))]`,`Reset()` 自动把碰撞设成 Trigger 并填默认提示词)。没有谁"创建"它们,由 Unity 加载场景实例化。
- 玩家移动碰到触发区 → `OnTriggerEnter2D` → 头顶出现木框 → 玩家按 F(`Pressed()` 读 `Keyboard.current.fKey`,`InteractTrigger.cs:130`)→ 仲裁后的 actionWinner 执行 `Interact()`。
- 存档系统在读档/过场景后回调:`SaveSystem.ApplyDoorStates`(`Assets/Scripts/Save/SaveSystem.cs:701`)用 `FindObjectsByType(...Include)` 扫场景(含 inactive),逐个调 `LockedDoor.LoadOpened`(`:715`)、`ChestInteract.LoadOpened`(`:718`)、`EquipmentPickup.SyncToEquipment`(`:726`)、`ItemPickup.SyncToInventory`(`:728`)。

**依赖 & 被依赖**

它用到的系统:
- `InteractPromptUI`(`UI/InteractPromptUI.cs`)—— 提示框显隐/Gizmo(`DrawAnchorGizmo`)。
- `PlayerController.Instance` —— 仲裁取玩家位置(`InteractTrigger.cs:37`);宝箱取 `Stats`、传送门取 `Rb`/`groundLayer`。
- `InventorySystem` / `EquipmentSystem` —— 拾取物和宝箱发放道具/装备;门 `HasKey()` 查 `InventorySystem.items` 是否含钥匙道具(`LockedDoor.cs:88`)。
- `SaveSystem`(`MarkDoorOpened`)+ `SaveIdUtility`(`WithScene`/`GetSceneObjectID` 生成防跨场景撞的 `SaveID`)—— 门/箱持久化(`Save/SaveIdUtility.cs`)。
- `PlayerStats.AddGold`、`AudioManager`(各类音效)、`RewardPopupUI`(宝箱奖励弹窗)、`GameManager.LoadScene`(传送)、`CharacterPanelUI.IsOpen` / `ShopUI.IsOpen`(屏蔽条件)、`Tags.Player`。
- 数据资产:`ItemData` / `EquipmentData`(ScriptableObject)。

反过来用它的系统:
- `SaveSystem.ApplyDoorStates`(`SaveSystem.cs:714-728`)是主要消费者,驱动全部读档恢复。
- `EquipmentPickup.PickedUp` 事件(`EquipmentPickup.cs:10`)供外部订阅(如教程引导)。
- 注意 `Tutorial/TutorialGate.cs` 是"第三类门",与 LockedDoor/ChestInteract 共用同一套 `SaveID` + `LoadOpened` + "已开集合"约定,但它不在本文件夹、不继承 InteractTrigger。

**关键设计 / 易错点**
- *仲裁靠静态字段,跨所有实例共享*:`s_inRange`/`s_promptWinner`/`s_actionWinner` 是 `static`。`OnDisable`(`:87`)必须清掉自己在表里的引用并把赢家指针置空,否则会出现"对象禁用了还占着赢家位"导致提示卡住或别人按不了 F。
- *prompt 与 action 分两套赢家*:这是"提示在但点了没用"的关键(LockedDoor 没钥匙)。改条件时别把 `WantsInteract` 和 `ShowPrompt` 混为一谈。
- *拾取物不能自己订阅 AfterApply*:一旦 `SetActive(false)` 就收不到事件,所以读档恢复必须由 SaveSystem 用 `FindObjectsInactive.Include` 从外部扫描驱动(`ItemPickup.cs` 顶部注释明确写了)。
- *所有 `LoadOpened`/`Sync` 都是双向的*:开/关、捡/未捡两种状态都要处理。原因是 boss 门在 ForsakenShrine 走"原地读档不重载场景",单向恢复会导致"开门后读旧档门关不回来"。
- *门/箱复用"门"的持久化通道*:宝箱也调 `MarkDoorOpened`、ID 进同一个 `runtimeOpenedDoorIDs` 集合——"门"在这里是泛指"一次性已开物体"。
- *宝箱奖励弹窗时机*:`RewardPopupUI` 会 timeScale=0,必须等掀盖动画放完再弹,否则动画被冻住;`LoadOpened` 里要 `StopAllCoroutines()` 掐掉在播的开箱协程,防读档后协程续播覆盖 sprite + 重复弹奖励(`ChestInteract.cs:84`)。
- *开门要连交互触发体一起关*(`LockedDoor.SetInteractTriggerEnabled`):开着的门若留着 isTrigger 碰撞会挡箭矢和冲刺扫描;`blockingCollider` 是非 Trigger 的实体阻挡,单独管。
- *传送门落点要贴地*:传送门精灵高、中心在半空,直接传过去会悬空再下落,所以 `GroundedDropPoint` 向下射线找地面把玩家脚底对齐(`SceneLoadTrigger.cs:39`)。跨场景靠 `static pendingTargetPortalID` 在 LoadScene 后存活传递落点。
