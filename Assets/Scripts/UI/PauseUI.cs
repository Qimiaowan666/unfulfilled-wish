using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseUI : MonoBehaviour
{
    const string OverlayName = "InkPauseOverlay";
    const string MainMenuSceneName = "MainMenu";
    const string BackgroundResourcePath = "UI/PauseReference/pause_menu_ink_vertical";

    static readonly Color OverlayColor = new Color(0.02f, 0.02f, 0.02f, 0.72f);
    static readonly Color TransparentButtonColor = new Color(1f, 1f, 1f, 0.01f);
    static readonly Color HighlightButtonColor = new Color(0.12f, 0.12f, 0.10f, 0.13f);
    static readonly Color PressedButtonColor = new Color(0.08f, 0.08f, 0.06f, 0.22f);
    static readonly Color InkTextColor = new Color(0.09f, 0.08f, 0.07f, 1f);
    static readonly Color MutedInkTextColor = new Color(0.27f, 0.25f, 0.22f, 1f);
    static readonly Color PaperPanelColor = new Color(0.78f, 0.76f, 0.68f, 0.96f);
    static readonly Color CinnabarColor = new Color(0.58f, 0.06f, 0.04f, 1f);

    [Header("Artwork")]
    public Sprite pauseArtwork;

    GameObject overlay;
    GameObject artworkRoot;
    GameObject confirmPanel;
    Button continueButton;
    Button settingsButton;
    Button mainMenuButton;
    Button quitButton;
    Button confirmCancelButton;
    Button confirmAcceptButton;
    Text confirmTitleText;
    Text confirmBodyText;
    Text confirmCancelText;
    Text confirmAcceptText;
    Font uiFont;
    bool uiBuilt;
    Action pendingConfirmAction;
    GameManager subscribedGameManager;
    readonly List<Button> selectableButtons = new List<Button>();
    readonly List<Image> selectionMarks = new List<Image>();

    public static PauseUI Instance { get; private set; }

    void Awake()
    {
        // 常驻单例：全局唯一，跨场景不销毁（业界标准的全局 UI 处理）
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        SubscribeGameManager();

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            ShowPanel();
    }

    void OnDestroy()
    {
        UnsubscribeGameManager();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        PlayerInputBuffer.ClearAll();

        if (IsConfirmVisible())
        {
            CancelConfirm();
            return;
        }

        if (IsPauseMenuVisible() && GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            Resume();
            return;
        }

        if (!CanOpenPause())
            return;

        GameManager.Instance.TogglePause();
    }

    public void Resume()
    {
        PlayerInputBuffer.ClearAll();

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            GameManager.Instance.TogglePause();
    }

    public void ShowSettingsMessage()
    {
        ShowMessage(
            "设置暂未开放",
            "后续会在这里加入音量、画面、操作和辅助选项。\n当前版本先保留入口。");
    }

    public void ReturnToMainMenu()
    {
        ShowConfirm(
            "返回主菜单？",
            "当前未保存的进度会丢失。",
            () =>
            {
                PlayerInputBuffer.ClearAll();
                HideAll();
                GameManager.Instance?.LoadScene(MainMenuSceneName);
            });
    }

    public void QuitGame()
    {
        ShowConfirm(
            "退出游戏？",
            "当前未保存的进度会丢失。",
            () =>
            {
                PlayerInputBuffer.ClearAll();
                HideAll();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
    }

    // 这些场景不是 gameplay，禁止暂停（PauseUI 常驻，会跟到这些场景）
    static readonly string[] NonPausableScenes = { "MainMenu", "Bootstrap" };

    bool CanOpenPause()
    {
        if (GameManager.Instance == null) return false;
        if (GameManager.Instance.IsGameOver) return false;
        if (GameManager.Instance.IsPaused) return false;
        if (ShopUI.IsOpen) return false;
        if (CharacterPanelUI.IsOpen) return false;
        if (Time.timeScale <= 0f) return false;

        string active = SceneManager.GetActiveScene().name;
        foreach (var s in NonPausableScenes)
            if (active == s) return false;

        return true;
    }

    void SubscribeGameManager()
    {
        UnsubscribeGameManager();

        subscribedGameManager = GameManager.Instance;
        if (subscribedGameManager == null) return;

        subscribedGameManager.OnGamePaused += ShowPanel;
        subscribedGameManager.OnGameResumed += HidePanel;
    }

    void UnsubscribeGameManager()
    {
        if (subscribedGameManager == null) return;

        subscribedGameManager.OnGamePaused -= ShowPanel;
        subscribedGameManager.OnGameResumed -= HidePanel;
        subscribedGameManager = null;
    }

    void EnsureUI()
    {
        if (uiBuilt) return;
        uiBuilt = true;

        ConfigureHostCanvas();

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (pauseArtwork == null)
            pauseArtwork = Resources.Load<Sprite>(BackgroundResourcePath);

        overlay = BuildOverlay(transform);
        artworkRoot = BuildArtworkRoot(overlay.transform);
        BuildArtworkBackground(artworkRoot.transform);
        BuildHotspots(artworkRoot.transform);
        BuildConfirmPanel(overlay.transform);

        overlay.SetActive(false);
    }

    void ConfigureHostCanvas()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    GameObject BuildOverlay(Transform parent)
    {
        var root = CreateUIObject(OverlayName, parent);
        Stretch(root.GetComponent<RectTransform>());

        var image = root.AddComponent<Image>();
        image.color = OverlayColor;
        image.raycastTarget = true;

        return root;
    }

    GameObject BuildArtworkRoot(Transform parent)
    {
        var root = CreateUIObject("PauseArtworkRoot", parent);
        SetRect(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(760f, 878f));
        return root;
    }

    void BuildArtworkBackground(Transform parent)
    {
        var background = CreateUIObject("PauseArtworkBackground", parent);
        Stretch(background.GetComponent<RectTransform>());

        var image = background.AddComponent<Image>();
        image.sprite = pauseArtwork;
        image.preserveAspect = true;
        image.color = pauseArtwork == null ? new Color(0.72f, 0.70f, 0.64f, 1f) : Color.white;
        image.raycastTarget = false;
    }

    void BuildHotspots(Transform parent)
    {
        continueButton = BuildHotspotButton(parent, "ContinueButton", new Vector2(0f, 112f), new Vector2(440f, 92f), Resume);
        settingsButton = BuildHotspotButton(parent, "SettingsButton", new Vector2(0f, 0f), new Vector2(440f, 92f), ShowSettingsMessage);
        mainMenuButton = BuildHotspotButton(parent, "MainMenuButton", new Vector2(0f, -118f), new Vector2(440f, 92f), ReturnToMainMenu);
        quitButton = BuildHotspotButton(parent, "QuitButton", new Vector2(0f, -236f), new Vector2(440f, 92f), QuitGame, true);
    }

    Button BuildHotspotButton(Transform parent, string objectName, Vector2 position, Vector2 size, Action action, bool danger = false)
    {
        var buttonObject = CreateUIObject(objectName, parent);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, size);

        var image = buttonObject.AddComponent<Image>();
        image.color = TransparentButtonColor;
        image.raycastTarget = true;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            PlayerInputBuffer.ClearAll();
            action?.Invoke();
        });

        var colors = button.colors;
        colors.normalColor = TransparentButtonColor;
        colors.highlightedColor = danger ? new Color(0.55f, 0.04f, 0.03f, 0.12f) : HighlightButtonColor;
        colors.selectedColor = danger ? new Color(0.55f, 0.04f, 0.03f, 0.16f) : HighlightButtonColor;
        colors.pressedColor = PressedButtonColor;
        colors.disabledColor = new Color(0f, 0f, 0f, 0f);
        button.colors = colors;

        var mark = CreateUIObject("SelectionMark", buttonObject.transform);
        var markRect = mark.GetComponent<RectTransform>();
        markRect.anchorMin = new Vector2(0f, 0.5f);
        markRect.anchorMax = new Vector2(0f, 0.5f);
        markRect.pivot = new Vector2(0f, 0.5f);
        markRect.anchoredPosition = new Vector2(12f, 0f);
        markRect.sizeDelta = new Vector2(7f, Mathf.Max(44f, size.y - 20f));

        var markImage = mark.AddComponent<Image>();
        markImage.color = CinnabarColor;
        markImage.raycastTarget = false;
        mark.SetActive(false);

        RegisterSelectableButton(button, markImage);
        return button;
    }

    void BuildConfirmPanel(Transform parent)
    {
        confirmPanel = CreateUIObject("ConfirmPanel", parent);
        SetRect(confirmPanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(520f, 260f));

        var image = confirmPanel.AddComponent<Image>();
        image.color = PaperPanelColor;
        image.raycastTarget = true;

        var outline = confirmPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.11f, 0.10f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        confirmTitleText = BuildText(confirmPanel.transform, "ConfirmTitle", string.Empty, 30, TextAnchor.MiddleCenter, InkTextColor, new Vector2(0f, 76f), new Vector2(440f, 44f));
        confirmBodyText = BuildText(confirmPanel.transform, "ConfirmBody", string.Empty, 21, TextAnchor.MiddleCenter, MutedInkTextColor, new Vector2(0f, 16f), new Vector2(430f, 82f));

        confirmCancelButton = BuildTextButton(confirmPanel.transform, "ConfirmCancelButton", "取消", new Vector2(-108f, -82f), new Vector2(160f, 48f), CancelConfirm);
        confirmAcceptButton = BuildTextButton(confirmPanel.transform, "ConfirmAcceptButton", "确认", new Vector2(108f, -82f), new Vector2(160f, 48f), AcceptConfirm, true);
        confirmCancelText = confirmCancelButton.GetComponentInChildren<Text>();
        confirmAcceptText = confirmAcceptButton.GetComponentInChildren<Text>();

        confirmPanel.SetActive(false);
    }

    Button BuildTextButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, Action action, bool danger = false)
    {
        var buttonObject = CreateUIObject(objectName, parent);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, size);

        var image = buttonObject.AddComponent<Image>();
        image.color = danger ? new Color(0.53f, 0.08f, 0.06f, 0.82f) : new Color(0.32f, 0.30f, 0.25f, 0.72f);
        image.raycastTarget = true;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            PlayerInputBuffer.ClearAll();
            action?.Invoke();
        });

        var text = BuildText(buttonObject.transform, "Text", label, 22, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size);
        Stretch(text.rectTransform);
        text.raycastTarget = false;

        return button;
    }

    Text BuildText(Transform parent, string objectName, string value, int size, TextAnchor anchor, Color color, Vector2 position, Vector2 rectSize)
    {
        var textObject = CreateUIObject(objectName, parent);
        SetRect(textObject.GetComponent<RectTransform>(), position, rectSize);

        var text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = uiFont;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;

        return text;
    }

    void RegisterSelectableButton(Button button, Image mark)
    {
        selectableButtons.Add(button);
        selectionMarks.Add(mark);

        var trigger = button.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.Select, () => SetSelectedButton(button));
        AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            SetSelectedButton(button);
        });
    }

    void AddTrigger(EventTrigger trigger, EventTriggerType eventType, Action action)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => action?.Invoke());
        trigger.triggers.Add(entry);
    }

    void SetSelectedButton(Button button)
    {
        for (int i = 0; i < selectionMarks.Count; i++)
            if (selectionMarks[i] != null)
                selectionMarks[i].gameObject.SetActive(i < selectableButtons.Count && selectableButtons[i] == button);
    }

    void SelectButton(Button button)
    {
        if (button == null) return;

        SetSelectedButton(button);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    void ShowMessage(string title, string body)
    {
        ShowModal(title, body, null, true);
    }

    void ShowConfirm(string title, string body, Action onConfirm)
    {
        ShowModal(title, body, onConfirm, false);
    }

    void ShowModal(string title, string body, Action onConfirm, bool messageOnly)
    {
        EnsureUI();
        pendingConfirmAction = onConfirm;

        if (confirmTitleText != null) confirmTitleText.text = title;
        if (confirmBodyText != null) confirmBodyText.text = body;
        if (confirmCancelText != null) confirmCancelText.text = messageOnly ? string.Empty : "取消";
        if (confirmAcceptText != null) confirmAcceptText.text = messageOnly ? "知道了" : "确认";
        if (confirmCancelButton != null) confirmCancelButton.gameObject.SetActive(!messageOnly);
        if (confirmPanel != null) confirmPanel.SetActive(true);

        SetMenuButtonsInteractable(false);
        SelectButton(confirmAcceptButton);
    }

    void CancelConfirm()
    {
        HideConfirm();
        SelectButton(continueButton);
    }

    void AcceptConfirm()
    {
        var action = pendingConfirmAction;
        HideConfirm();
        action?.Invoke();
        if (action == null)
            SelectButton(continueButton);
    }

    void HideConfirm()
    {
        pendingConfirmAction = null;
        if (confirmCancelButton != null)
            confirmCancelButton.gameObject.SetActive(true);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        SetMenuButtonsInteractable(true);
    }

    void SetMenuButtonsInteractable(bool value)
    {
        if (continueButton != null) continueButton.interactable = value;
        if (settingsButton != null) settingsButton.interactable = value;
        if (mainMenuButton != null) mainMenuButton.interactable = value;
        if (quitButton != null) quitButton.interactable = value;
    }

    bool IsPauseMenuVisible()
    {
        return overlay != null && overlay.activeSelf;
    }

    bool IsConfirmVisible()
    {
        return confirmPanel != null && confirmPanel.activeSelf;
    }

    void ShowPanel()
    {
        EnsureUI();
        if (overlay != null) overlay.SetActive(true);
        HideConfirm();
        SelectButton(continueButton);
    }

    void HidePanel()
    {
        HideConfirm();
        if (overlay != null) overlay.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void HideAll()
    {
        HideConfirm();
        if (overlay != null) overlay.SetActive(false);
    }

    GameObject CreateUIObject(string objectName, Transform parent)
    {
        var created = new GameObject(objectName, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
