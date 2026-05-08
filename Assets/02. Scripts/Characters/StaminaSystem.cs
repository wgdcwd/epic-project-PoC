using System;
using UnityEngine;

public enum StaminaState { Normal, Tired, Exhausted, CriticallyExhausted, KnockedOut }

/// <summary>
/// 기력 관리 컴포넌트. IStaminaModifier 인터페이스를 통해 스탯 클래스에 기력값을 위임.
/// </summary>
public sealed class StaminaSystem : MonoBehaviour
{
    [SerializeField] private float decayPerTenSeconds = 1f;
    private float _decayTimer;

    // 기력값은 외부(PlayerStats/NPCStats)에서 주입
    private Func<float>        _getStamina;
    private Action<float>      _modifyStamina;

    public void Initialize(Func<float> getter, Action<float> modifier)
    {
        _getStamina    = getter;
        _modifyStamina = modifier;
    }

    void Update()
    {
        if (_getStamina == null) return;
        _decayTimer += Time.deltaTime;
        if (_decayTimer >= 10f)
        {
            _decayTimer -= 10f;
            _modifyStamina?.Invoke(-decayPerTenSeconds);
        }
    }

    public void OnRangedAttack()  => _modifyStamina?.Invoke(-1f);
    public void OnCombatJoined()  => _modifyStamina?.Invoke(-5f);
    public void OnHit()           => _modifyStamina?.Invoke(-3f);

    public static StaminaState GetState(float stamina)
    {
        return stamina switch
        {
            >= 70f  => StaminaState.Normal,
            >= 40f  => StaminaState.Tired,
            >= 20f  => StaminaState.Exhausted,
            > 0f    => StaminaState.CriticallyExhausted,
            _       => StaminaState.KnockedOut
        };
    }

    /// <summary>스탯 보정치: ATK 배율 반환 (0.6 ~ 1.0)</summary>
    public static float GetATKMultiplier(float stamina)
    {
        return GetState(stamina) switch
        {
            StaminaState.Exhausted           => 0.8f,
            StaminaState.CriticallyExhausted => 0.6f,
            StaminaState.KnockedOut          => 0f,
            _                                => 1f
        };
    }
}
