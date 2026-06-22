using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 统一暂停菜单（View 版）：布局在 PauseMenu.prefab 里摆好，这个脚本只引用节点 + 跑逻辑。
/// 主菜单（继续/存档/读档/设置/返回主菜单/退出）+ 存档/读档子面板 + 确认弹窗。常驻单例，挂在 Bootstrap。
/// </summary>
[DefaultExecutionOrder(100)]
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [Header("根 / 标题")]
    public GameObject panelRoot;        // 开关用的根（overlay）
    public RectTransform windowRect;    // 木框窗口（设置子页会缩小它）
    public TMP_Text titleText;

    [Header("两组：主菜单 / 存读档")]
    public GameObject mainGroup;
    public GameObject slotsGroup;

    [Header("主菜单按钮")]
    public Button continueButton;
    public Button saveButton;
    public Button loadButton;
    public Button settingsButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("存读档（数组长度 = 槽数，按顺序连）")]
    public Button backButton;
    public RawImage[] slotThumbs;
    public TMP_Text[] slotInfos;
    public Button[] slotCards;
    public Button[] slotDeletes;

    [Header("确认弹窗")]
    public GameObject confirmPanel;
    public TMP_Text confirmText;
    public Button confirmOkButton;
    public Button confirmCancelButton;

    [Header("设置子页")]
    public GameObject settingsGroup;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Toggle fullscreenToggle;
    public Dropdown resolutionDropdown;
    public Button settingsBackButton;

    // 场景名 / 非游玩判定统一见 SceneNames

    enum Page { Main, Save, Load, Settings }
    bool settingsBound;
    bool standalone;   // 主菜单模式：不暂停游戏，返回/ESC = 关闭面板
    Page page;
    Action pendingConfirm;
    GameManager subscribedGM;
    Texture2D[] loadedThumbs;

    // ─────────────────────────────────────────────────────
    // 生命周期
    // ─────────────────────────────────────────────────────
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
        loadedThumbs = new Texture2D[slotCards != null ? slotCards.Length : 0];
    }

    void Start()
    {
        BindButtons();
        Subscribe();
        if (panelRoot != null) panelRoot.SetActive(false);
        IsOpen = false;
    }

    void OnDestroy() => Unsubscribe();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame)
            return;

        PlayerInputBuffer.ClearAll();

        if (IsOpen)
        {
            AudioManager.Instance?.PlayUIClick();   // ESC 返回/关闭
            if (confirmPanel != null && confirmPanel.activeSelf) HideConfirm();
            else if (standalone) CloseStandalone();
            else if (page != Page.Main) ShowMain();
            else Resume();
            return;
        }

        if (CanOpenPause())
            GameManager.Instance.TogglePause();
    }

    // ─────────────────────────────────────────────────────
    // 按钮绑定
    // ─────────────────────────────────────────────────────
    void BindButtons()
    {
        Wire(continueButton, Resume);
        Wire(saveButton, ShowSave);
        Wire(loadButton, ShowLoad);
        Wire(settingsButton, OnSettings);
        Wire(mainMenuButton, OnReturnMainMenu);
        Wire(quitButton, OnQuit);
        Wire(backButton, Back);
        Wire(settingsBackButton, Back);
        Wire(confirmOkButton, AcceptConfirm);
        Wire(confirmCancelButton, HideConfirm);

        if (slotCards != null)
        {
            for (int i = 0; i < slotCards.Length; i++)
            {
                int idx = i;
                Wire(slotCards[i], () => OnSlotClicked(idx));
                if (slotDeletes != null && i < slotDeletes.Length)
                    Wire(slotDeletes[i], () => OnDeleteClicked(idx));
            }
        }
    }

    static void Wire(Button button, Action action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); PlayerInputBuffer.ClearAll(); action?.Invoke(); });
    }

    // ─────────────────────────────────────────────────────
    // 暂停开关
    // ─────────────────────────────────────────────────────
    void Subscribe()
    {
        Unsubscribe();
        subscribedGM = GameManager.Instance;
        if (subscribedGM == null) return;
        subscribedGM.OnGamePaused += Open;
        subscribedGM.OnGameResumed += Close;
    }

    void Unsubscribe()
    {
        if (subscribedGM == null) return;
        subscribedGM.OnGamePaused -= Open;
        subscribedGM.OnGameResumed -= Close;
        subscribedGM = null;
    }

    bool CanOpenPause()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return false;
        if (ShopUI.IsOpen || CharacterPanelUI.IsOpen || VictoryUI.IsOpen || DialogueUI.IsPlaying || BossIntroTrigger.Sequencing || BossFinishUI.IsPlaying || MinotaurBoss.PhaseTransition) return false;

        if (SceneNames.IsNonGameplay(SceneManager.GetActiveScene().name)) return false;

        return true;
    }

    public void Resume()
    {
        PlayerInputBuffer.ClearAll();
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            GameManager.Instance.TogglePause();
    }

    void Open()
    {
        AudioManager.Instance?.PlayUIOpen();   // 暂停菜单弹出
        if (panelRoot != null) panelRoot.SetActive(true);
        ShowMain();
        IsOpen = true;
    }

    void Close()
    {
        HideConfirm();
        if (panelRoot != null) panelRoot.SetActive(false);
        IsOpen = false;
    }

    // ── 主菜单独立调用：直接显示读档 / 设置子面板（不暂停游戏）──
    public void OpenLoadStandalone()
    {
        standalone = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        ShowLoad();
        IsOpen = true;
    }

    public void OpenSettingsStandalone()
    {
        standalone = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        ShowSettings();
        IsOpen = true;
    }

    void Back()
    {
        if (standalone) CloseStandalone();
        else ShowMain();
    }

    void CloseStandalone()
    {
        standalone = false;
        IsOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ─────────────────────────────────────────────────────
    // 页面切换
    // ─────────────────────────────────────────────────────
    void ShowMain()
    {
        page = Page.Main;
        if (titleText != null) titleText.text = "暂停";
        SetWindowSize(false);
        SetActive(mainGroup, true);
        SetActive(slotsGroup, false);
        SetActive(settingsGroup, false);
        if (saveButton != null) saveButton.interactable = !EnemyBase.AnyBossInCombat();   // boss 战期间禁止存档
        HideConfirm();
    }

    void ShowSave()
    {
        page = Page.Save;
        if (titleText != null) titleText.text = "存档";
        SetActive(mainGroup, false);
        SetActive(slotsGroup, true);
        SetActive(settingsGroup, false);
        RefreshSlots();
    }

    void ShowLoad()
    {
        page = Page.Load;
        if (titleText != null) titleText.text = "读档";
        SetActive(mainGroup, false);
        SetActive(slotsGroup, true);
        SetActive(settingsGroup, false);
        RefreshSlots();
    }

    static void SetActive(GameObject go, bool value)
    {
        if (go != null) go.SetActive(value);
    }

    void SetWindowSize(bool small)
    {
        if (windowRect != null)
            windowRect.sizeDelta = small ? new Vector2(620f, 600f) : new Vector2(760f, 880f);
    }

    // ─────────────────────────────────────────────────────
    // 存 / 读 / 删
    // ─────────────────────────────────────────────────────
    void RefreshSlots()
    {
        var save = SaveSystem.Instance;
        int n = slotCards != null ? slotCards.Length : 0;

        for (int i = 0; i < n; i++)
        {
            bool has = save != null && save.HasSlot(i);
            var meta = has ? save.GetSlotMeta(i) : null;

            UpdateThumb(i, has && save != null ? save.LoadThumbnail(i) : null);

            if (slotInfos != null && i < slotInfos.Length && slotInfos[i] != null)
                slotInfos[i].text = has
                    ? $"<b>槽 {i + 1}</b>\n{SceneStr(meta.sceneName)}\n{TimeStr(meta.savedAtUtc)}   金币 {meta.gold}"
                    : $"<b>槽 {i + 1}</b>\n<color=#7c766a>空档位</color>";

            if (slotCards[i] != null)
                slotCards[i].interactable = page == Page.Save || has;
            if (slotDeletes != null && i < slotDeletes.Length && slotDeletes[i] != null)
                slotDeletes[i].gameObject.SetActive(has);
        }
    }

    void UpdateThumb(int slot, Texture2D tex)
    {
        if (loadedThumbs == null || slot >= loadedThumbs.Length) return;

        if (loadedThumbs[slot] != null)
        {
            Destroy(loadedThumbs[slot]);
            loadedThumbs[slot] = null;
        }
        loadedThumbs[slot] = tex;

        if (slotThumbs != null && slot < slotThumbs.Length && slotThumbs[slot] != null)
        {
            slotThumbs[slot].texture = tex;
            slotThumbs[slot].color = tex != null ? Color.white : new Color(0.10f, 0.10f, 0.12f, 1f);
        }
    }

    void OnSlotClicked(int slot)
    {
        var save = SaveSystem.Instance;
        if (save == null) return;

        if (page == Page.Save)
        {
            if (EnemyBase.AnyBossInCombat()) return;   // boss 战期间禁止存档（兜底）
            if (save.HasSlot(slot))
                ShowConfirm($"覆盖存档槽 {slot + 1}？", () => StartCoroutine(SaveRoutine(slot)));
            else
                StartCoroutine(SaveRoutine(slot));
        }
        else if (page == Page.Load)
        {
            if (!save.HasSlot(slot)) return;
            ShowConfirm($"读取存档槽 {slot + 1}？\n当前未保存的进度会丢失。", () =>
            {
                if (standalone) CloseStandalone(); else Resume();
                save.LoadSlot(slot);
            });
        }
    }

    void OnDeleteClicked(int slot)
    {
        var save = SaveSystem.Instance;
        if (save == null) return;
        ShowConfirm($"删除存档槽 {slot + 1}？", () =>
        {
            save.DeleteSlot(slot);
            RefreshSlots();
        });
    }

    // 存档前临时隐藏整个菜单，截一张干净游戏画面再恢复
    IEnumerator SaveRoutine(int slot)
    {
        var save = SaveSystem.Instance;
        if (save == null) yield break;

        if (panelRoot != null) panelRoot.SetActive(false);
        yield return new WaitForEndOfFrame();
        save.SaveToSlot(slot);
        if (panelRoot != null) panelRoot.SetActive(true);
        RefreshSlots();
    }

    // ─────────────────────────────────────────────────────
    // 主菜单动作
    // ─────────────────────────────────────────────────────
    void OnSettings() => ShowSettings();

    void ShowSettings()
    {
        page = Page.Settings;
        if (titleText != null) titleText.text = "设置";
        SetWindowSize(true);
        SetActive(mainGroup, false);
        SetActive(slotsGroup, false);
        SetActive(settingsGroup, true);
        HideConfirm();
        InitSettings();
    }

    void InitSettings()
    {
        if (!settingsBound)
        {
            settingsBound = true;
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(GameSettings.SetBGMVolume);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(GameSettings.SetSFXVolume);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);
            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.AddListener(GameSettings.SetResolution);
                resolutionDropdown.ClearOptions();
                var opts = new System.Collections.Generic.List<string>();
                foreach (var r in GameSettings.Resolutions) opts.Add($"{r.x} × {r.y}");
                resolutionDropdown.AddOptions(opts);
            }
        }

        // 填当前值（不触发回调）
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(GameSettings.BGMVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SFXVolume);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(GameSettings.CurrentResolutionIndex());
    }

    void OnReturnMainMenu() => ShowConfirm("返回主菜单？\n当前未保存的进度会丢失。", () =>
    {
        Close();
        GameManager.Instance?.LoadScene(SceneNames.MainMenu);
    });

    void OnQuit() => ShowConfirm("退出游戏？\n当前未保存的进度会丢失。", () =>
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    });

    // ─────────────────────────────────────────────────────
    // 确认 / 消息弹窗
    // ─────────────────────────────────────────────────────
    void ShowConfirm(string text, Action onYes)
    {
        pendingConfirm = onYes;
        if (confirmText != null) confirmText.text = text;
        if (confirmCancelButton != null) confirmCancelButton.gameObject.SetActive(true);
        if (confirmPanel != null) confirmPanel.SetActive(true);
    }


    void ShowMessage(string title, string body)
    {
        pendingConfirm = null;
        if (confirmText != null) confirmText.text = $"<b>{title}</b>\n{body}";
        if (confirmCancelButton != null) confirmCancelButton.gameObject.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    void HideConfirm()
    {
        pendingConfirm = null;
        if (confirmCancelButton != null) confirmCancelButton.gameObject.SetActive(true);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    void AcceptConfirm()
    {
        var action = pendingConfirm;
        HideConfirm();
        action?.Invoke();
    }

    // ─────────────────────────────────────────────────────
    static string TimeStr(string utc)
    {
        if (DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("MM-dd HH:mm");
        return "";
    }

    static string SceneStr(string scene)
    {
        switch (scene)
        {
            case SceneNames.Tutorial:       return "教程";
            case SceneNames.ForsakenShrine: return "被弃神殿";
            default: return string.IsNullOrEmpty(scene) ? "未知" : scene;
        }
    }
}
