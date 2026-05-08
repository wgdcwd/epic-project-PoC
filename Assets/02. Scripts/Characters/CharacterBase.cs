using UnityEngine;

public enum CharacterTeam { Player, Companion, Enemy, Neutral }

/// <summary>
/// 모든 캐릭터의 공통 베이스. HealthComponent를 필수로 요구.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public abstract class CharacterBase : MonoBehaviour
{
    public CharacterTeam Team { get; protected set; }
    public HealthComponent Health { get; private set; }

    protected virtual void Awake()
    {
        Health = GetComponent<HealthComponent>();
        Health.OnDied += HandleDeath;
    }

    protected virtual void OnDestroy()
    {
        if (Health != null) Health.OnDied -= HandleDeath;
    }

    protected abstract void HandleDeath(GameObject attacker);
}
