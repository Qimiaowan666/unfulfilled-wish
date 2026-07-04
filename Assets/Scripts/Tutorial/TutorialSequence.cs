using UnityEngine;

// 统一的分步教学站:玩家踏入触发区 → 逐条显示步骤,做完当前步显示下一步,全部完成 → 开门。
// 所有教学站(移动/攻击/格挡/识破/装备)都用这一套。一步也算"分步"。
//  · 纯 info 单步站(如移动)：像提示牌,进区域显示、出区域隐藏、可反复。
//  · 带条件的步(格挡/识破/击败/捡/装备…)：达成才推进,最后 ForceOpen 关联的门。
[RequireComponent(typeof(Collider2D))]
public class TutorialSequence : MonoBehaviour
{
    // Block = 任意成功格挡(普通或完美都算);PerfectBlock = 只认完美格挡
    public enum Kind { Info, Block, PerfectBlock, Counter, DefeatTarget, Pickup, EquipWeapon }

    [System.Serializable]
    public class Step
    {
        public string title;
        [TextArea(2, 4)] public string body;
        [Tooltip("这一步怎样算完成")]
        public Kind completeOn = Kind.Info;
        [Tooltip("计数类(Block/PerfectBlock/Counter)需要几次")]
        public int count = 1;
        [Tooltip("计数显示标签(如「格挡」);空=不显示计数")]
        public string label = "";
        [Tooltip("DefeatTarget:击败这个敌人")]
        public EnemyBase targetEnemy;
        [Tooltip("Pickup:监听这个拾取物")]
        public EquipmentPickup pickup;
        [Tooltip("EquipWeapon:装上这件武器才算")]
        public EquipmentData equipTarget;

        [System.NonSerialized] public int progress;
    }

    [Tooltip("按顺序的步骤(做完一步显示下一步)")]
    public Step[] steps;
    [Tooltip("计数类的就近判断(只认机关附近的成功);0=不限")]
    public float activeRange = 0f;
    [Tooltip("全部步骤完成 → 开这道门(纯 info 站留空)")]
    public TutorialGate gate;

    int  idx = -1;
    bool running, finished;
    Transform player;

    bool IsInfoOnly => steps != null && steps.Length == 1 && steps[0].completeOn == Kind.Info;

    void Reset() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(Tags.Player)) return;
        if (gate != null && gate.IsOpen) { finished = true; return; }   // 门已开(手动档读回)→ 这站已完成,别再提示
        if (IsInfoOnly) { TutorialPromptUI.Show(steps[0].title, steps[0].body); return; }   // 纯 info:软提示
        if (finished) return;
        if (running) { ReshowCurrent(); return; }   // 离开又回来 → 恢复当前步提示(被别的站/出区域清掉后)
        running = true;
        Advance();
    }

    void ReshowCurrent()
    {
        if (idx < 0 || idx >= steps.Length) return;
        TutorialPromptUI.Show(steps[idx].title, steps[idx].body);
        ShowStatus(idx);
    }

    void Advance()
    {
        Unhook(idx);
        idx++;
        if (steps == null || idx >= steps.Length) { Finish(); return; }
        steps[idx].progress = 0;
        TutorialPromptUI.Show(steps[idx].title, steps[idx].body);
        ShowStatus(idx);
        Hook(idx);
        if (IsSatisfied(idx)) Advance();   // 开局就已满足(如武器已装)→ 跳过
    }

    void Hook(int i)
    {
        switch (steps[i].completeOn)
        {
            case Kind.Block:        CombatSignals.Blocked += OnBlock; CombatSignals.PerfectBlocked += OnBlock; break;   // 任意格挡(普通/完美都算)
            case Kind.PerfectBlock: CombatSignals.PerfectBlocked += OnPerfect; break;                                   // 只认完美
            case Kind.Counter:      CombatSignals.Countered      += OnCounter; break;
            case Kind.DefeatTarget: if (steps[i].targetEnemy != null) steps[i].targetEnemy.OnDied += OnDefeat; break;
            case Kind.Pickup:       if (steps[i].pickup != null) steps[i].pickup.PickedUp += OnPickup; break;
            case Kind.EquipWeapon:  if (EquipmentSystem.Instance != null) EquipmentSystem.Instance.OnEquipmentChanged += OnEquip; break;
            // Info:无完成事件,停在这条(纯 info 站才会用到)
        }
    }

    void Unhook(int i)
    {
        if (steps == null || i < 0 || i >= steps.Length) return;
        switch (steps[i].completeOn)
        {
            case Kind.Block:        CombatSignals.Blocked -= OnBlock; CombatSignals.PerfectBlocked -= OnBlock; break;
            case Kind.PerfectBlock: CombatSignals.PerfectBlocked -= OnPerfect; break;
            case Kind.Counter:      CombatSignals.Countered      -= OnCounter; break;
            case Kind.DefeatTarget: if (steps[i].targetEnemy != null) steps[i].targetEnemy.OnDied -= OnDefeat; break;
            case Kind.Pickup:       if (steps[i].pickup != null) steps[i].pickup.PickedUp -= OnPickup; break;
            case Kind.EquipWeapon:  if (EquipmentSystem.Instance != null) EquipmentSystem.Instance.OnEquipmentChanged -= OnEquip; break;
        }
    }

    void OnBlock()   => Count(Kind.Block);
    void OnPerfect() => Count(Kind.PerfectBlock);
    void OnCounter() => Count(Kind.Counter);
    void OnDefeat()  { if (CurrentIs(Kind.DefeatTarget)) Advance(); }
    void OnPickup()  { if (CurrentIs(Kind.Pickup)) Advance(); }
    void OnEquip()   { if (IsSatisfied(idx)) Advance(); }

    bool CurrentIs(Kind k) => idx >= 0 && idx < steps.Length && steps[idx].completeOn == k;

    // 计数类:就近 + 计数 + 显示,够数则推进
    void Count(Kind k)
    {
        if (!CurrentIs(k)) return;
        if (activeRange > 0f && !PlayerNearby()) return;
        steps[idx].progress++;
        ShowStatus(idx);
        if (steps[idx].progress >= steps[idx].count) Advance();
    }

    void ShowStatus(int i)
    {
        if (i < 0 || i >= steps.Length) return;
        var s = steps[i];
        if (!string.IsNullOrEmpty(s.label))
            TutorialPromptUI.SetStatus($"{s.label}  {Mathf.Min(s.progress, s.count)} / {s.count}");
    }

    bool IsSatisfied(int i)
    {
        if (steps == null || i < 0 || i >= steps.Length) return true;
        var s = steps[i];
        if (s.completeOn == Kind.EquipWeapon)
            return EquipmentSystem.Instance != null && s.equipTarget != null && EquipmentSystem.Instance.weapon == s.equipTarget;
        // 捡取:已拥有这件装备(读档后地上 pickup 已被隐藏)→ 视为已捡,跳过 → 不会卡在"捡不到的捡取步"
        if (s.completeOn == Kind.Pickup)
            return s.pickup != null && s.pickup.equipment != null
                && EquipmentSystem.Instance != null && EquipmentSystem.Instance.HasEquipment(s.pickup.equipment);
        return false;   // 其它类靠事件推进
    }

    bool PlayerNearby()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(Tags.Player);
            if (p != null) player = p.transform;
        }
        return player != null && Mathf.Abs(player.position.x - transform.position.x) <= activeRange;
    }

    void Finish()
    {
        finished = true; running = false;
        TutorialPromptUI.Hide();
        if (gate != null) gate.Open();
    }

    void OnDisable() { Unhook(idx); }
}
