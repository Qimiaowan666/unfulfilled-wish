using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 100f;

    [Header("Combat")]
    public float baseAttack = 10f;
    public float baseDefense = 5f;
    [HideInInspector]
    public float attack = 10f;
    [HideInInspector]
    public float defense = 5f;

    [Header("Ghost HP")]
    public float ghostHPBlockRatio = 0.5f;
    public float perfectBlockHealAmount = 20f;
    public float counterHealAmount      = 40f;
    public float attackHealAmount       = 5f;

    [Header("Stamina (技能资源)")]
    public float maxStamina              = 100f;
    public float perfectBlockStaminaGain = 20f;
    public float counterStaminaGain      = 40f;

    [Header("Poise (对敌韧性伤害)")]
    public float perfectBlockPoiseDamage = 5f;    // 完美格挡削减敌人/boss 韧性
    public float counterPoiseDamage      = 15f;   // 识破削韧（识破另有即时硬直，这里再叠韧性进度）

    [Header("Economy")]
    public int gold = 0;

    public float CurrentHP { get; private set; }
    public float CurrentGhostHP { get; private set; }
    public float CurrentStamina { get; private set; }
    public bool IsInvulnerable { get; private set; }
    public bool IsDead => deathTriggered;
    public float EquipmentAttackBonus => equipmentAttackBonus;
    public float EquipmentDefenseBonus => equipmentDefenseBonus;
    public float SkillAttackPercent => skillAttackPercent;
    public float SkillDefensePercent => skillDefensePercent;
    public float SkillAttackBonus => baseAttack * skillAttackPercent / 100f;
    public float SkillDefenseBonus => baseDefense * skillDefensePercent / 100f;

    public event Action<float, float> OnHPChanged;
    public event Action<float, float> OnGhostHPChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action OnStatsChanged;
    public event Action<float> OnDamaged;
    public event Action OnDeath;

    bool deathTriggered;
    float equipmentAttackBonus;
    float equipmentDefenseBonus;
    float skillAttackPercent;
    float skillDefensePercent;

    void Awake()
    {
        RecalculateCombatStats(false);
        CurrentHP = maxHP;
        CurrentGhostHP = 0f;
        CurrentStamina = 0f;       // 起始 0：必须靠完美格挡 / 识破攒
        deathTriggered = false;
    }

    void Start()
    {
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);   // 让 UI 同步初始 0%
    }

    public void GainStamina(float amount)
    {
        if (amount <= 0f || deathTriggered) return;
        float prev = CurrentStamina;
        CurrentStamina = Mathf.Min(CurrentStamina + amount, maxStamina);
        if (!Mathf.Approximately(prev, CurrentStamina))
            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }

    public bool SpendStamina(float amount)
    {
        if (amount <= 0f) return true;
        if (CurrentStamina < amount) return false;
        CurrentStamina -= amount;
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        return true;
    }

    public bool HasStamina(float amount) => CurrentStamina >= amount;

    public void OnNormalBlock(float incomingDamage)
    {
        float ghostGain = incomingDamage * ghostHPBlockRatio;
        float previousGhostHP = CurrentGhostHP;
        CurrentGhostHP += ghostGain;
        float actualGhostGain = CurrentGhostHP - previousGhostHP;

        if (actualGhostGain > 0f)
        {
            CurrentHP = Mathf.Max(CurrentHP - actualGhostGain, 0f);
            OnHPChanged?.Invoke(CurrentHP, maxHP);
            OnDamaged?.Invoke(actualGhostGain);
        }

        NotifyGhostHPChanged();

        if (CurrentHP <= 0f) Die();
    }

    // 把虚血赎回为 HP（完美格挡 / 识破成功 / 攻击命中等共用）
    public void RedeemGhostHP(float amount)
    {
        float actual = Mathf.Min(amount, CurrentGhostHP);
        if (actual <= 0f) return;
        CurrentGhostHP -= actual;
        Heal(actual);
        NotifyGhostHPChanged();
    }

    public void OnAttackHit() => RedeemGhostHP(attackHealAmount);

    public void TakeDamage(float damage)
    {
        if (damage < 0f) { Heal(-damage); return; }
        if (IsInvulnerable || deathTriggered) return;

        if (damage >= maxHP * 0.25f)
            AudioManager.Instance?.PlayHitHeavy();
        else
            AudioManager.Instance?.PlayHitLight();

        if (CurrentGhostHP > 0f)
        {
            CurrentGhostHP = 0f;
            NotifyGhostHPChanged();
        }

        CurrentHP = Mathf.Max(CurrentHP - damage, 0f);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnDamaged?.Invoke(damage);
        Debug.Log($"[{System.DateTime.Now:HH:mm:ss.fff}] [受伤] 主角 -{damage:0.#}  HP {CurrentHP:0}/{maxHP:0}");

        var feedback = GetComponent<DamageFeedback>();
        if (feedback != null) feedback.Flash();
        CameraShake.Shake(0.1f, 0.06f);

        if (CurrentHP <= 0f) Die();
    }

    public void SetInvulnerable(bool value)
    {
        IsInvulnerable = value;
    }

    public void ApplyStatBonus(float attackDelta, float defenseDelta, float maxHPDelta, bool healMaxHPIncrease = false)
    {
        baseAttack += attackDelta;
        baseDefense += defenseDelta;
        RecalculateCombatStats(false);

        if (!Mathf.Approximately(maxHPDelta, 0f))
        {
            maxHP = Mathf.Max(1f, maxHP + maxHPDelta);

            if (maxHPDelta > 0f && healMaxHPIncrease)
                CurrentHP = Mathf.Min(CurrentHP + maxHPDelta, maxHP);
            else
                CurrentHP = Mathf.Min(CurrentHP, maxHP);
        }

        NotifyStatsChanged();
    }

    public void ApplyEquipmentBonus(float attackDelta, float defenseDelta)
    {
        equipmentAttackBonus += attackDelta;
        equipmentDefenseBonus += defenseDelta;
        RecalculateCombatStats();
    }

    public void SetEquipmentBonuses(float attackBonus, float defenseBonus)
    {
        equipmentAttackBonus = attackBonus;
        equipmentDefenseBonus = defenseBonus;
        RecalculateCombatStats();
    }

    public void SetSkillBonusPercent(float attackPercent, float defensePercent)
    {
        skillAttackPercent = attackPercent;
        skillDefensePercent = defensePercent;
        RecalculateCombatStats();
    }

    public void ApplyStatMultiplier(float attackPercent, float defensePercent)
    {
        SetSkillBonusPercent(skillAttackPercent + attackPercent, skillDefensePercent + defensePercent);
    }

    void RecalculateCombatStats(bool notify = true)
    {
        attack = baseAttack + equipmentAttackBonus + SkillAttackBonus;
        defense = baseDefense + equipmentDefenseBonus + SkillDefenseBonus;

        if (notify)
            NotifyStatsChanged();
    }

    public void GetAttackBreakdown(out float baseValue, out float equipmentBonus, out float skillBonus)
    {
        baseValue = baseAttack;
        equipmentBonus = equipmentAttackBonus;
        skillBonus = SkillAttackBonus;
    }

    public void GetDefenseBreakdown(out float baseValue, out float equipmentBonus, out float skillBonus)
    {
        baseValue = baseDefense;
        equipmentBonus = equipmentDefenseBonus;
        skillBonus = SkillDefenseBonus;
    }

    public void RebuildSkillBonuses()
    {
        float attackPercent = 0f;
        float defensePercent = 0f;
        var skills = SkillSystem.Instance;
        if (skills != null)
        {
            foreach (var skill in skills.learnedSkills)
            {
                if (skill == null || skill.type != SkillType.Passive) continue;
                attackPercent += skill.attackPercent;
                defensePercent += skill.defensePercent;
            }
        }

        skillAttackPercent = attackPercent;
        skillDefensePercent = defensePercent;
        RecalculateCombatStats(false);
        NotifyStatsChanged();
    }

    public void LoadBaseStats(float baseAttackValue, float baseDefenseValue, float baseMaxHPValue)
    {
        baseAttack = Mathf.Max(0f, baseAttackValue);
        baseDefense = Mathf.Max(0f, baseDefenseValue);
        maxHP = Mathf.Max(1f, baseMaxHPValue);
        equipmentAttackBonus = 0f;
        equipmentDefenseBonus = 0f;
        skillAttackPercent = 0f;
        skillDefensePercent = 0f;
        CurrentHP = Mathf.Min(CurrentHP, maxHP);
        RecalculateCombatStats(false);
        NotifyStatsChanged();
    }

    public void LoadSavedVitals(float currentHP, float currentGhostHP, int savedGold)
    {
        deathTriggered = false;
        IsInvulnerable = false;
        CurrentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        CurrentGhostHP = Mathf.Max(0f, currentGhostHP);
        gold = Mathf.Max(0, savedGold);
        NotifyStatsChanged();

        if (CurrentHP <= 0f)
            Die();
    }

    public void AddGold(int amount)
    {
        gold = Mathf.Max(0, gold + amount);
        NotifyStatsChanged();
    }

    public void RestoreAll()
    {
        deathTriggered = false;
        IsInvulnerable = false;
        CurrentHP = maxHP;
        CurrentGhostHP = 0f;
        NotifyStatsChanged();
    }

    public void Kill()
    {
        if (deathTriggered) return;

        IsInvulnerable = false;
        CurrentGhostHP = 0f;
        CurrentHP = 0f;
        NotifyGhostHPChanged();
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnDamaged?.Invoke(maxHP);
        Die();
    }

    void Heal(float amount)
    {
        if (deathTriggered) return;

        CurrentHP = Mathf.Min(CurrentHP + amount, maxHP);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }

    void NotifyGhostHPChanged()
    {
        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxHP);
    }

    void NotifyStatsChanged()
    {
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        NotifyGhostHPChanged();
        OnStatsChanged?.Invoke();
    }

    void Die()
    {
        if (deathTriggered) return;

        deathTriggered = true;
        AudioManager.Instance?.PlayDeath();
        OnDeath?.Invoke();
        GameManager.Instance?.TriggerGameOver();   // 任何场景死亡都触发 GameOver（不依赖关卡级 LevelManager）
    }
}
