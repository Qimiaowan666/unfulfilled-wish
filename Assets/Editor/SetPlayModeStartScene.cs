#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

// 1) 自动把 Play Mode Start Scene 设成 Bootstrap → 任意场景按 Play 都先过 Bootstrap 启动 manager
// 2) 进 PlayMode 前记下"当前打开的场景"，供 Bootstrapper 跳回（开发期直接测当前场景）
[InitializeOnLoad]
static class SetPlayModeStartScene
{
    const string BootstrapPath   = "Assets/Scenes/Bootstrap.unity";
    const string ReturnSceneKey  = "Bootstrap_ReturnScene";

    static SetPlayModeStartScene()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath);
        if (scene != null && EditorSceneManager.playModeStartScene != scene)
            EditorSceneManager.playModeStartScene = scene;

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // 按下 Play、即将离开编辑模式时，记录当前激活场景名
        if (state == PlayModeStateChange.ExitingEditMode)
            SessionState.SetString(ReturnSceneKey, EditorSceneManager.GetActiveScene().name);
    }
}
#endif
