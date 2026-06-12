using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 九日式 boss 击破演出（常驻 Bootstrap）：顿帧/慢放 + 全屏压暗 + 角色变白色剪影 + 水墨泼溅白爆 + boss 消散。
// 白闪布局在 BossFinishUI.prefab；剪影材质(silhouetteMaterial)、压暗块、泼溅、碎屑由代码生成。
public class BossFinishUI : MonoBehaviour
{
    public static BossFinishUI Instance { get; private set; }
    public static bool IsPlaying { get; private set; }

    [Header("引用")]
    public GameObject panelRoot;     // 演出 UI 根（白闪）
    public Image      whiteFlash;    // 全屏白闪
    public Material   silhouetteMaterial;  // WhiteSilhouette.mat
    public Sprite     splatterSprite;      // InkSplatter.png（水墨泼溅白爆）

    [Header("参数")]
    public float  hitstopRealtime = 0.22f;
    public float  slowMoScale     = 0.25f;
    public Color  darkColor       = new Color(0.02f, 0.02f, 0.05f, 1f);
    [Tooltip("水墨泼溅大小（越小越像伤口、越大越像爆炸）")]
    public float  splatterScale   = 1.2f;

    [Header("击破名牌")]
    public GameObject  defeatBanner;   // 名牌根（金框 + 烘焙文字 DefeatTextImg，默认隐藏）
    public CanvasGroup defeatGroup;    // 淡入用（“xx 被打败了。”已烘焙进 DefeatText.png）

