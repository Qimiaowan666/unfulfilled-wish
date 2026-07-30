## Economy 经济(背包/装备/商店)

**定位**:玩家身外之物的三大账本——道具背包、装备穿戴、商人买卖。负责"获得→持有→使用/穿戴→消耗金币"这条循环,并把装备/道具的数值加成接到 `PlayerStats` 上,把库存/购买记录交给存档持久化。它是纯逻辑层(三个常驻单例 + 一组数据结构),不画 UI;界面由 `Assets/Scripts/UI` 渲染,数据由 `Assets/Scripts/Data` 定义。

**关键脚本**
- `Assets/Scripts/Economy/InventorySystem.cs:5` — 背包单例。一个 `List<ItemData> items`(默认 60 格 = 20×3 页,`InventorySystem.cs:9`),提供增删/移位/丢弃/使用,变更后广播 `OnInventoryChanged`(`InventorySystem.cs:12`)。`UseItem`(`:58`)是消耗品落地:回血走 `stats.TakeDamage(-healAmount)`,被动加成走 `stats.ApplyStatBonus`,用完移除。
- `Assets/Scripts/Economy/EquipmentSystem.cs:7` — 装备单例。四个穿戴槽 `weapon/armor/accessory1/accessory2` + 已拥有列表 `ownedEquipment`(`:11-16`),变更广播 `OnEquipmentChanged`(`:18`)。负责装备/卸下时对 `PlayerStats` 的加成增减,以及切场景后把加成重新贴到新玩家身上。
- `Assets/Scripts/Economy/ShopSystem.cs:32` — 挂在商人 NPC 上的库存组件(非单例,每个商店一个)。三类货架 `itemEntries/equipmentEntries/skillEntries`(`:38-40`),`BuyItem/BuyEquipment/BuySkill`(`:77/:89/:105`)做"够钱→交付→扣钱→减库存"的事务,并实现自己的存档快照 `CaptureSaveData/LoadSaveData`(`:129/:140`)。
- `Assets/Scripts/Economy/ShopNPCTrigger.cs:5` — 商人交互器,继承 `InteractTrigger`。玩家进范围按 F 调 `ShopUI.Instance.Open(shop)` 开店,再按 F 关店(`:21-28`)。
- 配套数据(在 `Assets/Scripts/Data`):`ItemData.cs:6`(消耗品/被动,字段 `price/healAmount/attackBonus/...`)、`EquipmentData.cs:6`(`slot/price/三项加成`)、枚举 `EquipmentSlot{Weapon,Armor,Accessory}`(`EquipmentData.cs:3`)。三类货架条目 `ShopItemEntry/ShopEquipmentEntry/ShopSkillEntry`(`ShopSystem.cs:6/15/24`)各带 `quantity`(`-1` = 无限,`0` = 售罄)。

**怎么工作**
- 数据驱动:道具/装备都是 `ScriptableObject` 资产,背包/装备只存对资产的引用。所以"持有"=列表里有这个引用,这也是拾取物双向同步(见下)和存档(按资产 ID 还原引用)能成立的前提。
- 加成数据流(装备):`Equip` → `ApplyBonus(eq, +1)`(`EquipmentSystem.cs:225`)把攻防写进 `stats.ApplyEquipmentBonus`(累加到 `equipmentAttackBonus/...`)、把生命上限写进 `stats.ApplyStatBonus`;`Unequip` 同理传 `-1` 抵消。注意攻防是"增量累加",生命上限也是增量,所以装/卸必须严格配对,否则会漂移。
- 加成全量重建:切场景玩家会重建,`OnSceneLoaded`(`:38`)清掉 `stats` 缓存并调 `RebuildEquippedCombatBonuses`(`:253`)——它绕开增量逻辑,直接把四槽加成求和后 `stats.SetEquipmentBonuses`(全量覆盖,`PlayerStats.cs:195`),保证新玩家拿到正确总值而不会重复叠加。生命上限不在重建之列(由存档的 `baseMaxHP` 推导,见存档说明)。
- 饰品双槽逻辑:`Equip` 遇到饰品转给 `EquipAccessory`(`:137`)按"空槽优先,满了替换槽1"塞;面板点具体槽则用 `EquipAccessoryToSlot(eq, slotIndex)`(`:164`),会先把同一件从另一槽摘掉避免两槽放同一件,再换目标槽。
- 商店事务:三类购买都是"`IsAvailable` 且够钱"才执行,先 `AddItem/AddEquipment/LearnSkill`,成功后 `AddGold(-price)` 再 `Consume`(`quantity--`,无限货 `-1` 不减,`ShopSystem.cs:149`)。装备买进只入库不自动穿(`AddEquipment(eq, false)`,`:100`),技能若已学则直接消库存返回 true(`:110`)。`AvailableSkills`(`:63`)会过滤掉已学技能(查 `SkillSystem.GetOrCreate().HasSkill`),不在货架显示。
- 存档:库存/装备本身不自己存,由 `SaveSystem.BuildSaveData` 读 `inventory.items / equipment.ownedEquipment / weapon...`(`SaveSystem.cs:181-187`)序列化为资产 ID,读档调 `LoadItems/LoadEquipment`(`InventorySystem.cs:78` / `EquipmentSystem.cs:98`)还原。`LoadEquipment` 先清四槽并 `SetEquipmentBonuses(0,0)` 归零,再逐件 `EquipLoaded`(`:275`)重贴加成——避免读档叠到旧加成上。商店库存则各自 `CaptureSaveData/LoadSaveData` 按资产 ID 记 `quantity`(`ShopSystem.cs:129-246`)。

