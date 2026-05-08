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

    // 평소 점수 패시브 체크
    private float _passiveCheckTimer;
    private const float PassiveCheckInterval = 6f;

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
        Relationship.OnTheftTriggered      += HandleTheft;
        Relationship.OnBetrayalWarning     += Reaction.TriggerBetrayalWarning;
        Relationship.OnRetirementWarning   += Reaction.TriggerRetirementWarning;

        NPCStats.OnTrustChanged   += OnTrustChanged;
        NPCStats.OnStaminaChanged += OnStaminaChanged;
        Health.OnDied             += OnDied;
        Health.OnDamaged          += OnHealthDamaged;
    }

    void OnDisable()
    {
        Relationship.OnBetrayalTriggered   -= HandleBetrayal;
        Relationship.OnFleeTriggered       -= HandleFlee;
        Relationship.OnRetirementTriggered -= HandleRetirement;
        Relationship.OnTheftTriggered      -= HandleTheft;
        Relationship.OnBetrayalWarning     -= Reaction.TriggerBetrayalWarning;
        Relationship.OnRetirementWarning   -= Reaction.TriggerRetirementWarning;

        NPCStats.OnTrustChanged   -= OnTrustChanged;
        NPCStats.OnStaminaChanged -= OnStaminaChanged;
        Health.OnDied             -= OnDied;
        Health.OnDamaged          -= OnHealthDamaged;
    }

    /// <summary>휴식 중인데 공격받으면 깨운다.</summary>
    private void OnHealthDamaged(float amount, GameObject attacker)
    {
        if (RestSystem.Instance != null && RestSystem.Instance.IsResting)
        {
            RestSystem.Instance.ForceWake($"{NPCStats.NPCName}이(가) 공격받았다!");
        }
    }

    void Start()
    {
        _prevTrust   = NPCStats.Trust;
        _prevStamina = NPCStats.Stamina;
        _passiveCheckTimer = UnityEngine.Random.Range(2f, PassiveCheckInterval);
        PartyRoster.Instance?.AddMember(this);
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        _passiveCheckTimer -= Time.deltaTime;
        if (_passiveCheckTimer <= 0f)
        {
            _passiveCheckTimer = PassiveCheckInterval;
            // 평소에도 점수 임계값 통과하면 배신/도주/탈퇴 발동
            Relationship.CheckImmediateThresholds();
        }
    }

    // ── 이탈 처리 ────────────────────────────────────────

    private void HandleBetrayal()
    {
        string name = NPCStats.NPCName;
        BubbleManager.ShowBubble(transform, "미안하지만... 이게 낫겠어.");
        LogManager.AddLog($"{name}이(가) 배신했다!");

        bool wasResting = RestSystem.Instance != null && RestSystem.Instance.IsResting;

        // 휴식 중이면 다른 동료들에게 알림 (말풍선)
        if (wasResting && PartyRoster.Instance != null)
        {
            foreach (var c in PartyRoster.Instance.Members)
            {
                if (c == null || c == this) continue;
                BubbleManager.ShowBubble(c.transform, $"{name}이(가) 배신했어!");
            }
        }

        // 적대 상태로 전환
        Brain.SetState(CompanionState.Hostile);
        gameObject.layer = LayerMask.NameToLayer(Layers.Enemy);
        GetComponent<Shooter>()?.SetOwner(ProjectileOwner.Enemy);
        PartyRoster.Instance?.RemoveMember(this);

        // 휴식 중이었으면 깨움
        if (wasResting) RestSystem.Instance.ForceWake($"{name}의 배신!");
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

    private void HandleTheft()
    {
        string name = NPCStats.NPCName;
        var player = PlayerCharacter.Instance;

        // 골드 절도 → 자기 지갑에 누적 (사망 시 회수 가능)
        int stolenGold = 0;
        if (player != null && player.Stats.Gold > 0)
        {
            stolenGold = Mathf.Min(player.Stats.Gold, Mathf.RoundToInt(NPCStats.Greed * 2f));
            if (stolenGold > 0)
            {
                player.Stats.SpendGold(stolenGold);
                NPCStats.AddGold(stolenGold);
            }
        }

        int itemCount = Inventory.Slots.Inventory.Count;
        BubbleManager.ShowBubble(transform, "잘 챙겨갈게.");
        LogManager.AddLog($"{name}이(가) {stolenGold}G와 장비 {itemCount}개를 들고 도망쳤다!");

        // 적대 진영으로 전환 (다시 영입/메뉴 차단 + 동료의 자동 공격 대상)
        gameObject.layer = LayerMask.NameToLayer(Layers.Enemy);
        GetComponent<Shooter>()?.SetOwner(ProjectileOwner.Enemy);

        // Fear에 따라 도주 또는 즉시 공격
        if (NPCStats.Fear >= 60f)
            Brain.SetState(CompanionState.Fleeing);
        else
            Brain.SetState(CompanionState.Hostile);

        PartyRoster.Instance?.RemoveMember(this);
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
        NPCDropHandler.HandleDrop(NPCStats, Inventory, transform.position);
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
