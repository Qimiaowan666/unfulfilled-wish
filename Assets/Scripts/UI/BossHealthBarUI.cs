using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    public EnemyBase boss;
    public Image hpFill;
    public Image poiseFill;
    public GameObject root;

    [Tooltip("登场演出：开局先隐藏，等 boss 吼叫时再 Reveal 缓缓露出并充满")]
    public bool startHidden = false;

    PoiseMeter poise;
    Coroutine revealRoutine;
    bool revealed;

    // 自愈：登场模式下，只要 boss 进入战斗(combatEnabled) 就保证血条露出，
    // 不依赖别人显式调 Reveal（读档/各种时序下都不会"没血条"）。
    void Update()
    {
        if (!startHidden || revealed || boss == null) return;
        var mb = boss as MinotaurBoss;
        // 只在 boss「活着 + 在场 + 进入战斗」时自愈露出；死了就别再拉回来（击破时血条要消失）
        if (mb != null && mb.combatEnabled && mb.CurrentHP > 0f && mb.gameObject.activeInHierarchy)
            Reveal(0.6f);
    }

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

        if (startHidden) HideForIntro();   // 登场前先藏（用 alpha=0，保持 GameObject 激活，协程可用）
    }

    CanvasGroup EnsureCanvasGroup()
    {
        if (root == null) root = gameObject;
        var cg = root.GetComponent<CanvasGroup>();
        if (cg == null) cg = root.AddComponent<CanvasGroup>();
        return cg;
    }

    public void HideForIntro()
    {
        revealed = false;
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        EnsureCanvasGroup().alpha = 0f;
        if (hpFill != null) hpFill.fillAmount = 0f;
    }

    // boss 吼叫时调用：血条淡入 + 血量从 0 缓缓充满
    public void Reveal(float duration)
    {
        if (root == null) root = gameObject;
        revealed = true;
        root.SetActive(true);
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(RevealRoutine(duration));
    }

    IEnumerator RevealRoutine(float duration)
    {
        var cg = EnsureCanvasGroup();
        float target = boss != null && boss.maxHP > 0f ? Mathf.Clamp01(boss.CurrentHP / boss.maxHP) : 1f;
        cg.alpha = 0f;
        if (hpFill != null) hpFill.fillAmount = 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            cg.alpha = Mathf.Clamp01(k * 1.8f);          // 前期淡入
            if (hpFill != null) hpFill.fillAmount = target * k;   // 缓缓充满
            yield return null;
        }
        cg.alpha = 1f;
        if (hpFill != null) hpFill.fillAmount = target;
        revealRoutine = null;
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

    // boss 死亡时隐藏：用 alpha 而非停用 GameObject，保证 Update 自愈/查找还能工作（读档后能恢复）
    void Hide() => HideForIntro();
}
