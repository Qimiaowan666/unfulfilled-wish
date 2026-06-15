using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 玩家 HUD（血/虚血/体力/韧性）。常驻 Bootstrap：每次 sceneLoaded 重新 Find 当前场景玩家并重新绑定，
// 没有玩家的场景（主菜单/Bootstrap）则隐藏可视部分。
public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("HP")]
    public Image hpFill;
    public Image ghostFill;

    [Header("Poise")]
    public Image poiseFill;
    public GameObject poiseBarRoot;

    [Header("Stamina")]
    public Image staminaFill;

    [Header("Damage Feedback")]
    public RectTransform shakeTarget;
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 6f;
    public Color flashColor = Color.white;
    public float flashDuration = 0.12f;

    PlayerStats stats;
    PoiseMeter poise;
    Vector2 shakeOrigin;
    Color hpOriginalColor;
    bool cached;
    LiquidBar hpLiquid, ghostLiquid, staminaLiquid;   // 液体条驱动（暗黑4式波浪），无则回退 fillAmount
    bool firstFill = true;             // 首次填充瞬间到位，之后才晃
    bool firstStamina = true;
    Coroutine shakeRoutine;
    Coroutine flashRoutine;

    void Awake() { SceneManager.sceneLoaded += OnSceneLoaded; }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unbind();
    }

    void Start() { CacheSelf(); Acquire(); }
    void OnSceneLoaded(Scene s, LoadSceneMode m) { CacheSelf(); Acquire(); }

    // 缓存自身（HUD 位置 / hp 原色）一次即可，与玩家无关
    void CacheSelf()
    {
        if (cached) return;
        if (shakeTarget == null) shakeTarget = transform as RectTransform;
        if (shakeTarget != null) shakeOrigin = shakeTarget.anchoredPosition;
        if (hpFill != null) hpOriginalColor = hpFill.color;
        if (hpFill != null) hpLiquid = hpFill.GetComponent<LiquidBar>();
        if (ghostFill != null) ghostLiquid = ghostFill.GetComponent<LiquidBar>();
        if (staminaFill != null) staminaLiquid = staminaFill.GetComponent<LiquidBar>();
        cached = true;
    }

    // 重新获取当前场景玩家并绑定；无玩家则隐藏
    void Acquire()
    {
        var player = FindAnyObjectByType<PlayerStats>();
        if (player == stats) { SetVisible(stats != null); return; }   // 没变化
        Unbind();
        if (player == null) { SetVisible(false); return; }
        Bind(player);
        poise = player.GetComponent<PoiseMeter>();
        if (poise != null)
        {
            poise.OnPoiseChanged += HandlePoiseChanged;
            RefreshPoise();
        }
        else if (poiseBarRoot != null)
        {
            poiseBarRoot.SetActive(false);
        }
        SetVisible(true);
    }

    void Unbind()
    {
        if (stats != null)
        {
            stats.OnHPChanged -= HandleHPChanged;
            stats.OnGhostHPChanged -= HandleGhostHPChanged;
            stats.OnStaminaChanged -= HandleStaminaChanged;
            stats.OnDamaged -= HandleDamaged;
        }
        if (poise != null) { poise.OnPoiseChanged -= HandlePoiseChanged; poise = null; }
        stats = null;
        firstFill = true;   // 下次绑定时初始填充瞬间到位，不晃
        firstStamina = true;
    }

    // 隐藏/显示可视部分（保留组件存活以继续监听 sceneLoaded）
    void SetVisible(bool v)
    {
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(v);
    }

    // 外部临时隐藏（如 boss 二阶段过渡演出）；结束再 false 恢复
    public void SetHudHidden(bool hidden) => SetVisible(!hidden);

    void Bind(PlayerStats target)
    {
        stats = target;
        stats.OnHPChanged += HandleHPChanged;
        stats.OnGhostHPChanged += HandleGhostHPChanged;
        stats.OnStaminaChanged += HandleStaminaChanged;
        stats.OnDamaged += HandleDamaged;
        Refresh();
        RefreshStamina();
    }

    void HandleHPChanged(float current, float max) => Refresh();
    void HandleGhostHPChanged(float current, float max) => Refresh();
    void HandleStaminaChanged(float current, float max) => RefreshStamina();
    void HandlePoiseChanged(float current, float max) => RefreshPoise();

    void HandleDamaged(float damage)
    {
        if (shakeTarget != null)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeRoutine());
        }
        if (hpFill != null)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }
    }

    IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float decay = 1f - (elapsed / shakeDuration);
            Vector2 offset = Random.insideUnitCircle * shakeMagnitude * decay;
            shakeTarget.anchoredPosition = shakeOrigin + offset;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        shakeTarget.anchoredPosition = shakeOrigin;
        shakeRoutine = null;
    }

    IEnumerator FlashRoutine()
    {
        hpFill.color = flashColor;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            hpFill.color = Color.Lerp(flashColor, hpOriginalColor, elapsed / flashDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        hpFill.color = hpOriginalColor;
        flashRoutine = null;
    }

    void Refresh()
    {
        if (stats == null) return;
        float maxHP = Mathf.Max(stats.maxHP, 1f);
        float ghostF = Mathf.Clamp01((stats.CurrentHP + stats.CurrentGhostHP) / maxHP);
        float hpF    = Mathf.Clamp01(stats.CurrentHP / maxHP);
        bool inst = firstFill; firstFill = false;
        // 有 LiquidBar 走液体波浪，否则回退原 fillAmount
        if (ghostLiquid != null) ghostLiquid.SetFill(ghostF, inst); else if (ghostFill != null) ghostFill.fillAmount = ghostF;
        if (hpLiquid != null)    hpLiquid.SetFill(hpF, inst);       else if (hpFill != null)    hpFill.fillAmount    = hpF;
    }

    void RefreshPoise()
    {
        if (poise == null || poiseFill == null) return;
        poiseFill.fillAmount = poise.maxPoise > 0f ? Mathf.Clamp01(poise.CurrentPoise / poise.maxPoise) : 0f;
    }

    void RefreshStamina()
    {
        if (stats == null) return;
        float f = stats.maxStamina > 0f ? Mathf.Clamp01(stats.CurrentStamina / stats.maxStamina) : 0f;
        if (staminaLiquid != null) staminaLiquid.SetFill(f, firstStamina);
        else if (staminaFill != null) staminaFill.fillAmount = f;
        firstStamina = false;
    }
}
