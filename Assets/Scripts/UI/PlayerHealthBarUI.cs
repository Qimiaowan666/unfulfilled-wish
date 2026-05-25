using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("HP")]
    public Image hpFill;
    public Image ghostFill;
    public TMP_Text hpText;

    [Header("Poise")]
    public Image poiseFill;
    public GameObject poiseBarRoot;

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
    Coroutine shakeRoutine;
    Coroutine flashRoutine;

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

        if (shakeTarget == null) shakeTarget = transform as RectTransform;
        if (shakeTarget != null) shakeOrigin = shakeTarget.anchoredPosition;
        if (hpFill != null) hpOriginalColor = hpFill.color;
    }

    void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnHPChanged -= HandleHPChanged;
            stats.OnGhostHPChanged -= HandleGhostHPChanged;
            stats.OnDamaged -= HandleDamaged;
        }
        if (poise != null)
            poise.OnPoiseChanged -= HandlePoiseChanged;
    }

    void Bind(PlayerStats target)
    {
        stats = target;
        stats.OnHPChanged += HandleHPChanged;
        stats.OnGhostHPChanged += HandleGhostHPChanged;
        stats.OnDamaged += HandleDamaged;
        Refresh();
    }

    void HandleHPChanged(float current, float max) => Refresh();
    void HandleGhostHPChanged(float current, float max) => Refresh();
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
