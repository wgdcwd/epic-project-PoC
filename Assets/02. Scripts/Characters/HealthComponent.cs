using System;
using UnityEngine;

/// <summary>
/// HP 관리 컴포넌트. Player/NPC/Enemy 모두 공유.
/// </summary>
public sealed class HealthComponent : MonoBehaviour
{
    [SerializeField] private float maxHP = 100f;

    public float MaxHP       => maxHP;
    public float CurrentHP   { get; private set; }
    public float HPRatio     => CurrentHP / MaxHP;
    public bool  IsAlive     => CurrentHP > 0f;

    public event Action<float, GameObject> OnDamaged; // (amount, attacker)
    public event Action<GameObject>        OnDied;    // (attacker)

    void Awake() => CurrentHP = maxHP;

    public void SetMaxHP(float value, bool refillOnSet = false)
    {
        maxHP = Mathf.Max(1f, value);
        if (refillOnSet) CurrentHP = maxHP;
        else             CurrentHP = Mathf.Min(CurrentHP, maxHP);
    }

    public void TakeDamage(float amount, GameObject attacker)
    {
        if (!IsAlive || amount <= 0f) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        OnDamaged?.Invoke(amount, attacker);
        if (!IsAlive) OnDied?.Invoke(attacker);
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
    }

    public void HealPercent(float ratio) => Heal(maxHP * ratio);
}
