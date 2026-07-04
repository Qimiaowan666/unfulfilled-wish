using UnityEngine;
using TMPro;

// 教程提示框(uGUI + TMP,木框画风同菜单/商店)。显示当前教学文字 + 进度计数。
// 不暂停、不锁输入。UI 在场景里预置(编辑器构建),静态接口驱动;切场景随场景销毁。
public class TutorialPromptUI : MonoBehaviour
{
    static TutorialPromptUI instance;

    [Tooltip("木框面板根(整体显示/隐藏)")]
    public GameObject box;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    [Tooltip("进度计数行,如「格挡 1/3」")]
    public TMP_Text statusText;

    void Awake()
    {
        instance = this;
        if (box != null) box.SetActive(false);   // 初始隐藏,等 Show
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // 显示一条教学提示(替换上一条),清空计数行
    public static void Show(string title, string body)
    {
        if (instance == null) return;
        if (instance.titleText != null)
        {
            instance.titleText.text = title ?? "";
            instance.titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }
        if (instance.bodyText != null) instance.bodyText.text = body ?? "";
        SetStatusInternal("");
        if (instance.box != null) instance.box.SetActive(true);
    }

    public static void Hide()
    {
        if (instance != null && instance.box != null) instance.box.SetActive(false);
    }

    // 进度计数(由机关在玩家做对动作时调),写在框里
    public static void SetStatus(string s) => SetStatusInternal(s);

    static void SetStatusInternal(string s)
    {
        if (instance == null || instance.statusText == null) return;
        instance.statusText.text = s ?? "";
        instance.statusText.gameObject.SetActive(!string.IsNullOrEmpty(s));
    }
}
