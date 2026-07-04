## Data 数据(ScriptableObject)

**定位**:一组纯数据的 ScriptableObject 资产类——把"道具/装备/技能/对话/Boss 展示信息"从代码里抽出来,做成可在 Project 里右键创建、Inspector 里手填的配置资产。它们本身不含逻辑,是整个游戏(商店/背包/装备/技能/对话/Boss UI)共享的**数据契约层**,实现"数据驱动 + 美术/策划改资产不改代码"。

**关键脚本**(均在 `Assets/Scripts/Data/`):
- `ItemData.cs:6` —— 道具数据。`ItemType`(`Consumable`/`Passive`,`ItemData.cs:3`)、价格、图标,加可被消耗的回血量 `healAmount` 与可被动加成的 `attackBonus/defenseBonus/maxHPBonus`(`ItemData.cs:18-23`)。
- `EquipmentData.cs:6` —— 装备数据。`EquipmentSlot`(`Weapon`/`Armor`/`Accessory`,`EquipmentData.cs:3`)+ 三围加成 `attackBonus/defenseBonus/maxHPBonus`(`EquipmentData.cs:18-20`)。
- `SkillData.cs:6` —— 技能数据。`SkillType`(`Active`/`Passive`,`SkillData.cs:3`);主动技参数 `cooldown/damage/poiseDamage/manaCost`(`SkillData.cs:18-21`),被动技参数 `attackPercent/defensePercent/perfectBlockWindowBonus`(`SkillData.cs:24-26`)。
- `DialogueSequence.cs:15` —— 一段剧情台词。内含 `[Serializable]` 的 `DialogueLine`(speaker + `[TextArea]` text,`DialogueSequence.cs:8`),`DialogueSequence` 就是一个 `List<DialogueLine>`(`DialogueSequence.cs:17`)。
- `BossProfile.cs:7` —— 单个 Boss 的"展示数据"(非战斗数值):血条名 `displayName`、可选血条覆盖色 `overrideBarColor/barColor`、登场名牌素材(罗马名整图 `romanSprite` + 中文逐字图数组 `nameChars` + 染色 `nameColor`)、击破名牌 `defeatBanner`(`BossProfile.cs:11-28`)。

> 这三个"物品类"(Item/Equipment/Skill)结构高度同构:都带 `[Header("Save")] saveID`、`xxxName`、`description`、`icon`、`type` 枚举、`price`,再各自补自己的效果字段。它们没有共同基类——是有意保持的扁平 POD 资产。

**怎么工作**:
- 都标了 `[CreateAssetMenu(menuName = "Game/...")]`,所以在 Project 右键 Create/Game 下就能造资产实例,数值/图标全在 Inspector 填。运行时各系统只**读**这些字段,不改它们(资产是共享只读模板)。
- **数据如何变成游戏效果**:加成字段被消费方"翻译"成玩家属性。装备走 `EquipmentSystem.ApplyBonus(EquipmentData, sign)`(`Economy/EquipmentSystem.cs:225`),把 `eq.attackBonus/defenseBonus/maxHPBonus` 以 `±1` 符号加/减到 `PlayerStats`(`EquipmentSystem.cs:232-234`);卸下就传 `sign=-1` 抵消。技能/道具的被动加成与回血同理被各自系统读取。
- **saveID 的作用(关键设计)**:存档不能存对象引用,只能存字符串。`SaveIdUtility.GetAssetID(SaveIdUtility.cs:6)` 对这三类资产取其 `saveID`,**空则回退到 `asset.name`**(`SaveIdUtility.cs:10-14`)。读档时 `SaveSystem.ResolveAsset<T>(SaveSystem.cs:644)` 反向:用 ID 去 `Resources.LoadAll<T>("Data")`(`SaveSystem.cs:652`)的资产池里逐个 `MatchesAssetID` 匹配回真正的 SO 引用。所以**这些资产必须放在 `Resources/Data` 下**才能在发布版被读档解析到(编辑器还有 `AssetDatabase` 兜底,`SaveSystem.cs:661-669`)。
- `BossProfile` 是"展示数据驱动 UI":血条 `BossHealthBarUI` 读 `p.displayName` 设名字、`p.overrideBarColor` 时用 `p.barColor` 染填充(`UI/BossHealthBarUI.cs:85-86`);名牌 `BossNameCard.Build` 读 `romanSprite` + `nameChars[]` 动态排版任意字数中文名(`UI/BossNameCard.cs:50-82`)。
- `DialogueSequence` 是"内容容器",由 `DialogueUI` 逐行播放、`BossIntroTrigger` 触发。

