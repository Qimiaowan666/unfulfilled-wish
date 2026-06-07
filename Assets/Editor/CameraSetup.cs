#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;

// 一键搭建 Cinemachine 相机，配置照 Nine Sols 风格（横向灵敏、纵向迟钝、关 Lookahead）
// 用法：Tools > Setup > Add Cinemachine Camera (Player Follow)
public static class CameraSetup
{
    [MenuItem("Tools/Setup/Add Cinemachine Camera (Player Follow)")]
    public static void Setup()
    {
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[CameraSetup] 当前场景没有 Main Camera，先建一个。");
            return;
        }

        // 1. Main Camera → Orthographic + size 8 + 挂 CinemachineBrain
        mainCam.orthographic = true;
        mainCam.orthographicSize = 8f;
        if (mainCam.GetComponent<CinemachineBrain>() == null)
            Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);

        // 2. 移除旧的简易 CameraFollow（如果有）
        var oldFollow = mainCam.GetComponent<CameraFollow>();
        if (oldFollow != null)
        {
            Undo.DestroyObjectImmediate(oldFollow);
            Debug.Log("[CameraSetup] 移除 Main Camera 上旧的 CameraFollow 组件。");
        }

        // 3. 检查是否已有 CinemachineCamera 物体
        var existing = GameObject.Find("CinemachineCamera");
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            Debug.LogWarning("[CameraSetup] 场景里已有 CinemachineCamera。选中它，重设参数后退出。");
            ConfigureVCam(existing);
            return;
        }

        // 4. 新建 CinemachineCamera GameObject + 配置
        var go = new GameObject("CinemachineCamera");
        Undo.RegisterCreatedObjectUndo(go, "Add CinemachineCamera");
        go.AddComponent<CinemachineCamera>();
        go.AddComponent<CinemachinePositionComposer>();
        var confiner = go.AddComponent<CinemachineConfiner2D>();
        confiner.enabled = false;   // 等用户后续加 PolygonCollider 边界再开

        ConfigureVCam(go);

        Selection.activeGameObject = go;
        EditorUtility.SetDirty(go);
    }

    static void ConfigureVCam(GameObject go)
    {
        var vcam     = go.GetComponent<CinemachineCamera>();
        var composer = go.GetComponent<CinemachinePositionComposer>();
        if (vcam == null || composer == null) return;

        // Lens
        var lens = vcam.Lens;
        lens.OrthographicSize = 8f;
        vcam.Lens = lens;

        // Tracking Target → 自动绑 PlayerController（如果场景里有）
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            var t = vcam.Target;
            t.TrackingTarget = player.transform;
            vcam.Target = t;
        }

        // PositionComposer（Nine Sols 风格：横快纵慢，关 Lookahead）
        composer.CameraDistance   = 10f;
        composer.TargetOffset     = Vector3.zero;
        composer.Damping          = new Vector3(0.3f, 0.7f, 1f);   // X 灵敏、Y 迟钝
        composer.CenterOnActivate = true;

        var comp = composer.Composition;
        comp.ScreenPosition       = new Vector2(0.05f, -0.17f);    // 玩家略右、下方 1/3，给上方平台留视野
        comp.DeadZone.Enabled     = true;
        comp.DeadZone.Size        = new Vector2(0.20f, 0.35f);     // 死区横向更紧
        comp.HardLimits.Enabled   = true;
        comp.HardLimits.Size      = new Vector2(0.6f, 0.8f);
        comp.HardLimits.Offset    = Vector2.zero;
        composer.Composition = comp;

        // Lookahead 关闭（Nine Sols 不依赖前瞻，纯延迟跟随）
        var look = composer.Lookahead;
        look.Enabled   = false;
        look.Time      = 0f;
        look.Smoothing = 0f;
        look.IgnoreY   = false;
        composer.Lookahead = look;

        EditorUtility.SetDirty(composer);
        EditorUtility.SetDirty(vcam);

        Debug.Log("[CameraSetup] ✓ Cinemachine 配置完成。" +
                  (player != null ? " Player 已自动绑 TrackingTarget。"
                                  : " 未找到 PlayerController — 手动拖 Player 进 TrackingTarget。"));
    }
}
#endif
