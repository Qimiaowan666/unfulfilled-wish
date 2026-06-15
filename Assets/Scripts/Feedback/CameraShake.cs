using UnityEngine;
using Unity.Cinemachine;

// 相机震屏：走 Cinemachine Impulse。懒加载常驻单例(第一次 Shake 自动生成 GameObject + 自带 ImpulseSource)。
// 生效前提：gameplay 的 vcam 上要挂 CinemachineImpulseListener(本项目已在各场景 vcam 上加好)。
// magnitude = 抖动强度(0~1 量级),duration = 时长;实际位移再乘 forceScale + Listener 的 Gain。
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShake : MonoBehaviour
{
    [Tooltip("magnitude → impulse 力度的缩放(整体抖动强度)")]
    public float forceScale = 6f;

    static CameraShake _inst;
    CinemachineImpulseSource source;

    static CameraShake Inst
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("CameraShake");
                _inst = go.AddComponent<CameraShake>();   // RequireComponent 自动补 ImpulseSource
                DontDestroyOnLoad(go);
            }
            return _inst;
        }
    }

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        source = GetComponent<CinemachineImpulseSource>();
        var def = source.ImpulseDefinition;
        def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;   // 抖动感(非单向推)
        def.ImpulseType  = CinemachineImpulseDefinition.ImpulseTypes.Uniform;   // 不随距离衰减
        source.ImpulseDefinition = def;
    }

    // duration 默认 0.12s、magnitude 默认 0.4。受击/格挡用小值，boss 演出用大值。
    public static void Shake(float duration = 0.12f, float magnitude = 0.4f)
    {
        var s = Inst;
        if (s == null || s.source == null) return;

        var def = s.source.ImpulseDefinition;
        def.ImpulseDuration = Mathf.Max(0.05f, duration);
        s.source.ImpulseDefinition = def;

        // 随机方向，避免每次都同向
        float sx = Random.value < 0.5f ? -1f : 1f;
        float sy = Random.value < 0.5f ? -1f : 1f;
        s.source.DefaultVelocity = new Vector3(Random.Range(0.6f, 1f) * sx, Random.Range(0.6f, 1f) * sy, 0f);
        s.source.GenerateImpulseWithForce(magnitude * s.forceScale);
    }
}
