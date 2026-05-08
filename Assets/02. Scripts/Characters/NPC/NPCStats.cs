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

    [Header("Spawn Randomization")]
    [Tooltip("체크 시 Awake에서 이름/성향 값을 랜덤화 (Wanderer 단일 프리팹 다양성 확보용)")]
    [SerializeField] private bool randomizeOnSpawn = false;

    private static readonly string[] NamePool = {
        "박민호", "정찬서", "유윤상", "김우성", "이재준",
        "최도윤", "한지원", "오세훈", "장태현", "남궁민",
        "권시우", "윤서아", "조하늘", "강현우", "임도현"
    };

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

    [Header("Wallet")]
    [SerializeField] private int gold = 0;
    public int Gold => gold;

    public event Action<float> OnTrustChanged;   // newTrust
    public event Action        OnStaminaChanged;

    void Awake()
    {
        if (randomizeOnSpawn) Randomize();
        Trust   = Mathf.Clamp(trust, 0f, 100f);
        Stamina = maxStamina;
    }

    private void Randomize()
    {
        npcName  = NamePool[UnityEngine.Random.Range(0, NamePool.Length)];
        trust    = UnityEngine.Random.Range(40f, 65f);
        greed    = UnityEngine.Random.Range(20f, 95f);
        fear     = UnityEngine.Random.Range(20f, 90f);
        morality = UnityEngine.Random.Range(20f, 90f);
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

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
    }

    public bool SpendGold(int amount)
    {
        if (amount < 0 || gold < amount) return false;
        gold -= amount;
        return true;
    }

    /// <summary>NPC가 들고 있던 골드 전부를 빼서 반환 (사망 시 회수용).</summary>
    public int TakeAllGold()
    {
        int g = gold;
        gold = 0;
        return g;
    }
}
