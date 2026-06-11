using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// 角色面板控制器（挂在 CharacterPanel.prefab 根，常驻单例）。
// UI 布局全部在 prefab 里画好，这里只管：开关(C) / 切页(1-4) / 订阅系统刷新 / 暂停游戏 / 隐藏 HUD。
// 各页内容由对应的 CharacterPageView 子类填。
public class CharacterPanelUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static CharacterPanelUI Instance { get; private set; }

    [Header("开关用的内容根（Header+Content+Footer 的父；留空=自身）")]
    public GameObject panelRoot;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text goldText;
    [Tooltip("顺序：状态 / 装备 / 背包 / 技能")]
    public Button[] tabButtons;

    [Header("四页 View（顺序对应 tab）")]
    public CharacterPageView[] pages;

    // 非游玩场景：主菜单 / Bootstrap 里不允许开角色面板
    static readonly string[] NonGameplayScenes = { "MainMenu", "Bootstrap" };

    static readonly string[] TabTitles = { "状态", "装备", "背包", "技能" };
    static readonly Color TabOn      = new Color(1f, 1f, 1f, 1f);          // 选中：木牌原色(最亮)
    static readonly Color TabOff     = new Color(0.66f, 0.63f, 0.60f, 1f); // 未选中：略压暗
    static readonly Color TabTextOn  = new Color(0.15f, 0.10f, 0.05f);     // 选中：深棕黑(醒目)
    static readonly Color TabTextOff = new Color(0.34f, 0.27f, 0.20f);     // 未选中：棕

    int currentTab;
    bool isOpen, pausedByPanel, hasSubscribed;
    float previousTimeScale = 1f;
    PlayerStats subStats; EquipmentSystem subEquip; InventorySystem subInv; SkillSystem subSkills;
    readonly List<GameObject> hiddenHud = new List<GameObject>();
    readonly List<bool> hiddenHudPrev = new List<bool>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        isOpen = false; IsOpen = false; pausedByPanel = false;
    }

    void Start()
    {
        BindTabs();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnDisable()
    {
        if (pausedByPanel) ResumeTime();
        RestoreHud();
        if (isOpen) { isOpen = false; IsOpen = false; }
        Unsubscribe();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 主菜单 / Bootstrap：面板不可用；若从游玩场景残留打开则关掉
        if (!InGameplayScene())
        {
            if (isOpen) SetOpen(false);
            return;
        }

        if (!isOpen && ShopUI.IsOpen) return;
        if (kb.cKey.wasPressedThisFrame) SetOpen(!isOpen);
        if (!isOpen) return;
        if (kb.digit1Key.wasPressedThisFrame) SetTab(0);
        if (kb.digit2Key.wasPressedThisFrame) SetTab(1);
        if (kb.digit3Key.wasPressedThisFrame) SetTab(2);
        if (kb.digit4Key.wasPressedThisFrame) SetTab(3);
    }

    static bool InGameplayScene()
    {
        string active = SceneManager.GetActiveScene().name;
        foreach (var s in NonGameplayScenes)
            if (active == s) return false;
        return true;
    }

    void SetOpen(bool value)
    {
        if (value && ShopUI.IsOpen) return;
        isOpen = value; IsOpen = value;
        PlayerInputBuffer.ClearAll();
        if (panelRoot != null) panelRoot.SetActive(isOpen);

        if (isOpen) { Resubscribe(); HideHud(); PauseTime(); Refresh(); }
        else { HideAllPages(); ResumeTime(); RestoreHud(); }
    }

    void SetTab(int idx)
    {
        int max = pages != null ? pages.Length - 1 : 0;
        currentTab = Mathf.Clamp(idx, 0, Mathf.Max(0, max));
        Refresh();
    }

    void Refresh()
    {
        if (!isOpen || pages == null) return;

        if (titleText != null && currentTab < TabTitles.Length) titleText.text = TabTitles[currentTab];
        if (goldText != null) { var s = FindAnyObjectByType<PlayerStats>(); goldText.text = s != null ? $"金币 {s.gold}" : "金币 0"; }

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] == null) continue;
            if (i == currentTab) pages[i].Show();
            else pages[i].Hide();
        }
        UpdateTabVisual();
    }

    void HideAllPages()
    {
        if (pages == null) return;
        foreach (var p in pages) p?.Hide();
    }

    void UpdateTabVisual()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            bool on = i == currentTab;
            var img = tabButtons[i].GetComponent<Image>();
            if (img != null) img.color = on ? TabOn : TabOff;
            var txt = tabButtons[i].GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.color = on ? TabTextOn : TabTextOff;
        }
    }

    void BindTabs()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            int idx = i;
            tabButtons[i].onClick.RemoveAllListeners();
            tabButtons[i].onClick.AddListener(() => SetTab(idx));
        }
    }

    // ── 订阅系统变化 → 刷新 ──────────────────────────────────────────
    void Subscribe()
    {
        if (!isActiveAndEnabled || hasSubscribed) return;

        subStats = FindAnyObjectByType<PlayerStats>();
        if (subStats != null) subStats.OnStatsChanged += Refresh;

        subEquip = EquipmentSystem.Instance != null ? EquipmentSystem.Instance : FindAnyObjectByType<EquipmentSystem>();
        if (subEquip != null) subEquip.OnEquipmentChanged += Refresh;

        subInv = InventorySystem.Instance != null ? InventorySystem.Instance : FindAnyObjectByType<InventorySystem>();
        if (subInv != null) subInv.OnInventoryChanged += Refresh;

        subSkills = SkillSystem.Instance != null ? SkillSystem.Instance : FindAnyObjectByType<SkillSystem>();
        if (subSkills != null) subSkills.OnSkillsChanged += Refresh;

        hasSubscribed = true;
    }

    void Resubscribe() { Unsubscribe(); Subscribe(); }

    void Unsubscribe()
    {
        if (!hasSubscribed) return;
        if (subStats != null)  subStats.OnStatsChanged   -= Refresh; subStats = null;
        if (subEquip != null)  subEquip.OnEquipmentChanged -= Refresh; subEquip = null;
        if (subInv != null)    subInv.OnInventoryChanged  -= Refresh; subInv = null;
        if (subSkills != null) subSkills.OnSkillsChanged   -= Refresh; subSkills = null;
        hasSubscribed = false;
    }

    // ── 暂停游戏时间 ────────────────────────────────────────────────
    void PauseTime()
    {
        if (pausedByPanel) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pausedByPanel = true;
    }

    void ResumeTime()
    {
        if (!pausedByPanel) return;
        Time.timeScale = GameManager.Instance != null && GameManager.Instance.IsPaused ? 0f : previousTimeScale;
        pausedByPanel = false;
    }

    // ── 打开时隐藏战斗 HUD ──────────────────────────────────────────
    void HideHud()
    {
        RestoreHud();
        string[] names = { "HUD_Canvas", "HUD", "CombatHUDCanvas", "ExecutePromptCanvas", "TeleportCanvas", "PlayerHUD_Canvas" };
        foreach (var n in names)
        {
            var hud = GameObject.Find(n);
            if (hud == null || hud == gameObject) continue;
            hiddenHud.Add(hud);
            hiddenHudPrev.Add(hud.activeSelf);
            hud.SetActive(false);
        }
    }

    void RestoreHud()
    {
        for (int i = 0; i < hiddenHud.Count; i++)
            if (hiddenHud[i] != null) hiddenHud[i].SetActive(hiddenHudPrev[i]);
        hiddenHud.Clear();
        hiddenHudPrev.Clear();
    }
}