**入口 & 触发**:
- 这些 SO 不被"创建"——它们是离线资产,运行时由别的对象**通过 Inspector 引用字段持有**:Boss 预制体的 `EnemyBase.profile`(`Enemy/EnemyBase.cs:15`)指向 `BossProfile`;宝箱 `ChestInteract` 持 `EquipmentData[] equipment` / `ItemData[] items`(`Interactables/ChestInteract.cs:11-12`);拾取物 `ItemPickup`/`EquipmentPickup` 各持一个;商店/背包/技能面板持有可售/已有清单。
- **玩家行为触达路径**:开宝箱/拾取 → 道具/装备进 `InventorySystem`;在商店买东西 → `ShopSystem` 读 `price`/效果字段并展示(`UI/ShopUI.cs:133-169`);打开角色面板 → 各 PageView 读字段渲染详情;装备一件 → `EquipmentSystem` 把加成应用到属性;触发 Boss 战 → `BossIntroTrigger` 用 `BossProfile` 播名牌+绑血条;触发对话 → `DialogueUI` 播 `DialogueSequence`。

**依赖 & 被依赖**:
- **它依赖**:几乎不依赖任何运行时系统——只 `using UnityEngine`,纯数据。可以说是依赖图的叶子。
- **被谁用(具体到类)**:
  - `ItemData`:`InventorySystem`、`ShopSystem`/`ShopUI`、`ItemPickup`、`ChestInteract`、`InventoryPageView`(`UI/CharacterPanel/Views/InventoryPageView.cs:115-120`)、`SaveSystem`/`SaveIdUtility`。
  - `EquipmentData`:`EquipmentSystem`(应用加成,`Economy/EquipmentSystem.cs:225-234`)、`EquipmentPickup`、`ChestInteract`、`EquipmentPageView`、`ShopSystem`/`ShopUI`、`SaveSystem`/`SaveIdUtility`。
  - `SkillData`:`SkillSystem`、`SkillsPageView`(`UI/CharacterPanel/Views/SkillsPageView.cs:122-141`)、`ShopSystem`/`ShopUI`、`SaveSystem`/`SaveIdUtility`。
  - `DialogueSequence`:`DialogueUI`、`BossIntroTrigger`、`TutorialSequence`。
  - `BossProfile`:`EnemyBase`(`Enemy/EnemyBase.cs:15`)持有,`BossHealthBarUI`、`BossNameCard`、`BossIntroTrigger`、`BossFinishUI`(击破名牌)消费。

**关键设计 / 易错点**:
- **资产位置约束**:Item/Equipment/Skill 三类要能被读档解析,必须落在 `Resources/Data/`(含子文件夹),因为 `SaveSystem.ResolveAsset` 走 `Resources.LoadAll<T>("Data")`(`SaveSystem.cs:652`)。放别处 → 发布版读档时 `Debug.LogWarning("could not resolve ...")`(`SaveSystem.cs:672`),编辑器里能蒙混(有 AssetDatabase 兜底)但打包后失效。这也是项目记忆里"Data 移入 Resources"那次改动的原因。
- **saveID 与 name 的双重身份**:`saveID` 留空会回退用资产文件名当 ID(`SaveIdUtility.cs:14`、`MatchesAssetID` 同时匹配二者 `SaveIdUtility.cs:20`)。坑:若图省事不填 `saveID`,后续**重命名资产文件**就等于换了存档 ID,老存档会解析不到该道具/技能。要稳就显式填 `saveID`。
- **三类资产同构但无基类**:字段名一致(`attackBonus` 等),消费代码对 Item 和 Equipment 经常写两套几乎相同的加成逻辑(对比 `ShopUI.cs:133-135` 与 `:148-150`)。改加成口径时记得多处同步;没有共享基类是为换取 Inspector 扁平直观,代价是少量重复。
- **加成的符号约定**:装备生效/失效靠 `ApplyBonus(..., +1f/-1f)` 对称加减(`EquipmentSystem.cs:225`),数据本身只存正向加成值。卸装/换装时务必配对调用 `-1`,否则属性会漂移。
- **BossProfile 区分"展示"与"战斗"数据**:它只管名字/配色/名牌素材,不含血量攻击等战斗数值(那些在 EnemyBase/战斗数据里)。`overrideBarColor` 不勾时保留预制体原色、`defeatBanner` 为空时用预制体默认(`BossProfile.cs:15、28`)——按 boss 选择性覆盖。
- `DialogueLine.text` 用 `[TextArea(2,4)]`(`DialogueSequence.cs:11`)方便填多行;`DialogueLine` 是普通 `[Serializable]` 类不是 SO,只能内嵌在 `DialogueSequence` 里存在。
