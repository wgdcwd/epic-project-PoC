using System;
using UnityEngine;

/// <summary>
/// 플레이어 스탯. 이벤트를 통해 UI에 변경 사항을 알린다.
/// </summary>
public sealed class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float baseHP      = 100f;
    [SerializeField] private float baseATK     = 10f;
    [SerializeField] private float baseThreat  = 0f;
    [SerializeField] private float baseWealth  = 0f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina  = 100f;

    [Header("Gold")]
    [SerializeField] private int   startingGold = 0;

    // --- 스탯 프로퍼티 ---
    public float BaseHP     => baseHP;
    public float BaseATK    => baseATK;
    public float Threat     { get; private set; }
    public float Wealth     { get; private set; }
    public float MaxStamina => maxStamina;
    public float Stamina    { get; private set; }
    public int   Gold       { get; private set; }

    // 장비 보정치 (EquipmentSlots에서 갱신)
    public float BonusATK    { get; set; }
    public float BonusThreat { get; set; }
    public float BonusWealth { get; set; }

    // 보유 골드에 비례한 스탯 보너스 (상점 대용)
    public float GoldBonusATK => Gold / 100f;
    public float GoldBonusHP  => Gold / 20f;

    public float FinalATK    => (baseATK + BonusATK + GoldBonusATK) * StaminaSystem.GetATKMultiplier(Stamina);
    public float FinalThreat => baseThreat + BonusThreat;
    public float FinalWealth => baseWealth + BonusWealth;

    public event Action OnStatsChanged;
    public event Action<int> OnGoldChanged;

    void Awake()
    {
        Stamina = maxStamina;
        Gold    = startingGold;
        Threat  = baseThreat;
        Wealth  = baseWealth;
    }

    public void ModifyStamina(float delta)
    {
        Stamina = Mathf.Clamp(Stamina + delta, 0f, maxStamina);
        OnStatsChanged?.Invoke();
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    public void RecalculateBonuses(float bonusATK, float bonusThreat, float bonusWealth)
    {
        BonusATK    = bonusATK;
        BonusThreat = bonusThreat;
        BonusWealth = bonusWealth;
        OnStatsChanged?.Invoke();
    }

    // 휴식 시 기력 회복
    public void RestoreStamina(float amount) => ModifyStamina(amount);
}
