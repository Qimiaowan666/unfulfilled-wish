using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBarUI : MonoBehaviour
{
    public Image hpFill;
    public Image ghostFill;
    public Text hpText;

    PlayerStats stats;

    void Start()
    {
        Bind(FindAnyObjectByType<PlayerStats>());
    }

    void OnDestroy()
    {
        if (stats == null) return;
        stats.OnHPChanged -= HandleHPChanged;
        stats.OnGhostHPChanged -= HandleGhostHPChanged;
    }

    void Bind(PlayerStats target)
    {
        if (target == null) return;

        stats = target;
        stats.OnHPChanged += HandleHPChanged;
        stats.OnGhostHPChanged += HandleGhostHPChanged;
        Refresh();
    }

    void HandleHPChanged(float current, float max) => Refresh();
    void HandleGhostHPChanged(float current, float max) => Refresh();

    void Refresh()
    {
        if (stats == null) return;

        float maxHP = Mathf.Max(stats.maxHP, 1f);
        float hpAmount = Mathf.Clamp01(stats.CurrentHP / maxHP);
        float ghostAmount = Mathf.Clamp01((stats.CurrentHP + stats.CurrentGhostHP) / maxHP);

        if (ghostFill != null) ghostFill.fillAmount = ghostAmount;
        if (hpFill != null) hpFill.fillAmount = hpAmount;
        if (hpText != null) hpText.text = $"HP {stats.CurrentHP:F0}/{stats.maxHP:F0}";
    }
}
