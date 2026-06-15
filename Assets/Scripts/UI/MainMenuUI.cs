using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单：新游戏 / 继续 / 读档 / 设置 / 退出。布局在场景里摆好（木框风），这里只引用按钮 + 逻辑。
/// 读档、设置直接复用常驻的 PauseMenu 子面板（OpenLoadStandalone / OpenSettingsStandalone）。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public string firstGameplayScene = "ForsakenShrine";

    [Header("按钮")]
    public Button newGameButton;
    public Button continueButton;
    public Button loadButton;
    public Button settingsButton;
    public Button quitButton;

    void Start()
    {
        Wire(newGameButton, NewGame);
        Wire(continueButton, Continue);
        Wire(loadButton, OpenLoad);
        Wire(settingsButton, OpenSettings);
        Wire(quitButton, QuitGame);

        // 没有存档时，「继续」灰掉
        bool hasSave = SaveSystem.Instance != null && SaveSystem.Instance.HasSave();
        if (continueButton != null) continueButton.interactable = hasSave;
    }

    static void Wire(Button b, System.Action a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => a?.Invoke());
    }

    public void NewGame()
    {
        SaveSystem.Instance?.ResetForNewGame();     // 清档 + 清空常驻装备/背包/技能，真正从头开始
        LoadScene(firstGameplayScene);
    }

    public void Continue()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Load() : null;
        string scene = (data != null && !string.IsNullOrEmpty(data.sceneName)) ? data.sceneName : firstGameplayScene;
        SaveSystem.Instance?.RequestFullReload();   // 进场景后由 ApplyOnSceneLoaded 恢复存档
        LoadScene(scene);
    }

    void OpenLoad()     => PauseMenu.Instance?.OpenLoadStandalone();
    void OpenSettings() => PauseMenu.Instance?.OpenSettingsStandalone();

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LoadScene(string scene)
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.LoadScene(scene);
        else SceneManager.LoadScene(scene);
    }
}
