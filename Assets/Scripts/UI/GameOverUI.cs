using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 程序化死亡界面，常驻单例（跟 PauseUI 同类全局 UI）
// 监听 GameManager.OnGameOver → 显示遮罩 + 重试 / 返回主菜单
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    const string MainMenuSceneName = "MainMenu";

    static readonly Color OverlayColor   = new Color(0.06f, 0.01f, 0.01f, 0.86f);
    static readonly Color TitleColor     = new Color(0.82f, 0.12f, 0.10f, 1f);
    static readonly Color ButtonColor    = new Color(0.30f, 0.28f, 0.24f, 0.92f);
    static readonly Color ButtonTextColor = Color.white;

    GameObject overlay;
    Font uiFont;
    bool uiBuilt;
    GameManager subscribed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void Start()  => Subscribe();
    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnGameOver -= Show;
        if (Instance == this) Instance = null;
    }

    void Subscribe()
    {
        if (subscribed != null) subscribed.OnGameOver -= Show;
        subscribed = GameManager.Instance;
        if (subscribed != null) subscribed.OnGameOver += Show;
    }

    void Show()
    {
        EnsureUI();
        if (overlay != null) overlay.SetActive(true);
        Time.timeScale = 0f;
    }

    void Hide()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    public void Restart()
    {
        Hide();
        Time.timeScale = 1f;
        GameManager.Instance?.RestartScene();
    }

    public void ReturnToMainMenu()
    {
        Hide();
        Time.timeScale = 1f;
        GameManager.Instance?.LoadScene(MainMenuSceneName);
    }

    // ── 程序化构建 UI ──────────────────────────────────────────────
    void EnsureUI()
    {
        if (uiBuilt) return;
        uiBuilt = true;

        ConfigureHostCanvas();
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
              ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        overlay = CreateUIObject("GameOverOverlay", transform);
        Stretch(overlay.GetComponent<RectTransform>());
        var bg = overlay.AddComponent<Image>();
        bg.color = OverlayColor;
        bg.raycastTarget = true;

        var title = CreateText("Title", overlay.transform, "你 已 死 亡", 64, TextAnchor.MiddleCenter, TitleColor);
        SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(720f, 120f));
        title.fontStyle = FontStyle.Bold;

        BuildButton(overlay.transform, "RestartButton", "重新挑战", new Vector2(0f, -20f), Restart);
        BuildButton(overlay.transform, "MainMenuButton", "返回主菜单", new Vector2(0f, -96f), ReturnToMainMenu);

        overlay.SetActive(false);
    }

    void ConfigureHostCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1100;   // 高于暂停(1000)，死亡盖一切

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void BuildButton(Transform parent, string objName, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var go = CreateUIObject(objName, parent);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(320f, 60f));

        var img = go.AddComponent<Image>();
        img.color = ButtonColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var text = CreateText("Text", go.transform, label, 26, TextAnchor.MiddleCenter, ButtonTextColor);
        Stretch(text.rectTransform);
        text.raycastTarget = false;
    }

    Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor, Color color)
    {
        var go = CreateUIObject(name, parent);
        var text = go.AddComponent<Text>();
        text.text = value;
        text.font = uiFont;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
