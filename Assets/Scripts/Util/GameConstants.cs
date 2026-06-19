// 集中存放散落各处的字符串常量（标签 / 层 / 场景名），避免裸字符串多处重复、改一处漏一处。

public static class Tags
{
    public const string Player = "Player";
}

public static class Layers
{
    public const string Ground = "Ground";
    public const string Player = "Player";
}

public static class SceneNames
{
    public const string MainMenu       = "MainMenu";
    public const string Bootstrap      = "Bootstrap";
    public const string Tutorial       = "Tutorial";
    public const string ForsakenShrine = "ForsakenShrine";

    // 非游玩场景（主菜单 / Bootstrap）：这些场景里不暂停、不开角色面板、不自动读档。
    static readonly string[] nonGameplay = { MainMenu, Bootstrap };

    public static bool IsNonGameplay(string sceneName)
    {
        foreach (var s in nonGameplay)
            if (sceneName == s) return true;
        return false;
    }

    public static bool IsGameplay(string sceneName) => !IsNonGameplay(sceneName);
}
