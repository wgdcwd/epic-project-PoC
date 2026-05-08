using UnityEngine;

/// <summary>
/// 적 캐릭터. 사망 시 골드 드롭.
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
public sealed class EnemyCharacter : CharacterBase
{
    [SerializeField] private int goldDropMin = 10;
    [SerializeField] private int goldDropMax = 30;

    protected override void Awake()
    {
        base.Awake();
        Team             = CharacterTeam.Enemy;
        gameObject.layer = LayerMask.NameToLayer(Layers.Enemy);
    }

    protected override void HandleDeath(GameObject attacker)
    {
        int drop = Random.Range(goldDropMin, goldDropMax + 1);
        LootSystem.HandleEnemyDeath(drop, attacker);
        Destroy(gameObject);
    }
}