    struct SRState { public SpriteRenderer sr; public Material mat; public Color color; public int layer; public int order; }
    readonly List<SRState> swapped = new List<SRState>();
    Sprite squareSprite;
    GameObject hudHidden;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        IsPlaying = false;
    }

    void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnEnable()  { SceneManager.sceneLoaded += OnSceneChanged; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneChanged; }

    // 演出中途切场景 → 强制收尾，避免 timeScale / 材质 / IsPlaying 卡死
    void OnSceneChanged(Scene s, LoadSceneMode m)
    {
        if (!IsPlaying) return;
        StopAllCoroutines();
        Time.timeScale = 1f;
        RestoreSwapped();
        RestorePlayerHud();
        if (panelRoot != null) panelRoot.SetActive(false);
        IsPlaying = false;
    }

    public void Play(EnemyBase boss, Action onComplete)
    {
        if (IsPlaying) { onComplete?.Invoke(); return; }
        StartCoroutine(Sequence(boss, onComplete));
    }

    IEnumerator Sequence(EnemyBase boss, Action onComplete)
    {
        IsPlaying = true;
        EnsureAssets();
        HidePlayerHud();
        if (panelRoot != null) panelRoot.SetActive(true);
        if (defeatBanner != null) defeatBanner.SetActive(false);
        if (whiteFlash != null) whiteFlash.color = new Color(1f, 1f, 1f, 0f);

        Vector3 bossPos = boss != null ? BossCenter(boss) : Vector3.zero;

        // 1) 顿帧 + 白闪 + BGM 戛然而止（更有节奏）
        AudioManager.Instance?.StopBGM();
        Time.timeScale = 0.0001f;
        if (whiteFlash != null) StartCoroutine(FlashOnce(0.18f, 0.9f));
        yield return new WaitForSecondsRealtime(hitstopRealtime);

        // 2) 慢放
        Time.timeScale = slowMoScale;

        // 3) 压暗(盖住整个世界)。此时 boss 仍是原样，被压暗块逐渐盖黑
        int topLayer = TopSortingLayer();
        var dark = MakeDarkQuad(topLayer, 30000);
        var darkSR = dark != null ? dark.GetComponent<SpriteRenderer>() : null;
        yield return Fade(0.8f, t =>
        {
            if (darkSR != null) { var c = darkColor; c.a = t; darkSR.color = c; }   // 黑幕合上 → 背景全黑
        });

        // 4) 黑幕合上后：boss 剪影 + 水墨泼溅 + 碎屑 —— 同一刻一起出现
        var bossSRs = boss != null ? SwapToSilhouette(boss.gameObject, Color.white, topLayer, 30010) : new List<SpriteRenderer>();
        var splatSR = SpawnSplatter(bossPos, topLayer, 30005);   // 泼溅排在剪影之下；alpha 不自淡，交给下面共享 fade
        SpawnShards(bossPos, topLayer, 30025, 16);
        StartCoroutine(ShowDefeatBannerDelayed(1.2f));   // 名牌晚一拍再浮现
        // 只显示 boss 剪影；主角不染白(被压暗块盖住，不出现在画面)

        // 5) 定格亮相一拍
        yield return new WaitForSecondsRealtime(0.5f);

        // 6) boss 剪影 + 泼溅 用同一条 fade 一起消散 → 同时归零、同时消失
        yield return Fade(2.4f, t =>
        {
            float a = 1f - t;
            foreach (var sr in bossSRs) if (sr != null) { var c = sr.color; c.a = a; sr.color = c; }
            if (splatSR != null) { var c = splatSR.color; c.a = a; splatSR.color = c; }
        });

        // 7) 停一拍
        yield return new WaitForSecondsRealtime(1.4f);

        // 8) 收尾 → 交给 VictoryUI
        Time.timeScale = 1f;
        RestoreSwapped();
        if (splatSR != null) Destroy(splatSR.gameObject);
        if (dark != null) Destroy(dark);
        if (panelRoot != null) panelRoot.SetActive(false);
        IsPlaying = false;
        RestorePlayerHud();
        AudioManager.Instance?.PlayAreaBGM();   // 演出结束 → 恢复普通场景 BGM（boss 曲已在击破瞬间停掉）
        onComplete?.Invoke();
    }

    // ── helpers ──────────────────────────────────────────────
    void EnsureAssets()
    {
        if (squareSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            squareSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    Vector3 BossCenter(EnemyBase boss)
    {
        var sr = boss.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.bounds.center : boss.transform.position;
    }

    int TopSortingLayer()
    {
        var layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].id : 0;
    }

    GameObject MakeDarkQuad(int layerID, int order)
    {
        var go = new GameObject("BossFinish_Dark");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = new Color(darkColor.r, darkColor.g, darkColor.b, 0f);
        sr.sortingLayerID = layerID;
        sr.sortingOrder = order;

        var cam = Camera.main;
        if (cam != null)
        {
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            float h = (cam.orthographic ? cam.orthographicSize : 8f) * 2f;
            float w = h * cam.aspect;
            float sw = Mathf.Max(0.001f, squareSprite.bounds.size.x);
            go.transform.localScale = new Vector3(w / sw * 1.3f, h / sw * 1.3f, 1f);
        }
        return go;
    }

    List<SpriteRenderer> SwapToSilhouette(GameObject go, Color tint, int layerID, int order)
    {
        var list = new List<SpriteRenderer>();
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;   // 跳过空/禁用渲染器
            string n = sr.gameObject.name.ToLower();
            if (n.Contains("indicat") || n.Contains("vfx") || n.Contains("warn") ||
                n.Contains("afterimage") || n.Contains("slash") || n.Contains("trail")) continue;   // 跳过指示器/特效
            swapped.Add(new SRState { sr = sr, mat = sr.sharedMaterial, color = sr.color, layer = sr.sortingLayerID, order = sr.sortingOrder });
            if (silhouetteMaterial != null) sr.sharedMaterial = silhouetteMaterial;
            sr.color = tint;
            sr.sortingLayerID = layerID;
            sr.sortingOrder = order;
            list.Add(sr);
        }
        return list;
    }

    void RestoreSwapped()
    {
        foreach (var s in swapped)
            if (s.sr != null) { s.sr.sharedMaterial = s.mat; s.sr.color = s.color; s.sr.sortingLayerID = s.layer; s.sr.sortingOrder = s.order; }
        swapped.Clear();
    }

    // 击破演出期间藏玩家 HUD（血条/体力等所在的 Canvas），演出结束再开
    void HidePlayerHud()
    {
        var phb = FindAnyObjectByType<PlayerHealthBarUI>();
        var cv = phb != null ? phb.GetComponentInParent<Canvas>() : null;
        if (cv != null) { hudHidden = cv.gameObject; hudHidden.SetActive(false); }
    }

    void RestorePlayerHud()
    {
        if (hudHidden != null) hudHidden.SetActive(true);
        hudHidden = null;
    }

    // 名牌晚一拍再出（剪影/泼溅炸开后约 delay 秒），轻微淡入。
    // 文字由烘焙好的 DefeatTextImg(Image) 渲染 —— 绕开 SimHei 字体材质把文字乘成黑色的坑。
    IEnumerator ShowDefeatBannerDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (defeatBanner == null) yield break;
        defeatBanner.SetActive(true);
        if (defeatGroup == null) yield break;
        defeatGroup.alpha = 0f;
        float t = 0f, dur = 0.35f;
        while (t < dur)   // 轻微淡入
        {
            t += Time.unscaledDeltaTime;
            defeatGroup.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        defeatGroup.alpha = 1f;
    }

    // 水墨泼溅：快速爆开到位 + 缓慢扩散；alpha 不在这里淡出，
    // 由主时间线与 boss 剪影共享同一条 fade 一起消散（保证同时消失）。返回 SR 供主 fade 控 alpha。
    SpriteRenderer SpawnSplatter(Vector3 center, int layerID, int order)
    {
        if (splatterSprite == null) return null;
        var go = new GameObject("BossFinish_Splatter");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = splatterSprite; sr.color = Color.white;
        sr.sortingLayerID = layerID; sr.sortingOrder = order;
        StartCoroutine(SplatterGrow(go));
        return sr;
    }

    // 只动 scale（爆开 + 缓扩），不碰 alpha
    IEnumerator SplatterGrow(GameObject go)
    {
        float t = 0f, grow = 0.7f;
        while (t < grow && go != null)   // 爆开（ease-out，放慢更有慢镜感）
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / grow);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, splatterScale, e);
            yield return null;
        }
        float ft = 0f, spread = 2.4f;
        while (ft < spread && go != null)   // 缓慢扩散（与剪影淡出同步进行）
        {
            ft += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(ft / spread);
            go.transform.localScale = Vector3.one * Mathf.Lerp(splatterScale, splatterScale * 1.12f, k);
            yield return null;
        }
    }

    void SpawnShards(Vector3 center, int layerID, int order, int count)
    {
        float sw = Mathf.Max(0.001f, squareSprite.bounds.size.x);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("BossFinish_Shard");
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = squareSprite; sr.color = Color.white;
            sr.sortingLayerID = layerID; sr.sortingOrder = order;

            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            go.transform.right = dir;   // 沿飞行方向拉成条
            float lx = UnityEngine.Random.Range(0.18f, 0.55f);
            float ly = UnityEngine.Random.Range(0.04f, 0.1f);
            go.transform.localScale = new Vector3(lx / sw, ly / sw, 1f);

            float spd = UnityEngine.Random.Range(4f, 11f);
            StartCoroutine(ShardMove(go, sr, dir * spd, UnityEngine.Random.Range(0.4f, 0.85f)));
        }
    }

    IEnumerator ShardMove(GameObject go, SpriteRenderer sr, Vector2 vel, float life)
    {
        float t = 0f;
        while (t < life && go != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            go.transform.position += (Vector3)(vel * dt);
            vel *= 0.92f;
            if (sr != null) { var c = sr.color; c.a = Mathf.Clamp01(1f - t / life); sr.color = c; }
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator FlashOnce(float dur, float peak)
    {
        float half = dur * 0.5f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = t < half ? Mathf.Lerp(0f, peak, t / half) : Mathf.Lerp(peak, 0f, (t - half) / half);
            if (whiteFlash != null) whiteFlash.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        if (whiteFlash != null) whiteFlash.color = new Color(1f, 1f, 1f, 0f);
    }

    IEnumerator Fade(float dur, Action<float> step)
    {
        float t = 0f;
        while (t < dur) { t += Time.unscaledDeltaTime; step(Mathf.Clamp01(t / dur)); yield return null; }
        step(1f);
    }
}
