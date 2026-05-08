using System;
using UnityEngine;

/// <summary>
/// NPC 성향 스탯. CompanionCharacter와 WandererCharacter 모두 사용.
/// 모든 값은 0~100 범위.
/// </summary>
public sealed class NPCStats : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string npcName = "NPC";

    [Header("Combat")]
    [SerializeField] private float baseHP      = 100f;
    [SerializeField] private float baseATK     = 8f;
    [SerializeField] private float maxStamina  = 100f;

    [Header("Personality (0~100)")]
    [SerializeField, Range(0, 100)] private float trust    = 50f;
    [SerializeField, Range(0, 100)] private float greed    = 50f;
    [SerializeField, Range(0, 100)] private float fear     = 50f;
    [SerializeField, Range(0, 100)] private float morality = 50f;

    public string NPCName   => npcName;
    public float  BaseHP    => baseHP;
    public float  BaseATK   => baseATK;
    public float  MaxStamina => maxStamina;

    // --- 퍼블릭 읽기 ---
    public float Trust    { get; private set; }
    public float Greed    => greed;
    public float Fear     => fear;
    public float Morality => morality;
    public float Stamina  { get; private set; }

    // 장비 보정
    public float BonusATK    { get; set; }
    public float FinalATK    => (baseATK + BonusATK) * StaminaSystem.GetATKMultiplier(Stamina);

    public event Action<float> OnTrustChanged;   // newTrust
    public event Action        OnStaminaChanged;

    void Awake()
    {
        Trust   = Mathf.Clamp(trust, 0f, 100f);
        Stamina = maxStamina;
    }

    public void ModifyTrust(float delta)
    {
        Trust = Mathf.Clamp(Trust + delta, 0f, 100f);
        OnTrustChanged?.Invoke(Trust);
    }

    public void SetTrust(float value)
    {
        Trust = Mathf.Clamp(value, 0f, 100f);
        OnTrustChanged?.Invoke(Trust);
    }

    public void ModifyStamina(float delta)
    {
        Stamina = Mathf.Clamp(Stamina + delta, 0f, maxStamina);
        OnStaminaChanged?.Invoke();
    }

    public void RestoreStamina(float amount) => ModifyStamina(amount);
}
