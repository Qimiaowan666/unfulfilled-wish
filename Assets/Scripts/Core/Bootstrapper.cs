using UnityEngine;
using UnityEngine.SceneManagement;

// 挂在 Bootstrap.unity。同场景的 GameManager / AudioManager 在 Awake 完成
// DontDestroyOnLoad，本脚本 Start 时跳转：
//   - 发布版：跳 firstScene（MainMenu）
//   - 编辑器：跳回"按 Play 时打开的那个场景"（方便直接测当前场景，不被强制踢到 MainMenu）
public class Bootstrapper : MonoBehaviour
{
    [SerializeField] string firstScene = "MainMenu";

    void Start()
    {
        string target = firstScene;

#if UNITY_EDITOR
        // SetPlayModeStartScene 在 ExitingEditMode 时记下了按 Play 前的场景
        string editorReturn = UnityEditor.SessionState.GetString("Bootstrap_ReturnScene", "");
        if (!string.IsNullOrEmpty(editorReturn) && editorReturn != "Bootstrap")
            target = editorReturn;
#endif

        SceneManager.LoadScene(target);
    }
}
