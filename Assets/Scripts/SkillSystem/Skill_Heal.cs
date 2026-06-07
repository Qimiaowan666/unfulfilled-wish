using UnityEngine;

// 治疗：持续施法 castDuration 秒，期间不能动，受击会被打断
// 完成后回 healAmount HP
[ExecuteAlways]
public class Skill_Heal : Skill_Base
{
    [Header("Heal Settings")]
    [SerializeField] float healAmount   = 30f;     // 回血量
    [SerializeField] float castDuration = 1.5f;    // 持续施法时间（秒）

    [Header("Animation")]
    [SerializeField] string animStateName = "rest";  // 施法期间播的 animator state（留空 = 不播）

    [Header("VFX - Sprite 帧动画（武侠绿气 sheet）")]
    [SerializeField] bool          vfxEnabled       = true;
    [SerializeField] Sprite[]      vfxFrames;                                              // 8 帧按顺序拖入
    [SerializeField] float         vfxFps           = 10f;                                 // 帧率
    [SerializeField] Color         vfxTint          = Color.white;                         // 整体调色（白 = 原色）
    [SerializeField] Vector2       vfxLocalOffset   = new Vector2(0f, 0.45f);              // 相对玩家中心的偏移（脚下生效用 y≈玩家半高）
    [SerializeField] Vector2       vfxLocalScale    = new Vector2(1f, 1.4f);               // 拉伸：横/纵
    [SerializeField] string        vfxSortingLayer  = "Default";
    [SerializeField] int           vfxSortingOrder  = 1;                                   // 玩家之上一层（+1）

    [Header("Editor Preview")]
    [SerializeField] bool          showPreviewInScene = true;   // Scene 视图里实时显示 frames[0]
    SpriteRenderer _previewSr;

    public float    HealAmount      => healAmount;
    public float    CastDuration    => castDuration;
    public string   AnimStateName   => animStateName;
    public bool     VfxEnabled      => vfxEnabled;
    public Sprite[] VfxFrames       => vfxFrames;
    public float    VfxFps          => vfxFps;
    public Color    VfxTint         => vfxTint;
    public Vector2  VfxLocalOffset  => vfxLocalOffset;
    public Vector2  VfxLocalScale   => vfxLocalScale;
    public string   VfxSortingLayer => vfxSortingLayer;
    public int      VfxSortingOrder => vfxSortingOrder;

    public override void TryUseSkill()
    {
        if (!CanUseSkill()) return;
        if (player == null) return;

        player.healState.Configure(this);
        player.stateMachine.ChangeState(player.healState);
        SetSkillOnCooldown();
        Debug.Log($"[Skill] Heal start - cast={castDuration}s, amount={healAmount}");
    }

#if UNITY_EDITOR
    // Scene 视图实时预览（不需要 Play）：在玩家身上挂一个临时 SpriteRenderer 显示 frames[0]
    void Update()
    {
        if (Application.isPlaying) { CleanupPreview(); return; }
        if (!showPreviewInScene)   { CleanupPreview(); return; }
        EnsurePreview();
    }

    void OnDisable() { CleanupPreview(); }
    void OnDestroy() { CleanupPreview(); }

    void EnsurePreview()
    {
        if (vfxFrames == null || vfxFrames.Length == 0 || vfxFrames[0] == null) { CleanupPreview(); return; }
        var pc = GetComponentInParent<PlayerController>();
        if (pc == null) { CleanupPreview(); return; }

        if (_previewSr == null)
        {
            var go = new GameObject("[Skill_Heal Preview]");
            go.hideFlags = HideFlags.HideAndDontSave;   // 不存盘、不污染 Hierarchy
            go.transform.SetParent(pc.transform, false);
            _previewSr = go.AddComponent<SpriteRenderer>();
        }

        _previewSr.sprite = vfxFrames[0];
        Color c = vfxTint; c.a *= 0.6f;                   // 略透明，方便看到玩家
        _previewSr.color = c;
        _previewSr.sortingLayerName = string.IsNullOrEmpty(vfxSortingLayer) ? "Default" : vfxSortingLayer;
        _previewSr.sortingOrder     = vfxSortingOrder;
        _previewSr.transform.localPosition = (Vector3)vfxLocalOffset;
        _previewSr.transform.localScale    = new Vector3(vfxLocalScale.x, vfxLocalScale.y, 1f);
    }

    void CleanupPreview()
    {
        if (_previewSr == null) return;
        if (Application.isPlaying) Destroy(_previewSr.gameObject);
        else                       DestroyImmediate(_previewSr.gameObject);
        _previewSr = null;
    }
#endif
}
