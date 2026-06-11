using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 通关胜利画面：boss 击败后由 LevelManager 显示。常驻单例，布局在 VictoryUI.prefab 里摆好（木框风）。
/// </summary>
public class VictoryUI : MonoBehaviour
{
    public static VictoryUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [Header("引用")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button menuButton;   // 回主菜单
    public Button quitButton;

    const string MainMenuScene = "MainMenu";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (menuButton != null) menuButton.onClick.AddListener(OnMenu);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        if (panelRoot != null) panelRoot.SetActive(false);
        IsOpen = false;
    }

    public void Show()
    {
        if (panelRoot == null) return;
        PlayerInputBuffer.ClearAll();
        Time.timeScale = 0f;
        panelRoot.SetActive(true);
        IsOpen = true;
    }

    void OnMenu()
    {
        IsOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        GameManager.Instance?.LoadScene(MainMenuScene);
    }

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
