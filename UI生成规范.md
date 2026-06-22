# UI 生成规范

> 本项目所有 UI 的搭建/组织约定。新做 UI 前先过一遍这份。
> 配套:`动画驱动分类规范.md`、`攻击系统说明.md`。

## 核心原则(一句话)

**布局在场景 / prefab 里摆好,脚本只持引用 + 写逻辑(View 模式)。不在运行时用代码 `new GameObject` / `AddComponent` 搭界面层级。**

---

## 一、放哪里 —— 三类

| 类型 | 放哪 | 例子 |
|---|---|---|
| **跨场景常驻** | prefab 摆 `Bootstrap.unity` + 单例守卫 / `DontDestroyOnLoad` | 暂停菜单、角色面板、对话框、交互木框、商店、击破演出 |
| **单场景专用** | 摆在那个场景里(随场景加载 / 卸载) | 主菜单、主菜单的新游戏确认框 |
| **每场景一份** | 每个 gameplay 场景各放一个(依赖场内 player / boss 引用) | 战斗 HUD 血条 / 体力球 |

**判断**:别人也用 + 要跨场景活着 → 常驻 Bootstrap;只一个场景用 → 摆那个场景。
拿不准先放场景,真复用了再上移(别一上来就常驻)。

> 反例:`ConfirmDialog` 只有主菜单用 → 就该摆 MainMenu,不该常驻 Bootstrap。

---

## 二、脚本职责(View 模式)

脚本**只做两件事**:

1. `[SerializeField]` / public 引用场景里**已摆好**的子物体(panel / text / button / 列表容器…);
2. 显隐 + 数据刷新 + 按钮回调。

脚本**不做**:

- `new GameObject` / `AddComponent` 拼层级;
- 运行时算锚点 / 尺寸 / 位置去搭面板;
- 用代码加载 sprite / font 去拼外观(外观在 Inspector 配)。

约定:

- 命名 `XxxUI` / `XxxView`;
- 对外用**静态门面**:`Xxx.Show(...)` / `Xxx.Hide(who)` + `static Instance`,调用方不持引用(参考 `InteractPromptUI` / `ConfirmDialog`);
- `who` 参数("只有当前显示者能关")避免多方互相误关提示(见 `InteractPromptUI`)。

```csharp
// 标准瘦脚本长相
public class XxxUI : MonoBehaviour
{
    public static XxxUI Instance { get; private set; }
    public GameObject panelRoot;   // ← 引用场景里摆好的
    public TMP_Text   message;
    public Button     okButton;

    void Awake() { Instance = this; okButton.onClick.AddListener(OnOk); panelRoot.SetActive(false); }
    public static void Show(string t, Action onOk) { /* 设文字 + 开 panelRoot */ }
}
```

---

## 三、弹窗 / 面板结构约定

```
Xxx            (组件挂这, 常 active)
└ Root         (开关用; SetActive 切显隐)   ← toggle 这个, 不要 toggle 组件自己的 GameObject
   ├ Dim       (全屏暗底, 黑 a≈0.6, 挡点击 = 模态)
   └ Panel     (内容容器, 木框)
      └ 文字 / 按钮 / 列表 …
```

- 切显隐切的是 **Root**,不是组件所在的 GameObject(否则一关掉,组件的 `Show` 就回不来了);
- **模态**弹窗(确认 / 暂停 / 对话)要有 **Dim** 全屏暗底挡住后面点击;
- 非模态(头顶交互提示)不需要 Dim。

---

## 四、皮肤规范(统一木质风)

| 元素 | 资源 | Image 设置 |
|---|---|---|
| 面板 / 木框 | `SpriteSheet_66` | Type = **Sliced**(9 切片) |
| 按钮 | `SpriteSheet_0` | Type = Sliced |
| 字体 | `SimHei SDF`(TMP) | 正文 **黑字**(`#000000`) |
| 暗底 | 纯黑 Image | alpha ≈ 0.6,`raycastTarget` 开 |

- **9-slice**:木框 / 按钮的 sprite 必须切好 Border(Sprite Editor 里),Image Type 设 Sliced,缩放才不糊边;
- Canvas:Screen Space - Overlay;
- 弹窗放对应 Canvas 下,用 `SetAsLastSibling` 或更高 `sortingOrder` 盖住一切;
- 文字颜色统一**黑字**(木框是浅色,黑字最清楚);标题想要点缀色单独定,别整片白/金。

> 颜色 / 字号以后要做 token 就在这一节补一张表,先用上面这套。

---

## 五、层级 / 命名

- 场景根按功能分组:`_Systems` / `_Player` / `_Camera` / `_UI` / `_Interactables` / `_Level` / `_Event`;
- UI 对象进 `_UI`(常驻的在 Bootstrap 里同理分组);
- 弹窗内部用上面"Root / Dim / Panel"的命名,别一堆 `GameObject (1)`。

---

## 六、开局 / 性能 / 门禁

- 大 UI 走 prefab(布局预摆)→ 开局**无运行时生成开销**;
- `Awake` 里清静态 `IsOpen = false`(防 Play 反复测试残留);
- 进任意可游玩场景,基础态必须复位:`Time.timeScale = 1` / `GameManager.IsPaused = false` / 各面板 `IsOpen = false`(玩家移动逻辑会读这些,错一个就"开局不能动");
- 按键门禁:`MainMenu` / `Bootstrap` 不响应 `C`(角色面板)/ `Esc`(暂停)。

---

## 七、唯一例外:数据驱动的"项"

列表行 / 网格格子这种**同构、数量随数据变**的,可以用**工厂 / 模板**批量实例化(商店商品行、背包格子):

- 实例化的是**一个模板格子 / prefab 行**,不是手搭整张面板;
- 面板骨架(容器 / 分栏 / 标题 / 滚动区)仍在 prefab / 场景里**摆死**;
- 一句话:**面板摆死,项填充**。

参考:`商店UI重设计计划.md` 里的"左列表 + 右详情"、角色面板背包页的网格。

---

## 反面教材(本次踩的坑)

`ConfirmDialog` 起初在 `Awake` 里 `new GameObject` + `AddComponent` 程序化搭整个弹窗(Canvas / 暗底 / 木框 / 文字 / 两个按钮)→ 代码臃肿、外观不可视编辑、和上面约定全冲突。

已改正为标准做法:

- UI 摆进 `MainMenu` 场景成**实体对象**(`ConfirmDialog/Root/Dim/Panel/Message/BtnOk/BtnCancel`);
- 脚本瘦成只引用 4 个子物体(`panelRoot` / `message` / `okButton` / `cancelButton`)+ `Show/Hide` 逻辑;
- 木框 / 按钮 / 字体在 Inspector 配好,随时可视调。

**新做 UI 时对照本规范,别再程序化搭面板。**
