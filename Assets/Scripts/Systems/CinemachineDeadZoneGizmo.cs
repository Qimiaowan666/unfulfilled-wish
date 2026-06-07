using UnityEngine;
using Unity.Cinemachine;

// 在场景视图实时画出 Cinemachine PositionComposer 的死区 + 硬限
// 挂在 CinemachineCamera GameObject 上（跟 PositionComposer 同物体）
[ExecuteAlways]
[RequireComponent(typeof(CinemachinePositionComposer))]
[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineDeadZoneGizmo : MonoBehaviour
{
    [Header("显示开关")]
    public bool showDeadZone   = true;
    public bool showHardLimits = true;
    public bool alwaysVisible  = true;   // false = 仅选中时显示

    [Header("颜色")]
    public Color deadZoneColor   = new Color(0f, 1f, 0f, 0.85f);    // 绿色 = 死区
    public Color hardLimitsColor = new Color(1f, 0.3f, 0.3f, 0.85f); // 红色 = 硬限

    [Header("Filled 半透明叠加")]
    public bool drawFilled = false;

    void OnDrawGizmos()
    {
        if (alwaysVisible) Draw();
    }

    void OnDrawGizmosSelected()
    {
        if (!alwaysVisible) Draw();
    }

    void Draw()
    {
        var composer = GetComponent<CinemachinePositionComposer>();
        var vcam     = GetComponent<CinemachineCamera>();
        if (composer == null || vcam == null) return;

        var lens = vcam.Lens;
        if (lens.OrthographicSize <= 0f) return;

        // 相机视野尺寸（屏幕高 = OrthoSize * 2，宽 = 高 * aspect）
        float screenH = lens.OrthographicSize * 2f;
        var mainCam = Camera.main;
        float aspect = mainCam != null && mainCam.aspect > 0f ? mainCam.aspect : 16f / 9f;
        float screenW = screenH * aspect;

        var comp = composer.Composition;

        // ScreenPosition (-0.5 到 0.5) 表示 target 在屏幕里的相对位置（0 = 中心）
        Vector3 center = transform.position;
        Vector3 focusOffset = new Vector3(
            comp.ScreenPosition.x * screenW,
            comp.ScreenPosition.y * screenH,
            0f);
        Vector3 focusCenter = center + focusOffset;

        // 死区（绿色）
        if (showDeadZone)
        {
            Vector3 dzSize = new Vector3(
                comp.DeadZone.Size.x * screenW,
                comp.DeadZone.Size.y * screenH,
                0.05f);
            Gizmos.color = deadZoneColor;
            Gizmos.DrawWireCube(focusCenter, dzSize);
            if (drawFilled)
            {
                var fillColor = deadZoneColor; fillColor.a *= 0.15f;
                Gizmos.color = fillColor;
                Gizmos.DrawCube(focusCenter, dzSize);
            }
        }

        // 硬限（红色）
        if (showHardLimits)
        {
            Vector3 hlSize = new Vector3(
                comp.HardLimits.Size.x * screenW,
                comp.HardLimits.Size.y * screenH,
                0.05f);
            Vector3 hlOffset = new Vector3(
                comp.HardLimits.Offset.x * screenW,
                comp.HardLimits.Offset.y * screenH,
                0f);
            Gizmos.color = hardLimitsColor;
            Gizmos.DrawWireCube(focusCenter + hlOffset, hlSize);
            if (drawFilled)
            {
                var fillColor = hardLimitsColor; fillColor.a *= 0.1f;
                Gizmos.color = fillColor;
                Gizmos.DrawCube(focusCenter + hlOffset, hlSize);
            }
        }
    }
}
