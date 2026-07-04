using UnityEngine;

/// <summary>
/// 全局设置：音量 / 全屏 / 分辨率。集中读写 PlayerPrefs，启动时自动应用。
/// 设置面板（PauseMenu 的设置子页）改值时调这里的 SetXxx。
/// </summary>
public static class GameSettings
{
    public const string KeyBgm        = "set_bgm";
    public const string KeySfx        = "set_sfx";
    public const string KeyFullscreen = "set_fullscreen";
    public const string KeyResW       = "set_resW";
    public const string KeyResH       = "set_resH";

    // 设置面板下拉用的常见分辨率
    public static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1366, 768),
        new Vector2Int(1280, 720),
    };

    // 启动时把上次保存的全屏 / 分辨率应用上（音量由 AudioManager 自己读）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnLaunch()
    {
        if (PlayerPrefs.HasKey(KeyResW))
        {
            bool fs = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            int w = PlayerPrefs.GetInt(KeyResW, Screen.width);
            int h = PlayerPrefs.GetInt(KeyResH, Screen.height);
            Screen.SetResolution(w, h, fs);
        }
        else if (PlayerPrefs.HasKey(KeyFullscreen))
        {
            Screen.fullScreen = PlayerPrefs.GetInt(KeyFullscreen) == 1;
        }
    }

    public static void SetBGMVolume(float v)
    {
        PlayerPrefs.SetFloat(KeyBgm, v);
        PlayerPrefs.Save();
        AudioManager.Instance?.SetBGMVolume(v);
    }

    public static void SetSFXVolume(float v)
    {
        PlayerPrefs.SetFloat(KeySfx, v);
        PlayerPrefs.Save();
        AudioManager.Instance?.SetSFXVolume(v);
    }

    public static void SetFullscreen(bool on)
    {
        Screen.fullScreen = on;
        PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetResolution(int index)
    {
        if (index < 0 || index >= Resolutions.Length) return;
        var r = Resolutions[index];
        Screen.SetResolution(r.x, r.y, Screen.fullScreen);
        PlayerPrefs.SetInt(KeyResW, r.x);
        PlayerPrefs.SetInt(KeyResH, r.y);
        PlayerPrefs.Save();
    }

    public static float BGMVolume => PlayerPrefs.GetFloat(KeyBgm, AudioManager.Instance != null ? AudioManager.Instance.bgmVolume : 0.5f);
    public static float SFXVolume => PlayerPrefs.GetFloat(KeySfx, AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.8f);

    public static int CurrentResolutionIndex()
    {
        int w = PlayerPrefs.GetInt(KeyResW, Screen.width);
        int h = PlayerPrefs.GetInt(KeyResH, Screen.height);
        for (int i = 0; i < Resolutions.Length; i++)
            if (Resolutions[i].x == w && Resolutions[i].y == h) return i;
        return 0;
    }
}
