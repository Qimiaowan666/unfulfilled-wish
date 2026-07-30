## StateMachine 状态机底座

**定位**:整个游戏所有角色(主角 / 小怪 / boss)行为逻辑的公共骨架。它把"一个角色当前处于什么状态、状态怎么切、每帧干什么"这套机制抽成三个极薄的基类,让三种角色共用同一份状态机代码与同一套实体基础能力(刚体/动画/朝向/落地检测),各角色只需写自己的具体状态(Idle/Move/Attack/...)和 owner 数据。

**关键脚本**
- `Assets/Scripts/StateMachine/StateMachine.cs:3` —— 非泛型状态机本体。持有 `currentState`,提供 `Initialize / ChangeState / Update / Lock / Unlock`,以及一个 `canChangeState` 开关。整个项目只有这一份状态机实现。
- `Assets/Scripts/StateMachine/EntityState.cs:6` —— 所有状态的抽象基类 `EntityState`。封装状态机引用、`anim/rb`、`stateTimer`,以及可选的 `animBoolName`;`Enter` 点亮动画 bool、`Exit` 熄灭、`Update` 倒计时。
- `Assets/Scripts/StateMachine/Entity.cs:3` —— 所有角色 GameObject 的抽象 `MonoBehaviour` 基类。缓存 `Rb/Anim`,统一管理朝向(`FacingRight/FacingDir/SetFacing/Flip`)、对目标转身/取方向(`DirToward/FaceToward`)、可覆盖的落地检测(`CheckGrounded`)。

**怎么工作**
- **三合一设计**:`StateMachine` 是非泛型的,不关心 owner 是谁。全项目(主角 / 小怪 / boss)直接 `new StateMachine()` 共用这一份实现——早期那三个空壳子类 `PlayerStateMachine / EnemyStateMachine / BossStateMachine`(`class XxxStateMachine : StateMachine { }`)已删除,不再按角色分出有语义名的子类型。
- **状态切换流程**:`Initialize(start)` 置位 `canChangeState=true`、设 `currentState` 并调用 `Enter()`;`ChangeState(new)` 先判 `canChangeState`(锁住直接返回),再 `currentState.Exit() → 换引用 → new.Enter()`;`Update()` 每帧转发给 `currentState.Update()`。这是经典的 Enter/Update/Exit 状态模式。
- **动画 bool 约定**:`EntityState` 的 `Enter` 调 `SetAnimBool(true)`、`Exit` 调 `SetAnimBool(false)`(`EntityState.cs:21-35`)。但 `SetAnimBool` 对空 `animBoolName` 不做任何事——于是有两种动画驱动风格:主角/boss 给每个状态一个 Animator bool 走条件过渡;小怪状态 `animBoolName` 留空(`EnemyBaseState` 构造默认 `""`,见 `EnemyBaseState.cs:9`),不碰 bool,改在各状态里手动 `Anim.Play`。同一套基类同时支持两种风格。
- **三层继承**:`EntityState`(公共)→ 三个角色专属基类(`PlayerBaseState / EnemyBaseState / BossBaseState`)→ 具体状态。专属基类只负责把"owner 引用 + owner 的 Rb/Anim"灌进来,并加少量角色独有的钩子:
  - `Assets/Scripts/Player/PlayerBaseState.cs:5` 注入 `PlayerController + PlayerInput`,加全局过渡 `CheckGlobalTransitions()`(任意状态下检测识破/冲刺/技能键)和动画事件回调 `OnAnimationFinished/OnHitFrame/OnCounterWindowClosed`。
  - `Assets/Scripts/Enemy/Mobs/EnemyBaseState.cs:5` 注入 `GroundEnemy`,并在 `Enter` 里把 `stateTimer` 清零(保留小怪"进状态清计时"的旧行为)。
  - `Assets/Scripts/Enemy/Boss/BossBaseState.cs:4` 注入 `MinotaurBoss`,其余沿用基类。
- **Entity 层的公共能力**:朝向用 `localScale.x *= -1` 翻转并维护 `FacingRight`(`Entity.cs:42-48`);`DirToward(worldX)` 返回指向某点的水平符号,`FaceToward` 直接转身——省掉到处手写 `Mathf.Sign(目标.x - 自身.x)`。`CheckGrounded` 默认空,主角覆盖成 `OverlapCircle`,敌人不用(走射线检测悬崖/墙)。

**入口 & 触发**
- 状态机由各 owner 的 `MonoBehaviour` 在初始化时 `new` 出来并喂状态:
  - `Assets/Scripts/Player/PlayerController.cs:118` `new StateMachine()` → `:135` `Initialize(idleState)`,并把 `Stats.OnDeath` 接到 `ChangeState(deadState)`。
  - `Assets/Scripts/Enemy/Mobs/GroundEnemy.cs:87-94` `new StateMachine()` 后逐个 `new` 出 idle/move/chase/attack/stunned/dead 再 `Initialize(idleState)`。
  - `Assets/Scripts/Enemy/Boss/MinotaurBoss.cs:71-82` 同理 `new StateMachine()` + 一堆 boss 状态 + `Initialize(idleState)`。
