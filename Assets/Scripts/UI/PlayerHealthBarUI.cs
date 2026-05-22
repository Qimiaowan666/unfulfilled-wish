using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("HP")]
    public Image hpFill;
    public Image ghostFill;
    public Text hpText;

    [Header("Poise")]
    public Image poiseFill;
    public GameObject poiseBarRoot;

    PlayerStats stats;
    PoiseMeter poise;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerStats>();
        if (player == null) return;
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
    }

    void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnHPChanged -= HandleHPChanged;
            stats.OnGhostHPChanged -= HandleGhostHPChanged;
        }
        if (poise != null)
            poise.OnPoiseChanged -= HandlePoiseChanged;
    }

    void Bind(PlayerStats target)
    {
        stats = target;
        stats.OnHPChanged += HandleHPChanged;
        stats.OnGhostHPChanged += HandleGhostHPChanged;
        Refresh();
    }

    void HandleHPChanged(float current, float max) => Refresh();
    void HandleGhostHPChanged(float current, float max) => Refresh();
    void HandlePoiseChanged(float current, float max) => RefreshPoise();

    void Refresh()
    {
        if (stats == null) return;
        float maxHP = Mathf.Max(stats.maxHP, 1f);
        if (ghostFill != null) ghostFill.fillAmount = Mathf.Clamp01((stats.CurrentHP + stats.CurrentGhostHP) / maxHP);
        if (hpFill != null)    hpFill.fillAmount    = Mathf.Clamp01(stats.CurrentHP / maxHP);
        if (hpText != null)    hpText.text          = $"HP {stats.CurrentHP:F0}/{stats.maxHP:F0}";
    }

    void RefreshPoise()
    {
        if (poise == null || poiseFill == null) return;
        poiseFill.fillAmount = poise.maxPoise > 0f ? Mathf.Clamp01(poise.CurrentPoise / poise.maxPoise) : 0f;
    }
}
