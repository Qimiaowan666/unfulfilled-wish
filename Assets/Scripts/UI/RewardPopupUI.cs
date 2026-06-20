using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// 奖励弹窗:屏幕正中独立木框,显示"获得了什么"。
// 弹出期间 timeScale=0 暂停一切;按 鼠标左键 或 F 关闭;无自动消失时间。
// 常驻单例,实例摆 Bootstrap.unity(随其它管理器),Awake 里 DontDestroyOnLoad。
public class RewardPopupUI : MonoBehaviour
{
    public static RewardPopupUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [Tooltip("弹窗可见部分(暗化遮罩 + 中央木框)的根,显隐用")]
    public GameObject root;
    [Tooltip("标题(静态,默认「获得」)")]
    public TMP_Text titleText;
    [Tooltip("正文(开箱时填入获得的内容)")]
    public TMP_Text bodyText;
    [Tooltip("底部提示(静态,默认「左键 / F 继续」)")]
    public TMP_Text hintText;

    float prevTimeScale = 1f;
    int   openedFrame;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        if (root != null) root.SetActive(false);
        IsOpen = false;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (IsOpen) { Time.timeScale = prevTimeScale; IsOpen = false; }   // 销毁时别把游戏卡在暂停
    }

    public static void Show(string content)
    {
        if (Instance != null) Instance.Open(content);
    }

    void Open(string content)
    {
        if (bodyText != null) bodyText.text = content;   // 标题/提示是静态的(在 prefab 上改)
        if (root != null) root.SetActive(true);
        if (!IsOpen) prevTimeScale = Time.timeScale;   // 记住进来前的流速(只在首次记,防重复打开把 0 存进去)
        IsOpen = true;
        Time.timeScale = 0f;                           // 暂停一切
        openedFrame = Time.frameCount;
    }

    void Close()
    {
        if (root != null) root.SetActive(false);
        IsOpen = false;
        Time.timeScale = prevTimeScale;                // 恢复进来前的流速
    }

    void Update()
    {
        // timeScale=0 时 Update 仍跑;跳过打开当帧,避免"开箱那次 F"立刻把弹窗关掉
        if (!IsOpen || Time.frameCount == openedFrame) return;
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        bool dismiss = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    || (kb != null && kb.fKey.wasPressedThisFrame);
        if (dismiss) Close();
    }
}
