## Feedback 打击感/反馈

**定位**:一组与游戏逻辑解耦的"反馈层"工具——命中闪白、顿帧、震屏、粒子特效、刀光/残影/全屏吼叫。它本身不做任何伤害判定,只负责把"打到了 / 被打了 / boss 怒了"这些事件翻译成玩家肉眼/手感能感知到的反馈,是动作手感(juice)的统一供给方。

**关键脚本**(`Assets/Scripts/Feedback/`):
- `DamageFeedback.cs:4` — 唯一挂在角色身上的反馈组件(玩家+所有敌人各一份)。负责受击闪白、持续红染预警、击退滑行、基色染色。它是个有状态的 MonoBehaviour,其余都是无状态/单例工具。
- `Hitstop.cs:6` — 顿帧。`Hitstop.Do(duration, scale)` 把 `Time.timeScale` 压到极小一小会儿(realtime 计时),增强重击的"咔哒"感。懒加载常驻单例。
- `CameraShake.cs:8` — 震屏。走 Cinemachine Impulse(`CameraShake.Shake(duration, magnitude)`),懒加载单例,自带 `CinemachineImpulseSource`。
- `VfxManager.cs:7` — 通用粒子特效播放器:常驻单例 + 对象池。`Play`(一次性自动回收)/ `PlayLoop`+`StopLoop`(跟随父物体的持续特效)。特效是 `Resources/` 下的 `ParticleSystem` 预制,加特效=做预制+一行调用,不写脚本。
- `ScreenRoarFx.cs:8` — 全屏放射状吼叫(漫画集中线)。`ScreenRoarFx.Burst(focusWorld,…)` 以 boss 屏幕位置为中心爆开多波集中线,用 `unscaledDeltaTime` 跑,顿帧里也照常。单例。
- `Vfx_SlashLine.cs:6` / `Vfx_Afterimage.cs:5` — 两个"new GameObject + AddComponent + Init"式的临时特效:月牙刀光(`LineRenderer` + `sin(πt)` 鼓起)、残影(复制 sprite 渐隐)。自播放、自销毁,无单例无池。
- `SpriteOneShot.cs:6` — 轻量一次性逐帧精灵动画(尘土这种不值得上粒子的),`OnEnable` 播一遍 `frames` 后 `Destroy`。

**怎么工作**:
- **受击表现链(DamageFeedback)**。这是反馈层最核心的一块,它把多种"颜色/位移"叠在同一组 `SpriteRenderer` 上,靠几个状态量协调,避免互相卡死:
  - `Awake` 抓全部子 `SpriteRenderer`,缓存原色 `originalColors` 和原材质 `originalMaterials`(`DamageFeedback.cs:18`)。
  - `Flash()` 把渲染器换成纯白材质 `flashMaterial`(真正变白,而不是改 color——白色染白看不出),`flashDuration` 后还原(`DamageFeedback.cs:52`、`FlashRoutine:123`)。
  - `HoldWarning()` / `ClearWarning()` 是一对:危招预警期间持续染红、保持到清除(`DamageFeedback.cs:60/71`)。关键状态量是 `warningHeld`:为 true 时,即便预警期间又被 `Flash` 打了,闪白结束后**回到红而非原色**(`FlashRoutine:138` 的 `warningHeld ? Color.red : originalColors[i]`),否则一次受击会把红预警洗掉。
  - `SetBaseColor()` 改的是 `originalColors` 本身,即"受击闪白后该恢复成什么色",用于 boss 二阶段怒气红这种持久染色(`DamageFeedback.cs:81`)。
  - **材质还原是反复出现的防坑动作**:`RestoreMaterials()`(`:33`)在 `HoldWarning`/`ClearWarning`/`OnDisable` 里都被调,因为一旦在白材质上被打断,渲染器就卡成白剪影。`OnDisable`(`:41`)是兜底:闪白途中被禁用 → 停协程、还原材质和颜色、清 `warningHeld`,避免再启用时卡白。
  - **击退**`ApplyKnockback(sourcePos, force)`(`:96`):按"远离来源"的方向给一段水平初速度,在 `KnockbackRoutine`(`:108`)里按剩余时间线性衰减到 0,读起来是"被推一下滑停"。`immuneToKnockback`=霸体,只挡位移、不挡扣血/削韧/闪白。`knockbackDuration` 同时也是最短 flinch 时长。
- **顿帧防菜单冲突(Hitstop)**。`Run`(`Hitstop.cs:26`)起手就判 `timeScale<=0.001` 直接 return(已暂停不顿);还原前还要确认 `timeScale` 仍停在"我们设的顿帧值附近(0.001~scale+0.001)",否则不碰——这是为了防止"顿帧期间玩家开了暂停菜单(timeScale=0)",结束时被误还原成 1 把暂停解掉(`Hitstop.cs:35`)。`busy` 标志保证同一时刻只有一次顿帧。
- **特效池(VfxManager)**。`Spawn`(`:69`)从池里 `Rent` 或 `Instantiate`,首次实例化时记下每个子粒子系统的原色 `baseColors`(`:113`),之后 `tint` 都是"原色×tint"相乘(`:87`),白 tint=保持原色。`sortRef` 让粒子排序层跟随某个角色 `SpriteRenderer`(`+30`),保证特效在角色之上(`:89`)。一次性走 `Recycle` 定时回收,持续型 `StopLoop` 先停发射再 `RecycleWhenDead` 等粒子自然消散后回池(`:142`)。
- **临时特效**`Vfx_SlashLine`/`Vfx_Afterimage`/`SpriteOneShot` 都是"自己管自己生命周期"的脚本,`Update`/协程里跑完渐隐就 `Destroy(gameObject)`,不进池(用量小、参数每次不同)。