- **每帧驱动**:owner 的 `Update` 调 `stateMachine.Update()`(`PlayerController.cs:148`、`GroundEnemy.cs:148`、`MinotaurBoss.cs:95`)。注意 owner 的 `Update` 是 `override Entity.Update`,会先 `base.Update()`(跑 `CheckGrounded`)再驱动状态机。
- **玩家行为如何触达**:玩家几乎所有操作(移动/跳/攻击/冲刺/识破/技能/受击/被处决/死亡)都是某个状态在 `Update` 里读输入后调 `stateMachine.ChangeState(...)`;`CheckGlobalTransitions()` 让冲刺/识破/技能在任意状态都能打断。敌人/boss 则由 AI 状态自身根据距离、计时、受击事件切换。

**依赖 & 被依赖**
- **本系统依赖**:仅 UnityEngine(`Rigidbody2D / Animator / Transform / LayerMask`)。`StateMachine.cs` 甚至不 `using` 任何东西,是纯 C#。底座不反向依赖任何业务系统。
- **被依赖(具体到类)**:
  - 角色 owner:`PlayerController : Entity`(`PlayerController.cs:7`)、`EnemyBase : Entity`(`EnemyBase.cs:6`,其下 `GroundEnemy`、`MinotaurBoss` 再派生)。它们持有 `stateMachine` 字段并调用 `Initialize/Update/ChangeState/Lock/Unlock`。
  - 所有具体状态类都 `extends` 三个专属基类→`EntityState`,例如 `Player_AttackState`、`Enemy_ChaseState`、`Boss_EnragedState` 等(见 `Assets/Scripts/Player/States/`、`Assets/Scripts/Enemy/Mobs/States/`、`Assets/Scripts/Enemy/Boss/States/`)。
  - 跨角色交互也走它:`MinotaurBoss.cs:145/169/206` 直接拿 `pc.stateMachine.ChangeState(...)` 把玩家踢进 idle/knocked;`BossIntroTrigger`、技能 `Skill_DashStrike/Skill_Heal`、`Player_ExecuteState` 等都通过 `stateMachine.ChangeState` 切玩家状态。
  - `Entity` 的朝向工具(`FaceToward/DirToward/FacingDir`)被大量移动/攻击状态调用做转身与位移方向。

**关键设计 / 易错点**
- **`canChangeState`(Lock/Unlock)几乎只为主角死亡服务**:全项目唯一的 `Lock()` 在 `Assets/Scripts/Player/States/Player_DeadState.cs:13`(死后锁死状态机),唯一的 `Unlock()` 在 `PlayerController.cs:170`(复活时解锁)。小怪/boss 从不锁,`canChangeState` 对它们恒为 `true`(注释见 `StateMachine.cs:6`)。改状态切换逻辑时记住:锁定时 `ChangeState` 是静默 no-op,死亡态下任何切状态尝试都会被吞掉。
- **`Initialize` vs `ChangeState` 不对称**:`Initialize` 不调 `Exit`(没有前一个状态),直接 `Enter`;且不受 `canChangeState` 影响。所以重置/复活若想"强切"且当前被 Lock,要么先 `Unlock()` 再 `ChangeState`,要么走 `Initialize`。
- **`ChangeState` 不做"同状态去重"**:切到当前同一个状态实例也会照样 `Exit()+Enter()`。需要避免自切的地方得自己判 `currentState == xxx`(如 `MinotaurBoss.cs:122/205` 就显式做了判断)。
- **状态实例是复用的,不是每次 new**:owner 在初始化时把每个状态各 `new` 一个长期持有(`idleState/attackState/...`),切换只换引用。所以状态里**不能假设 Enter 时字段是干净的**——需要的瞬时数据要在 `Enter` 里显式重置(`EnemyBaseState.Enter` 清 `stateTimer` 就是这个原因)。
- **动画 bool 留空的双轨制**:给状态写 `animBoolName` 时务必和该角色的驱动风格一致——主角/boss 填 bool 名(对应 Animator 里的过渡条件),小怪留空(`Anim.Play` 手动播)。填错会导致动画不切或 Animator 里找不到参数(`SetAnimBool` 已对 `runtimeAnimatorController==null` 做了保护,但参数名不存在仍会报警告)。
- **`IsGrounded` 是主角专属**:`Entity.CheckGrounded` 默认空,只有主角覆盖。敌人读 `IsGrounded` 永远是默认值,别在敌人状态里依赖它(注释见 `Entity.cs:7`)。
- **owner `Update` 的兜底**:`PlayerController.cs:144` 有 `if (stateMachine == null) return;`,防自毁重复单例实例在初始化前驱动空状态机——新增 owner 时同样要注意 `Awake`/`Update` 的时序。
