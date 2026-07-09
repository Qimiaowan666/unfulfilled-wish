using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// 对话框（View 版）：布局在 DialogueUI.prefab 木框里摆好，常驻 Bootstrap。
// 剧情/过场用：Play(sequence, onDone) 逐句显示，空格 / 回车 / 左键推进，放完回调。
// 播放期间 IsPlaying=true（接进 PlayerInput.InputBlocked）+ 暂停时间。
[DefaultExecutionOrder(100)]   // 晚于 PlayerInput：结束那帧玩家仍被屏蔽，避免推进键漏成跳跃
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }
    public static bool IsPlaying { get; private set; }

    [Header("引用")]
    public GameObject panelRoot;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public GameObject continueHint;

    List<DialogueLine> lines;
    int index;
    Action onDone;
    float previousTimeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        IsPlaying = false;
    }

    void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Play(DialogueSequence sequence, Action done = null)
    {
        if (IsPlaying) return;   // 重入守卫:正在放对话时又调 Play,会把 previousTimeScale 记成已冻结的 0 → 结束后还原成 0 卡死游戏
        if (sequence == null || sequence.lines == null || sequence.lines.Count == 0)
        {
            done?.Invoke();
            return;
        }

        lines = sequence.lines;
        index = 0;
        onDone = done;
        IsPlaying = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (panelRoot != null) panelRoot.SetActive(true);
        ShowLine();
    }

    void Update()
    {
        if (!IsPlaying) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool advance =
            (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (advance) Next();
    }

    void Next()
    {
        index++;
        if (lines == null || index >= lines.Count) { Finish(); return; }
        ShowLine();
    }

    void ShowLine()
    {
        var line = lines[index];
        if (speakerText != null) speakerText.text = line != null ? line.speaker : "";
        if (bodyText != null) bodyText.text = line != null ? line.text : "";
    }

    void Finish()
    {
        IsPlaying = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = previousTimeScale;

        var cb = onDone;
        onDone = null;
        lines = null;
        cb?.Invoke();
    }
}