**入口 & 触发**:
- `DamageFeedback` **必须挂在 prefab 上**(玩家、各敌人、`MinotaurBoss`)。它不是单例,靠 `GetComponent<DamageFeedback>()` 取用:
  - 玩家挥击命中敌人 → `Player_AttackState.cs:108-110` 调 `enemy.TakeDamage` + `feedback.ApplyKnockback`;敌人 `TakeDamage` 内部 `feedback.Flash()`(`EnemyBase.cs:457`)。
  - 冲刺斩同理(`Player_DashStrikeState.cs:84-85`)。
  - 玩家被击 → `PlayerStats.cs:142-144`:`Flash()` + `CameraShake.Shake`。
  - 敌人攻击命中玩家的击退/硬直配合 → `EnemyBase.cs:406-429`(先 `Stun` 再 `ApplyKnockback`)。
  - 危招预警 → 敌人动画事件 `Warn()/WarnEnd()` 转发到 `HoldWarning/ClearWarning`(`EnemyBase.cs:329-330`);招式中断兜底 `ClearWarning`(`AttackRunner.cs:88/130`)。
- 单例工具**首次调用时自动懒加载生成 GameObject + DontDestroyOnLoad**,无需场景预置:
  - `Hitstop.Do` — 处决重击(`Player_ExecuteState.cs:60`)。
  - `CameraShake.Shake` — 受击/格挡/识破/处决/boss 演出多处(`PlayerStats:144`、`Player_BlockState:62/74`、`Player_CounterState:62`、`Player_ExecuteState:59`、`MinotaurBoss:165/183`)。
  - `VfxManager.Play/PlayLoop/StopLoop` — 治疗光环(`Player_HealState:32/74`)、护盾火花(`Player_Block/CounterState`)、冲刺能量(`Player_DashStrikeState:64/147`)、boss 怒气光环/吼叫/冲锋/爆炸(`MinotaurBoss:102/166/174/250`)。
  - `ScreenRoarFx.Burst` — boss 吼叫(`MinotaurBoss:167`)、boss 登场(`BossIntroTrigger:140`)。
- `Vfx_SlashLine`/`Vfx_Afterimage` 由 `Player_DashStrikeState`(`:91-98`、`:131-134`)按招式数据(`Skill_Base` 的 Slash*/Afterimage* 参数)实例化。`SpriteOneShot` 预制用于敌人脚下尘土(`ArcherEnemy.cs:10`)。

**依赖 & 被依赖**:
- **依赖外部**:`CameraShake` 依赖 Cinemachine(场景 vcam 上必须挂 `CinemachineImpulseListener`,否则震不动,`CameraShake.cs:5`);`VfxManager`/`ScreenRoarFx` 依赖 `Resources/` 下的预制与贴图(`Vfx/RoarLines`、各 `Vfx/*`);`ScreenRoarFx` 依赖 `Camera.main`。`DamageFeedback.flashMaterial` 依赖纯白剪影材质 `Custom/SpriteSolidColor`(空则闪白看不出)。
- **被依赖**:战斗/状态机层广泛调用——`PlayerStats`、`EnemyBase`、`MinotaurBoss` 以及玩家各 State(`Attack/DashStrike/Block/Counter/Execute/Heal`)、`AttackRunner`、`BossIntroTrigger`。其中 `EnemyBase.HandlePlayerHit` 与 `DamageFeedback` 的击退耦合最紧:**必须先把被击者切进"不每帧重设速度"的硬直态(`StunnedState`),`ApplyKnockback` 给的衰减速度才滑得出来**,否则 idle/move 每帧覆盖速度看不到击退(`EnemyBase.cs:422-429`、`DamageFeedback.cs:106-107`)。

**关键设计 / 易错点**:
- **两类生命周期管理**:有状态的 `DamageFeedback` 挂 prefab,无状态工具(`Hitstop/CameraShake/VfxManager/ScreenRoarFx`)都是**懒加载常驻单例**(首次调用自建 + `DontDestroyOnLoad`),调用方一行静态方法即可,不需要在场景里摆。这与项目"常驻单例进 Bootstrap"的约定略有出入——这几个反馈单例是运行时自建的轻量工具,不持久状态,故没放 Bootstrap。
- **闪白卡白剪影坑**:任何会打断 `Flash` 协程的路径都必须 `RestoreMaterials()`,否则渲染器停在 `flashMaterial` 上变成白剪影。这是 `HoldWarning/ClearWarning/OnDisable` 里反复出现 `RestoreMaterials()` 的原因(`DamageFeedback.cs:64/75`)。
- **`warningHeld` 优先级**:预警红 > 闪白白。受击闪白结束后,若仍在预警期则回红不回原色(`FlashRoutine:138`)。
- **顿帧 vs 暂停的 timeScale 之争**:`Hitstop` 起手判暂停、还原前判区间(`:28`、`:35`),这是为了不和暂停菜单(`timeScale=0`)互相破坏——改 timeScale 的系统要小心彼此覆盖。
- **特效 tint 是相乘不是覆盖**:`VfxManager` 在预制原色上乘 tint(`:87`),想保持原色传 `Color.white`,想染色才传具体色;首次实例化记原色,之后从池复用都基于原色重算,不会被上次 tint 污染。
- **击退方向取来源相对位置的符号**:`Mathf.Sign(myX - sourceX)`,同位置时兜底为 1(`DamageFeedback.cs:99-100`),保证一定被推开而不是原地不动。