**入口 & 触发**
- 三个单例(`InventorySystem`/`EquipmentSystem`/`ShopUI`)按项目约定常驻 `Assets/Scenes/Bootstrap.unity`(`Awake` 里 `DontDestroyOnLoad`,`InventorySystem.cs:14`、`EquipmentSystem.cs:22`),不靠运行时自动创建。`ShopSystem` 不是单例,挂在场景里的商人 NPC 上(`[RequireComponent]` 由 `ShopNPCTrigger` 强制,`ShopNPCTrigger.cs:4`)。
- 获得物品的玩家行为:地上拾取 `ItemPickup`(F 捡 → `InventorySystem.Instance.AddItem`,`ItemPickup.cs:23`)、`EquipmentPickup`(F 捡 → `EquipmentSystem.Instance.AddEquipment(eq,false)`,`EquipmentPickup.cs:22`)、宝箱 `ChestInteract`、商店购买。
- 使用/穿戴的玩家行为:开角色面板(`CharacterPanelUI`),`InventoryPageView` 双击用道具(`InventorySystem.Instance.UseItem`,`InventoryPageView.cs:126`),`EquipmentPageView` 点装备槽穿/卸(`sys.Equip/Unequip/EquipAccessoryToSlot/UnequipAccessory`,`EquipmentPageView.cs:48/81/91/197`,右键卸下)。
- 买卖的玩家行为:走到商人按 F → `ShopNPCTrigger.Interact` → `ShopUI.Open(shop)` → 点行 → 确认购买调 `shop.BuyItem/BuyEquipment/BuySkill`(`ShopUI.cs:113/118/123`)。

**依赖 & 被依赖**
- 依赖:`PlayerStats`(加成与金币的最终落点:`ApplyEquipmentBonus/SetEquipmentBonuses/ApplyStatBonus/AddGold/gold`)、`ItemData`/`EquipmentData`/`SkillData`(数据资产)、`SkillSystem`(`ShopSystem` 买技能与过滤已学:`GetOrCreate/HasSkill/LearnSkill`,`SkillSystem.cs:40/51/56`)、`AudioManager`(开店/购买/装备音效)、`ShopUI`/`InteractTrigger`(商店开关与交互基类)。
- 被依赖:`SaveSystem`(读写库存/装备/商店库存,并用 `equipment.GetEquippedMaxHPBonus()` 反推 `baseMaxHP`,`SaveSystem.cs:160`;读档后扫场景调 `ItemPickup.SyncToInventory`/`EquipmentPickup.SyncToEquipment`,`SaveSystem.cs:726/728`)、UI 三件套(`ShopUI`/`CharacterPanel` 的 `InventoryPageView`/`EquipmentPageView`)、拾取物 `ItemPickup`/`EquipmentPickup`/`ChestInteract`。

**关键设计 / 易错点**
- 装/卸加成是增量(`ApplyBonus ±1`),切场景靠全量重建(`SetEquipmentBonuses`)兜底——两条路并存,改加成逻辑时务必同时维护这两处,否则换场景后数值会和面板内操作时不一致。
- 生命上限(`maxHPBonus`)走的是 `ApplyStatBonus` 改 `maxHP` 这条"永久基线"路径,不在 `RebuildEquippedCombatBonuses` 重算范围内;存档靠 `baseMaxHP = maxHP - GetEquippedMaxHPBonus()` 在保存时剥离装备贡献(`SaveSystem.cs:160`),读档再叠回。所以 maxHP 加成的装备一旦增删,必须保证"剥离/叠回"成对,否则反复存读会漂移。
- 拾取物双向同步要点:`ItemPickup`/`EquipmentPickup` SetActive(false) 后收不到事件,所以同步由 `SaveSystem` 从外部统一驱动(含 inactive),不能让拾取物自己订阅 `AfterApply`(`ItemPickup.cs:7`)。读"捡之前的档"地上物品会重新出现而非凭空消失。
- 商店"已拥有不重复卖":装备 `BuyEquipment` 查 `HasEquipment` 拦截重复购买(`ShopSystem.cs:96`),技能 `AvailableSkills` 直接过滤已学(`:71`);但道具无此判断,可重复买(消耗品本应如此)。
- `quantity` 语义:`-1` = 无限货(`Consume` 不减),`0` = 售罄(`IsAvailable` 为 false),`>0` = 有限。`FormatPrice` 据此决定是否显示 `x{quantity}`(`ShopSystem.cs:124`)。
- `EquipmentSystem.accessory1` 字段有 `[FormerlySerializedAs("accessory")]`(`:13`)——历史上是单饰品槽,后来扩成双槽,旧 prefab/场景序列化数据靠它迁移,别误删。
- `ShopSystem.SaveID` 用 `SaveIdUtility.GetSceneObjectID(this, shopID)`(`:41`),多个商店要保证 `shopID` 唯一,否则库存存档会串。
