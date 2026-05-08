using UnityEngine;

/// <summary>
/// 동료 NPC 파사드. 관계/AI/반응 컴포넌트를 연결하고 이탈 이벤트를 처리.
/// </summary>
[RequireComponent(typeof(CompanionBrain), typeof(CompanionRelationship), typeof(CompanionReaction))]
public sealed class CompanionCharacter : NPCCharacter
{
    public CompanionBrain        Brain        { get; private set; }
    public CompanionRelationship Relationship { get; private set; }
    public CompanionReaction     Reaction     { get; private set; }

    // Trust 변화 감시용 (구간 체크)
    private float _prevTrust = -1f;
    private float _prevStamina = -1f;

    protected override void Awake()
    {
        base.Awake();
        Team         = CharacterTeam.Companion;
        Brain        = GetComponent<CompanionBrain>();
        Relationship = GetComponent<CompanionRelationship>();
        Reaction     = GetComponent<CompanionReaction>();

        gameObject.layer = LayerMask.NameToLayer(Layers.Companion);
    }

    void OnEnable()
    {
        Relationship.OnBetrayalTriggered   += HandleBetrayal;
        Relationship.OnFleeTriggered       += HandleFlee;
        Relationship.OnRetirementTriggered += HandleRetirement;
        Relationship.OnBetrayalWarning     += Reaction.TriggerBetrayalWarning;
        Relationship.OnRetirementWarning   += Reaction.TriggerRetirementWarning;

        NPCStats.OnTrustChanged   += OnTrustChanged;
        NPCStats.OnStaminaChanged += OnStaminaChanged;
        Health.OnDied             += OnDied;
    }

    void OnDisable()
    {
        Relationship.OnBetrayalTriggered   -= HandleBetrayal;
        Relationship.OnFleeTriggered       -= HandleFlee;
        Relationship.OnRetirementTriggered -= HandleRetirement;
        Relationship.OnBetrayalWarning     -= Reaction.TriggerBetrayalWarning;
        Relationship.OnRetirementWarning   -= Reaction.TriggerRetirementWarning;

        NPCStats.OnTrustChanged   -= OnTrustChanged;
        NPCStats.OnStaminaChanged -= OnStaminaChanged;
        Health.OnDied             -= OnDied;
    }

    void Start()
    {
        _prevTrust   = NPCStats.Trust;
        _prevStamina = NPCStats.Stamina;
        PartyRoster.Instance?.AddMember(this);
    }

    // ── 이탈 처리 ────────────────────────────────────────

    private void HandleBetrayal()
    {
        string name = NPCStats.NPCName;
        BubbleManager.ShowBubble(transform, "미안하지만... 이게 낫겠어.");
        LogManager.AddLog($"{name}이(가) 배신했다!");

        // 적대 상태로 전환
        Brain.SetState(CompanionState.Hostile);
        gameObject.layer = LayerMask.NameToLayer(Layers.Enemy);
        GetComponent<Shooter>()?.SetOwner(ProjectileOwner.Enemy);
        PartyRoster.Instance?.RemoveMember(this);
    }

    private void HandleFlee()
    {
        string name = NPCStats.NPCName;
        BubbleManager.ShowBubble(transform, "못 하겠어. 미안.");
        LogManager.AddLog($"{name}이(가) 전투 중 도망쳤다.");

        Brain.SetState(CompanionState.Fleeing);
        PartyRoster.Instance?.RemoveMember(this);
        // 착용 장비 들고 도망 (인벤토리 유지)
    }

    private void HandleRetirement()
    {
        string name  = NPCStats.NPCName;
        float  trust = NPCStats.Trust;

        if (trust >= 50f)
        {
            LogManager.AddLog($"{name}이(가) 장비를 돌려주고 떠났다.");
            Inventory.ReturnAllToPlayer();
        }
        else
        {
            LogManager.AddLog($"{name}이(가) 장비 일부를 들고 떠났다.");
        }

        BubbleManager.ShowBubble(transform, "이만 가볼게.");
        PartyRoster.Instance?.RemoveMember(this);
        Destroy(gameObject, 0.5f);
    }

    private void OnDied(GameObject attacker)
    {
        PartyRoster.Instance?.RemoveMember(this);
        Destroy(gameObject);
    }

    // ── 상태 감시 → 반응 트리거 ──────────────────────────

    private void OnTrustChanged(float newTrust)
    {
        if ((_prevTrust > 40f && newTrust <= 40f) ||
            (_prevTrust > 25f && newTrust <= 25f))
        {
            Reaction.TriggerLowTrust();
        }
        _prevTrust = newTrust;

        // 플레이어 Wealth/Threat 반응도 함께 체크
        var ps = PlayerCharacter.Instance?.Stats;
        if (ps != null)
        {
            Reaction.TriggerGreedWealthReaction(ps.FinalWealth);
            Reaction.TriggerFearThreatReaction(ps.FinalThreat);
        }
    }

    private void OnStaminaChanged()
    {
        float stamina = NPCStats.Stamina;
        if ((_prevStamina > 30f && stamina <= 30f) ||
            (_prevStamina > 10f && stamina <= 10f))
        {
            Reaction.TriggerLowStamina();
        }
        _prevStamina = stamina;
    }

    protected override void HandleDeath(GameObject attacker) { /* OnDied 에서 처리 */ }
}
