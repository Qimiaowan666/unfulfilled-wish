using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 通用特效播放器：常驻单例 + 对象池。特效做成 Resources 下的 ParticleSystem 预制，
// 调用处只需 VfxManager.Play(...)（一次性）或 PlayLoop/StopLoop（持续/跟随）。新增特效 = 做个预制 + 一行调用，不写脚本。
public class VfxManager : MonoBehaviour
{
    static VfxManager _inst;
    static VfxManager Inst
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("VfxManager");
                _inst = go.AddComponent<VfxManager>();
                DontDestroyOnLoad(go);
            }
            return _inst;
        }
    }

    readonly Dictionary<string, GameObject>        prefabs    = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Queue<GameObject>> pool       = new Dictionary<string, Queue<GameObject>>();
    readonly Dictionary<GameObject, string>        keyOf      = new Dictionary<GameObject, string>();
    readonly Dictionary<GameObject, Color[]>       baseColors = new Dictionary<GameObject, Color[]>();   // 每个实例各子系统的预制原色（tint 在其上相乘）

    // ── 一次性（自动回收）─────────────────────────────
    public static void Play(string key, Vector3 pos, Quaternion rot, float scale = 1f, Color? tint = null, SpriteRenderer sortRef = null)
        => Inst.PlayInternal(key, pos, rot, scale, tint ?? Color.white, sortRef);

    void PlayInternal(string key, Vector3 pos, Quaternion rot, float scale, Color tint, SpriteRenderer sortRef)
    {
        var inst = Spawn(key, scale, tint, sortRef, out float maxLife);
        if (inst == null) return;
        inst.transform.SetParent(null, false);
        inst.transform.SetPositionAndRotation(pos, rot);
        PlayRoot(inst);
        StartCoroutine(Recycle(inst, maxLife + 0.15f));
    }

    // ── 持续/循环（跟随父物体，手动 StopLoop）────────────
    public static GameObject PlayLoop(string key, Transform parent, Vector3 localOffset, float scale = 1f, Color? tint = null, SpriteRenderer sortRef = null)
        => Inst.PlayLoopInternal(key, parent, localOffset, scale, tint ?? Color.white, sortRef);

    GameObject PlayLoopInternal(string key, Transform parent, Vector3 localOffset, float scale, Color tint, SpriteRenderer sortRef)
    {
        var inst = Spawn(key, scale, tint, sortRef, out _);
        if (inst == null) return null;
        inst.transform.SetParent(parent, false);
        inst.transform.localPosition = localOffset;
        inst.transform.localRotation = Quaternion.identity;
        PlayRoot(inst);
        return inst;   // 调用方保存句柄，结束时调 StopLoop
    }

    public static void StopLoop(GameObject inst) { if (_inst != null) _inst.StopLoopInternal(inst); }

    void StopLoopInternal(GameObject inst)
    {
        if (inst == null) return;
        foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);   // 停止发射，已有粒子自然消散
        StartCoroutine(RecycleWhenDead(inst));
    }

    // ── 内部 ─────────────────────────────────────────
    GameObject Spawn(string key, float scale, Color tint, SpriteRenderer sortRef, out float maxLife)
    {
        maxLife = 0f;
        var prefab = LoadPrefab(key);
        if (prefab == null) { Debug.LogWarning($"[VfxManager] 找不到特效预制 Resources/{key}"); return null; }

        var inst = Rent(key, prefab);
        keyOf[inst] = key;
        inst.transform.localScale = prefab.transform.localScale * scale;
        inst.SetActive(true);

        baseColors.TryGetValue(inst, out var baseCols);
        var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            var m = ps.main;
            Color baseC = (baseCols != null && i < baseCols.Length) ? baseCols[i] : Color.white;
            m.startColor = baseC * tint;   // 在预制原色上相乘（tint=白则保持原色）
            //整个特效完全播完要多久
            maxLife = Mathf.Max(maxLife, m.duration + Mathf.Max(m.startLifetime.constant, m.startLifetime.constantMax));  
            if (sortRef != null)
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.sortingLayerID = sortRef.sortingLayerID;
                r.sortingOrder   = sortRef.sortingOrder + 30;
            }
        }
        return inst;
    }

    static void PlayRoot(GameObject inst)
    {
        var root = inst.GetComponent<ParticleSystem>();
        if (root != null) { root.Clear(true); root.Play(true); }
    }

    GameObject LoadPrefab(string key)
    {
        if (prefabs.TryGetValue(key, out var p) && p != null) return p;
        p = Resources.Load<GameObject>(key);
        prefabs[key] = p;
        return p;
    }

    //对象池
    GameObject Rent(string key, GameObject prefab)
    {
        // 翻有没有一个"活的"直接复用
        if (pool.TryGetValue(key, out var q))
            while (q.Count > 0) { var g = q.Dequeue(); if (g != null) return g; }


        // 没有就实例化一个
        var inst = Instantiate(prefab);
        var systems = inst.GetComponentsInChildren<ParticleSystem>(true);   // 首次实例化时记下各子系统原色
        var cols = new Color[systems.Length];
        for (int i = 0; i < systems.Length; i++) cols[i] = systems[i].main.startColor.color;
        baseColors[inst] = cols;
        return inst;
    }

    void ReturnToPool(GameObject inst)
    {
        if (inst == null) return;
        inst.SetActive(false);
        inst.transform.SetParent(transform, false);
        if (!keyOf.TryGetValue(inst, out var key)) { Destroy(inst); return; }
        //return时 不在池子里就加到池子里
        if (!pool.TryGetValue(key, out var q)) { q = new Queue<GameObject>(); pool[key] = q; }
        q.Enqueue(inst);
    }

    IEnumerator Recycle(GameObject inst, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(inst);
    }

    IEnumerator RecycleWhenDead(GameObject inst)
    {
        var root = inst != null ? inst.GetComponent<ParticleSystem>() : null;
        float timeout = 3f;
        while (inst != null && root != null && root.IsAlive(true) && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
        ReturnToPool(inst);
    }
}
