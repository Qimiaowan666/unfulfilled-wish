// 战斗成功事件的广播中枢：玩家成功格挡 / 完美格挡 / 识破时触发。
// 给教程机关(TutorialGate)这类"做对某动作才放行"的逻辑订阅用，与具体敌人/场景解耦。
public static class CombatSignals
{
    public static event System.Action Blocked;        // 任意成功格挡(普通或完美都算)
    public static event System.Action PerfectBlocked;  // 完美格挡
    public static event System.Action Countered;       // 识破成功

    public static void RaiseBlocked()        => Blocked?.Invoke();
    public static void RaisePerfectBlocked() => PerfectBlocked?.Invoke();
    public static void RaiseCountered()      => Countered?.Invoke();
}
