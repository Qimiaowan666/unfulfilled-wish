using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("HP Bar")]
    public Image hpFill;
    public Image ghostHPFill;

    [Header("Poise Bar (Enemy)")]
    public Image poiseFill;
    public GameObject poiseBarRoot;

    PlayerStats stats;

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;

        stats.OnHPChanged      += UpdateHP;
        stats.OnGhostHPChanged += UpdateGhostHP;

        UpdateHP(stats.CurrentHP, stats.maxHP);
        UpdateGhostHP(stats.CurrentGhostHP, stats.maxGhostHP);
    }

    void UpdateHP(float current, float max)
    {
        if (hpFill != null) hpFill.fillAmount = current / max;
    }

    void UpdateGhostHP(float current, float max)
    {
        if (ghostHPFill != null) ghostHPFill.fillAmount = max > 0 ? current / max : 0f;
    }

    public void ShowEnemyPoise(PoiseMeter poise)
    {
        if (poiseBarRoot != null) poiseBarRoot.SetActive(true);
        if (poise != null)
        {
            poise.OnPoiseChanged += (cur, max) =>
            {
                if (poiseFill != null) poiseFill.fillAmount = cur / max;
            };
        }
    }

    public void HideEnemyPoise()
    {
        if (poiseBarRoot != null) poiseBarRoot.SetActive(false);
    }
}
