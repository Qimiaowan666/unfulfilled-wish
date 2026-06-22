using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 独立确认弹窗(挂在用到它的场景里,如 MainMenu;和暂停菜单【无关】)。
// UI 在场景里摆好(暗底 + 木框 + 文字 + 确定/取消),这里只引用 + 逻辑,不程序化生成。
//   ConfirmDialog.Show(onYes);          // 文字在场景里 Message 上设,不代码传
//   ConfirmDialog.Show(onYes, onNo);
public class ConfirmDialog : MonoBehaviour
{
    public static ConfirmDialog Instance { get; private set; }

    [Tooltip("弹窗根(暗底 + 面板,开关用)")]
    public GameObject panelRoot;
    public TMP_Text   message;
    public Button     okButton;
    public Button     cancelButton;

    Action onYes, onNo;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (okButton     != null) { okButton.onClick.RemoveAllListeners();     okButton.onClick.AddListener(OnYes); }
        if (cancelButton != null) { cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(OnNo); }
        if (panelRoot != null) panelRoot.SetActive(false);
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    // 文字在场景里 message 那个 TMP 上直接写(不再代码传入);这里只负责显隐 + 回调
    public static void Show(Action onYes, Action onNo = null)
    {
        if (Instance == null) { onYes?.Invoke(); return; }   // 没实例兜底:直接当确定
        Instance.onYes = onYes; Instance.onNo = onNo;
        if (Instance.panelRoot != null) Instance.panelRoot.SetActive(true);
    }

    void OnYes() { var a = onYes; Hide(); a?.Invoke(); }
    void OnNo()  { var a = onNo;  Hide(); a?.Invoke(); }
    void Hide()  { onYes = null; onNo = null; if (panelRoot != null) panelRoot.SetActive(false); }
}
