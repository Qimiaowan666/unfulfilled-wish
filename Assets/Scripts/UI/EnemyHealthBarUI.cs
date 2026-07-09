using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarUI : MonoBehaviour
{
    public EnemyBase target;
    public Image hpFill;
    public Image poiseFill;
    public CanvasGroup canvasGroup;
    public float visibleDuration = 2.5f;

    [Header("自动定位 (统一所有敌人血条高度/大小，免去逐只 prefab 调)")]
    public float headOffset = 0.4f;    // 血条在碰撞体顶部之上的世界高度
    public float worldWidth = 1.6f;    // 血条目标世界宽度

    PoiseMeter poise;
    float visibleTimer;
    Collider2D targetCol;
    RectTransform rt;
    Camera cam;
    float baseWidthPx = 150f;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (target == null) target = GetComponentInParent<EnemyBase>();
        if (target != null)
        {
            poise = target.GetComponent<PoiseMeter>();
            targetCol = target.GetComponent<Collider2D>();
        }
        rt = transform as RectTransform;
        if (rt != null && rt.sizeDelta.x > 0f) baseWidthPx = rt.sizeDelta.x;
    }

    void OnEnable()
    {
        if (target != null)
        {
            target.OnHPChanged += HandleHPChanged;
            target.OnDied += Hide;
        }

        if (poise != null)
            poise.OnPoiseChanged += HandlePoiseChanged;

        RefreshHP();
        RefreshPoise();
        Hide();
    }

    void OnDisable()
    {
        if (target != null)
        {
            target.OnHPChanged -= HandleHPChanged;
            target.OnDied -= Hide;
        }

        if (poise != null)
            poise.OnPoiseChanged -= HandlePoiseChanged;
    }

    void LateUpdate()
    {
        if (visibleTimer > 0f)
        {
            visibleTimer -= Time.deltaTime;
            if (visibleTimer <= 0f) Hide();
        }

        // 统一定位/定大小：所有敌人血条悬在碰撞体顶部上方 headOffset、目标世界宽度 worldWidth
        // (脚本驱动，免去逐只 prefab 调 localPosition/scale)
        if (target != null)
        {
            float topY = targetCol != null ? targetCol.bounds.max.y : target.transform.position.y + 1f;
            transform.position = new Vector3(target.transform.position.x, topY + headOffset, transform.position.z);

            Vector3 pl = target.transform.lossyScale;
            float targetLossy = baseWidthPx > 0f ? worldWidth / baseWidthPx : 0.01f;
            transform.localScale = new Vector3(
                Mathf.Approximately(pl.x, 0f) ? targetLossy : targetLossy / pl.x,
                Mathf.Approximately(pl.y, 0f) ? targetLossy : targetLossy / pl.y,
                1f);
        }

        if (cam == null) cam = Camera.main;
        if (cam != null)
            transform.rotation = cam.transform.rotation;
    }

    // 任一变化(HP 或韧性)都把两条都刷新到当前值，避免血条出现时显示 stale(如满血只扣韧 → 血条空成黑底)
    void HandleHPChanged(float current, float max)
    {
        RefreshHP();
        RefreshPoise();
        Show();
    }

    void HandlePoiseChanged(float current, float max)
    {
        RefreshPoise();
        RefreshHP();
        Show();
    }

    void RefreshHP()
    {
        if (target == null || hpFill == null) return;
        hpFill.fillAmount = target.maxHP > 0f ? Mathf.Clamp01(target.CurrentHP / target.maxHP) : 0f;
    }

    void RefreshPoise()
    {
        if (poise == null || poiseFill == null) return;
        poiseFill.fillAmount = poise.maxPoise > 0f ? Mathf.Clamp01(poise.CurrentPoise / poise.maxPoise) : 0f;
    }

    void Show()
    {
        visibleTimer = visibleDuration;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        else gameObject.SetActive(true);
    }

    void Hide()
    {
        visibleTimer = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }
}
