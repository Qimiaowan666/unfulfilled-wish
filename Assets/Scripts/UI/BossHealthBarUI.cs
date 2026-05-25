using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    public EnemyBase boss;
    public Image hpFill;
    public Image poiseFill;
    public GameObject root;

    PoiseMeter poise;

    void Start()
    {
        if (root == null) root = gameObject;
        if (boss == null)
            boss = FindAnyObjectByType<MinotaurBoss>();

        if (boss == null)
        {
            root.SetActive(false);
            return;
        }

        poise = boss.GetComponent<PoiseMeter>();
        boss.OnHPChanged += HandleHPChanged;
        boss.OnDied += Hide;
        if (poise != null) poise.OnPoiseChanged += HandlePoiseChanged;

        root.SetActive(true);
        RefreshHP();
        RefreshPoise();
    }

    void OnDestroy()
    {
        if (boss != null)
        {
            boss.OnHPChanged -= HandleHPChanged;
            boss.OnDied -= Hide;
        }

        if (poise != null)
            poise.OnPoiseChanged -= HandlePoiseChanged;
    }

    void HandleHPChanged(float current, float max) => RefreshHP();
    void HandlePoiseChanged(float current, float max) => RefreshPoise();

    void RefreshHP()
    {
        if (boss == null || hpFill == null) return;
        hpFill.fillAmount = boss.maxHP > 0f ? Mathf.Clamp01(boss.CurrentHP / boss.maxHP) : 0f;
    }

    void RefreshPoise()
    {
        if (poise == null || poiseFill == null) return;
        poiseFill.fillAmount = poise.maxPoise > 0f ? Mathf.Clamp01(poise.CurrentPoise / poise.maxPoise) : 0f;
    }

    void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
