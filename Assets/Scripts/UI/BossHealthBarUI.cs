using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Boss 血条（通用，不绑定具体 boss 类型）。场景级：Start 时 Find 当前场景标记了 isBoss 的敌人并绑定，
// 读档 apply 时(SaveSystem.AfterApply)重新绑定/收起；名字/配色读 boss.profile(BossProfile)。
// 登场模式下开局先藏，吼叫 Reveal 缓缓露出充满。
public class BossHealthBarUI : MonoBehaviour
{
    public EnemyBase boss;
    public Image hpFill;
    public Image poiseFill;
    public TMP_Text nameText;   // boss 名（从 profile.displayName 取）
    public GameObject root;

    [Tooltip("登场演出：开局先隐藏，等 boss 吼叫时再 Reveal 缓缓露出并充满")]
    public bool startHidden = false;

    PoiseMeter poise;
    Coroutine revealRoutine;
    bool revealed;

    void Awake()
    {
        if (root == null) root = gameObject;
        SaveSystem.AfterApply += OnSaveApplied;
    }

    void OnDestroy()
    {
        SaveSystem.AfterApply -= OnSaveApplied;
        Unbind();
    }

    void Start() { Acquire(); }
    void OnSaveApplied() { Acquire(); }

    // 自愈：登场模式下，只要 boss 进入战斗(combatEnabled) 就保证血条露出，
    // 不依赖别人显式调 Reveal（读档/各种时序下都不会"没血条"）。
    void Update()
    {
        if (!startHidden || revealed || boss == null) return;
        // 只在 boss「活着 + 在场 + 进入战斗」时自愈露出；死了就别再拉回来（击破时血条要消失）
        if (boss.combatEnabled && boss.CurrentHP > 0f && boss.gameObject.activeInHierarchy)
            Reveal(0.6f);
    }

    // 找当前场景里标记了 isBoss 的活动敌人
    static EnemyBase FindActiveBoss()
    {
        foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.isBoss && e.gameObject.activeInHierarchy) return e;
        return null;
    }

    // 重新获取当前场景 boss 并绑定；无 boss 则隐藏
    void Acquire()
    {
        if (root == null) root = gameObject;
        var found = FindActiveBoss();
        if (found == boss && boss != null) return;   // 没变化
        Unbind();
        boss = found;
        if (boss == null) { root.SetActive(false); return; }

        poise = boss.GetComponent<PoiseMeter>();
        boss.OnHPChanged += HandleHPChanged;
        boss.OnDied += Hide;
        if (poise != null) poise.OnPoiseChanged += HandlePoiseChanged;

        ApplyProfile();   // 名字 + 可选配色

        root.SetActive(true);
        RefreshHP();
        RefreshPoise();

        if (startHidden) HideForIntro();   // 登场前先藏（用 alpha=0，保持 GameObject 激活，协程可用）
    }

    void ApplyProfile()
    {
        var p = boss != null ? boss.profile : null;
        if (p == null) return;
        if (nameText != null) nameText.text = p.displayName;
        if (p.overrideBarColor && hpFill != null) hpFill.color = p.barColor;
    }

    void Unbind()
    {
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        if (boss != null)
        {
            boss.OnHPChanged -= HandleHPChanged;
            boss.OnDied -= Hide;
        }
        if (poise != null) { poise.OnPoiseChanged -= HandlePoiseChanged; poise = null; }
        boss = null;
        revealed = false;
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

    // 外部临时隐藏/恢复（如 boss 二阶段过渡演出）；只动 alpha，不重置血量/露出状态
    public void SetHidden(bool hidden) => EnsureCanvasGroup().alpha = hidden ? 0f : 1f;

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
