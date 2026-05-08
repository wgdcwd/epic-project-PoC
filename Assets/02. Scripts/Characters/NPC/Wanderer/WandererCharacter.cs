using UnityEngine;

/// <summary>
/// 비동료 NPC. 플레이어가 우클릭하면 영입 메뉴가 열린다.
/// 공격받으면 도망치거나 사망.
/// </summary>
public sealed class WandererCharacter : NPCCharacter
{
    protected override void Awake()
    {
        base.Awake();
        Team             = CharacterTeam.Neutral;
        gameObject.layer = LayerMask.NameToLayer(Layers.WandererNPC);
    }

    void OnEnable() => Health.OnDied += OnDied;
    void OnDisable() => Health.OnDied -= OnDied;

    private void OnDied(GameObject attacker) => Destroy(gameObject);

    protected override void HandleDeath(GameObject attacker) { }

    /// <summary>영입 성공 시 CompanionCharacter로 전환하고 WandererCharacter를 제거.</summary>
    public CompanionCharacter ConvertToCompanion(float initialTrust)
    {
        // 필요한 컴포넌트 추가
        var companion     = gameObject.AddComponent<CompanionCharacter>();
        var brain         = gameObject.AddComponent<CompanionBrain>();
        var relationship  = gameObject.AddComponent<CompanionRelationship>();
        var reaction      = gameObject.AddComponent<CompanionReaction>();
        var shooter       = gameObject.GetComponent<Shooter>() ?? gameObject.AddComponent<Shooter>();

        shooter.SetOwner(ProjectileOwner.Companion);

        // Trust 초기값 설정 (Start 이전에 직접 설정)
        NPCStats.SetTrust(initialTrust);

        // WandererCharacter 제거 (자신)
        Destroy(this);
        return companion;
    }
}
