using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarUI : MonoBehaviour
{
    public EnemyBase target;
    public Image hpFill;
    public Image poiseFill;
    public CanvasGroup canvasGroup;
    public float visibleDuration = 2.5f;

    PoiseMeter poise;
    float visibleTimer;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (target == null) target = GetComponentInParent<EnemyBase>();
        if (target != null) poise = target.GetComponent<PoiseMeter>();
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

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }

    void HandleHPChanged(float current, float max)
    {
        RefreshHP();
        Show();
    }

    void HandlePoiseChanged(float current, float max)
    {
        RefreshPoise();
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
