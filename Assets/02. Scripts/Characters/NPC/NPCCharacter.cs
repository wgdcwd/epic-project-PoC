using System;
using System.Collections.Generic;
using UnityEngine;

public enum NPCAllegiance { Neutral, Companion, Hostile }
public enum NPCBehavior   { Idle, Following, Waiting, Fleeing, Engaging }

/// <summary>
/// 모든 NPC를 다루는 단일 클래스.
/// - Wanderer / Companion / Hostile 분리를 폐지하고 Allegiance enum으로 관리
/// - AI / 행동 / visual / 이탈 처리를 모두 한 곳에서 처리
/// - 영입은 컴포넌트 추가/제거 없이 JoinAsCompanion 호출만으로 처리
/// </summary>
[RequireComponent(typeof(NPCStats), typeof(NPCInventory), typeof(StaminaSystem))]
[RequireComponent(typeof(Rigidbody2D), typeof(Shooter))]
[RequireComponent(typeof(CompanionRelationship), typeof(CompanionReaction))]
public sealed class NPCCharacter : CharacterBase
{
    // ── 상태 ──────────────────────────────────────────────

    public NPCAllegiance Allegiance { get; private set; } = NPCAllegiance.Neutral;
    public NPCBehavior   Behavior   { get; private set; } = NPCBehavior.Idle;

    public event Action<NPCAllegiance> OnAllegianceChanged;
    public event Action<NPCBehavior>   OnBehaviorChanged;

    // ── 컴포넌트 ──────────────────────────────────────────

    public NPCStats              Stats        { get; private set; }
    public NPCInventory          Inventory    { get; private set; }
    public CompanionRelationship Relationship { get; private set; }
    public CompanionReaction     Reaction     { get; private set; }
    public Shooter               Shooter      { get; private set; }

    private Rigidbody2D   _rb;
    private StaminaSystem _staminaSys;
    private SpriteRenderer _sr;
    private Vector3       _baseScale;
    private Color         _baseColor = Color.white;

    // ── 행동 파라미터 ────────────────────────────────────

    [Header("Companion - Following")]
    [SerializeField] private float followDistance       = 2f;
    [SerializeField] private float companionAttackRadius = 7f;
    [SerializeField] private float companionMoveSpeed   = 4.5f;
    [SerializeField] private float separationDistance   = 1.5f;
    [SerializeField] private float separationStrength   = 3.5f;

    [Header("Idle - Wander")]
    [SerializeField] private float   wanderRadius        = 3f;
    [SerializeField] private Vector2 wanderIntervalRange = new(3f, 7f);
    [SerializeField] private float   wanderSpeed         = 1.5f;

    [Header("Idle - Combat")]
    [SerializeField] private float enemyDetectRadius    = 7f;
    [SerializeField] private float idleAttackRadius     = 6f;
    [SerializeField] private float idleAttackChaseSpeed = 2.5f;

    [Header("Idle - Fear Keep Distance")]
    [SerializeField] private float fearKeepThreshold        = 70f;
    [SerializeField] private float keepAwayDistance         = 4f;
    [SerializeField] private float keepAwaySpeed            = 2.2f;
    [SerializeField] private float keepAwayWarningCooldown  = 5f;

    [Header("Idle - Aggression")]
    [SerializeField] private float aggressionDetectRange   = 6f;
    [SerializeField] private float aggressionThreshold     = 110f;
    [SerializeField] private float aggressionCheckInterval = 2f;

    [Header("Idle - Pickup")]
    [SerializeField] private float pickupSearchRadius   = 8f;
    [SerializeField] private float pickupCollectRadius  = 0.7f;
    [SerializeField] private float pickupChaseSpeed     = 2.2f;

    [Header("Idle - Chatter")]
    [SerializeField] private Vector2 chatterIntervalRange = new(8f, 18f);

    [Header("Hostile")]
    [SerializeField] private float hostileAttackRadius   = 7f;
    [SerializeField] private float hostileMoveSpeed      = 3f;
    [SerializeField] private float hostileFearThreshold  = 60f; // 피격 시 이 이상이면 도주

    [Header("Fleeing")]
    [SerializeField] private float fleeSpeed         = 4f;
    [SerializeField] private float fleeStopDistance  = 12f;

    [Header("Visual")]
    [SerializeField] private Color companionTint     = new(0.65f, 1f, 0.7f, 1f);
    [SerializeField] private Color hostileTint       = new(1f, 0.55f, 0.55f, 1f);
    [SerializeField] private float goldScalePerGold  = 0.0006f;
    [SerializeField] private float goldScaleMaxBonus = 0.6f;

    // ── 내부 상태 변수 ────────────────────────────────────

    private Vector2 _wanderTarget;
    private float   _wanderTimer;
    private float   _fearWarningTimer;
    private float   _aggressionTimer;
    private float   _chatterTimer;
    private Vector2 _fleeDirection;

    private float _passiveCheckTimer;
    private const float PassiveCheckInterval = 6f;
    private float _prevTrust   = -1f;
    private float _prevStamina = -1f;

    // ── 대사 풀 ───────────────────────────────────────────

    private static readonly string[] IdleLines = {
        "오늘도 살아남자.", "여기 위험한 곳이군.", "어디로 갈까.", "조심해야지...",
        "조용하네.", "...", "한숨 돌리자.", "이쪽은 안전한가?",
    };
    private static readonly string[] GreedSpotLines = {
        "장비가 꽤 좋아 보이는데.", "꽤 부유해 보여...", "한몫 챙길 기회군.", "저 가방 안에 뭐가 있을까."
    };
    private static readonly string[] FearSpotLines = {
        "저 사람 위험해 보여.", "조용히 피하자.", "마주치지 말자.", "위협적이군..."
    };
    private static readonly string[] AggressionLines = {
        "쉬운 먹잇감이군.", "기회다.", "약해 보이는데?", "한 방이면 끝나겠어."
    };
    private static readonly string[] FleeRecoverLines = {
        "휴, 멀어졌네.", "이제 좀 안전한가...", "도망쳤다.",
    };

    // ────────────────────────────────────────────────────
    // Lifecycle
    // ────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        Team         = CharacterTeam.Neutral;
        Stats        = GetComponent<NPCStats>();
        Inventory    = GetComponent<NPCInventory>();
        Relationship = GetComponent<CompanionRelationship>();
        Reaction     = GetComponent<CompanionReaction>();
        Shooter      = GetComponent<Shooter>();
        _rb          = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _staminaSys  = GetComponent<StaminaSystem>();
        _staminaSys.Initialize(() => Stats.Stamina, d => Stats.ModifyStamina(d));

        // visual 캐시 (한 번만)
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null)
        {
            _baseScale = _sr.transform.localScale;
            _baseColor = _sr.color;
        }

        // 시작은 Wanderer 진영
        ApplyAllegianceVisual();
        ApplyLayer();
        Shooter.SetOwner(ProjectileOwner.Companion); // wanderer가 idle에서 적 공격할 때 사용

        _wanderTimer     = UnityEngine.Random.Range(0f, wanderIntervalRange.y);
        _aggressionTimer = UnityEngine.Random.Range(1f, aggressionCheckInterval);
        _chatterTimer    = UnityEngine.Random.Range(chatterIntervalRange.x, chatterIntervalRange.y);
        _wanderTarget    = transform.position;
    }

    void OnEnable()
    {
        Stats.OnGoldChanged           += OnGoldChanged;
        Health.OnDamaged              += OnHealthDamaged;
        Health.OnDied                 += OnDied;
        Relationship.OnBetrayalTriggered   += HandleBetrayal;
        Relationship.OnFleeTriggered       += HandleFlee;
        Relationship.OnRetirementTriggered += HandleRetirement;
        Relationship.OnTheftTriggered      += HandleTheft;
        Relationship.OnBetrayalWarning     += Reaction.TriggerBetrayalWarning;
        Relationship.OnRetirementWarning   += Reaction.TriggerRetirementWarning;
        Stats.OnTrustChanged          += OnTrustChanged;
        Stats.OnStaminaChanged        += OnStaminaChanged;

        UpdateGoldScale();
    }

    void OnDisable()
    {
        Stats.OnGoldChanged           -= OnGoldChanged;
        Health.OnDamaged              -= OnHealthDamaged;
        Health.OnDied                 -= OnDied;
        Relationship.OnBetrayalTriggered   -= HandleBetrayal;
        Relationship.OnFleeTriggered       -= HandleFlee;
        Relationship.OnRetirementTriggered -= HandleRetirement;
        Relationship.OnTheftTriggered      -= HandleTheft;
        Relationship.OnBetrayalWarning     -= Reaction.TriggerBetrayalWarning;
        Relationship.OnRetirementWarning   -= Reaction.TriggerRetirementWarning;
        Stats.OnTrustChanged          -= OnTrustChanged;
        Stats.OnStaminaChanged        -= OnStaminaChanged;
    }

    void Start()
    {
        _prevTrust   = Stats.Trust;
        _prevStamina = Stats.Stamina;
        _passiveCheckTimer = UnityEngine.Random.Range(2f, PassiveCheckInterval);
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        if (Allegiance != NPCAllegiance.Companion) return;

        _passiveCheckTimer -= Time.deltaTime;
        if (_passiveCheckTimer <= 0f)
        {
            _passiveCheckTimer = PassiveCheckInterval;
            Relationship.CheckImmediateThresholds();
        }
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        // 휴식 중엔 동료(Following/Waiting)만 정지. 적대/도주는 정상 동작.
        if (RestSystem.Instance != null && RestSystem.Instance.IsResting &&
            Allegiance == NPCAllegiance.Companion &&
            (Behavior == NPCBehavior.Following || Behavior == NPCBehavior.Waiting))
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (Allegiance)
        {
            case NPCAllegiance.Neutral:   TickNeutral();   break;
            case NPCAllegiance.Companion: TickCompanion(); break;
            case NPCAllegiance.Hostile:   TickHostile();   break;
        }
    }

    // ────────────────────────────────────────────────────
    // Allegiance 전환
    // ────────────────────────────────────────────────────

    public void JoinAsCompanion(float initialTrust)
    {
        if (Allegiance == NPCAllegiance.Companion) return;

        Stats.SetTrust(initialTrust);
        SetAllegiance(NPCAllegiance.Companion);
        SetBehavior(NPCBehavior.Following);
        PartyRoster.Instance?.AddMember(this);
    }

    private void BecomeHostile(string bubbleLine)
    {
        if (Allegiance == NPCAllegiance.Companion)
            PartyRoster.Instance?.RemoveMember(this);

        SetAllegiance(NPCAllegiance.Hostile);
        SetBehavior(NPCBehavior.Engaging);
        if (!string.IsNullOrEmpty(bubbleLine))
            BubbleManager.ShowBubble(transform, bubbleLine);
    }

    private void StartFleeing(GameObject from)
    {
        SetBehavior(NPCBehavior.Fleeing);
        _fleeDirection = from != null
            ? ((Vector2)transform.position - (Vector2)from.transform.position).normalized
            : UnityEngine.Random.insideUnitCircle.normalized;
        if (_fleeDirection.sqrMagnitude < 0.01f)
            _fleeDirection = UnityEngine.Random.insideUnitCircle.normalized;
    }

    private void SetAllegiance(NPCAllegiance newValue)
    {
        if (Allegiance == newValue) return;
        Allegiance = newValue;
        ApplyLayer();
        ApplyAllegianceVisual();
        UpdateShooterOwner();
        OnAllegianceChanged?.Invoke(newValue);
    }

    public void SetBehavior(NPCBehavior newValue)
    {
        if (Behavior == newValue) return;
        Behavior = newValue;
        Debug.Log($"[NPC] {Stats.NPCName} : Behavior → {newValue}");
        OnBehaviorChanged?.Invoke(newValue);
    }

    private void ApplyLayer()
    {
        gameObject.layer = Allegiance switch
        {
            NPCAllegiance.Companion => LayerMask.NameToLayer(Layers.Companion),
            NPCAllegiance.Hostile   => LayerMask.NameToLayer(Layers.Enemy),
            _                       => LayerMask.NameToLayer(Layers.WandererNPC),
        };
        Team = Allegiance switch
        {
            NPCAllegiance.Companion => CharacterTeam.Companion,
            NPCAllegiance.Hostile   => CharacterTeam.Enemy,
            _                       => CharacterTeam.Neutral,
        };
    }

    private void ApplyAllegianceVisual()
    {
        if (_sr == null) return;
        _sr.color = Allegiance switch
        {
            NPCAllegiance.Companion => companionTint,
            NPCAllegiance.Hostile   => hostileTint,
            _                       => _baseColor,
        };
    }

    private void UpdateShooterOwner()
    {
        Shooter.SetOwner(Allegiance == NPCAllegiance.Hostile
            ? ProjectileOwner.Enemy
            : ProjectileOwner.Companion);
    }

    // ────────────────────────────────────────────────────
    // Visual - Gold Scale
    // ────────────────────────────────────────────────────

    private void OnGoldChanged() => UpdateGoldScale();

    private void UpdateGoldScale()
    {
        if (_sr == null) return;
        float bonus = Mathf.Min(goldScaleMaxBonus, Stats.Gold * goldScalePerGold);
        _sr.transform.localScale = _baseScale * (1f + bonus);
    }

    // ────────────────────────────────────────────────────
    // Behavior 분기 - Neutral
    // ────────────────────────────────────────────────────

    private void TickNeutral()
    {
        switch (Behavior)
        {
            case NPCBehavior.Fleeing: TickFleeing(); break;
            default:                  TickNeutralIdle(); break;
        }
    }

    private void TickNeutralIdle()
    {
        TickTimers();

        // 1) 적 사냥
        Transform enemy = FindNearestEnemyInRange(enemyDetectRadius);
        if (enemy != null) { EngageEnemy(enemy); return; }

        // 2) 선제공격 판정
        if (_aggressionTimer <= 0f)
        {
            _aggressionTimer = aggressionCheckInterval;
            if (TryPreemptiveStrike()) return;
        }

        // 3) 픽업 줍기
        if (TryCollectPickup()) return;

        // 4) Fear 거리 유지
        if (Stats.Fear >= fearKeepThreshold && TryKeepAwayFromPlayer()) return;

        // 5) Wander + 가끔 대사
        Wander();
        TryIdleChatter();
    }

    // ────────────────────────────────────────────────────
    // Behavior 분기 - Companion
    // ────────────────────────────────────────────────────

    private void TickCompanion()
    {
        switch (Behavior)
        {
            case NPCBehavior.Waiting:  _rb.linearVelocity = Vector2.zero; break;
            case NPCBehavior.Fleeing:  TickFleeing(); break;
            default:                   TickCompanionFollowing(); break;
        }
    }

    private void TickCompanionFollowing()
    {
        var player = PlayerCharacter.Instance;
        if (player == null) return;

        Vector2 separation = GetSeparationForce() * separationStrength;

        Transform enemy = FindNearestEnemyInRange(companionAttackRadius);
        if (enemy != null && Vector2.Distance(transform.position, enemy.position) <= companionAttackRadius)
        {
            _rb.linearVelocity = separation;
            Shooter.TryFireAt(enemy.position);
            return;
        }

        float playerDist = Vector2.Distance(transform.position, player.transform.position);
        Vector2 toPlayer = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;

        _rb.linearVelocity = playerDist > followDistance
            ? toPlayer * companionMoveSpeed + separation
            : separation;
    }

    private Vector2 GetSeparationForce()
    {
        if (PartyRoster.Instance == null) return Vector2.zero;
        Vector2 result = Vector2.zero;
        foreach (var other in PartyRoster.Instance.Members)
        {
            if (other == null || other.gameObject == gameObject) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)other.transform.position;
            float dist = diff.magnitude;
            if (dist <= 0f || dist >= separationDistance) continue;
            float weight = (separationDistance - dist) / separationDistance;
            result += diff.normalized * weight;
        }
        return result;
    }

    // ────────────────────────────────────────────────────
    // Behavior 분기 - Hostile
    // ────────────────────────────────────────────────────

    private void TickHostile()
    {
        if (Behavior == NPCBehavior.Fleeing) { TickFleeing(); return; }

        var player = PlayerCharacter.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist <= hostileAttackRadius)
        {
            _rb.linearVelocity = Vector2.zero;
            Shooter.TryFireAt(player.transform.position);
        }
        else
        {
            Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * hostileMoveSpeed;
        }
    }

    // ────────────────────────────────────────────────────
    // Behavior - Fleeing 공통
    // ────────────────────────────────────────────────────

    private void TickFleeing()
    {
        var player = PlayerCharacter.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        if (dist < fleeStopDistance)
        {
            _fleeDirection = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;
            if (_fleeDirection.sqrMagnitude < 0.01f) _fleeDirection = UnityEngine.Random.insideUnitCircle.normalized;
            _rb.linearVelocity = _fleeDirection * fleeSpeed;
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
            // Neutral은 충분히 멀어졌으면 idle 복귀
            if (Allegiance == NPCAllegiance.Neutral)
            {
                SetBehavior(NPCBehavior.Idle);
                BubbleManager.ShowBubble(transform, PickRandom(FleeRecoverLines));
            }
        }
    }

    // ────────────────────────────────────────────────────
    // Idle helpers
    // ────────────────────────────────────────────────────

    private void TickTimers()
    {
        float dt = Time.fixedDeltaTime;
        if (_fearWarningTimer > 0f) _fearWarningTimer -= dt;
        if (_aggressionTimer  > 0f) _aggressionTimer  -= dt;
        if (_chatterTimer     > 0f) _chatterTimer     -= dt;
    }

    private Transform FindNearestEnemyInRange(float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, radius, LayerMask.GetMask(Layers.Enemy));
        Transform best = null;
        float minD = float.MaxValue;
        foreach (var h in hits)
        {
            if (h == null || h.gameObject == gameObject) continue;
            var hc = h.GetComponent<HealthComponent>();
            if (hc == null || !hc.IsAlive) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < minD) { minD = d; best = h.transform; }
        }
        return best;
    }

    private void EngageEnemy(Transform enemy)
    {
        float dist = Vector2.Distance(transform.position, enemy.position);
        if (dist <= idleAttackRadius)
        {
            _rb.linearVelocity = Vector2.zero;
            Shooter.TryFireAt(enemy.position);
        }
        else
        {
            Vector2 dir = ((Vector2)enemy.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * idleAttackChaseSpeed;
        }
    }

    private bool TryPreemptiveStrike()
    {
        var player = PlayerCharacter.Instance;
        if (player == null || !player.Health.IsAlive) return false;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist > aggressionDetectRange) return false;

        float weakBonus = 0f;
        if (player.Health.HPRatio < 0.4f) weakBonus += 30f;
        else if (player.Health.HPRatio < 0.7f) weakBonus += 10f;
        if (player.Stats.Stamina < 30f) weakBonus += 10f;

        float score =
            (100f - Stats.Morality) + Stats.Greed + weakBonus
            - Stats.Fear - player.Stats.FinalThreat * 0.3f;

        if (score < aggressionThreshold) return false;

        BecomeHostile(PickRandom(AggressionLines));
        LogManager.AddLog($"{Stats.NPCName}이(가) 선제공격을 시작했다! (공격성 {score:F0})");
        return true;
    }

    private bool TryKeepAwayFromPlayer()
    {
        var player = PlayerCharacter.Instance;
        if (player == null || !player.Health.IsAlive) return false;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist >= keepAwayDistance) return false;

        Vector2 awayDir = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;
        if (awayDir.sqrMagnitude < 0.01f) awayDir = UnityEngine.Random.insideUnitCircle.normalized;
        _rb.linearVelocity = awayDir * keepAwaySpeed;

        if (_fearWarningTimer <= 0f)
        {
            _fearWarningTimer = keepAwayWarningCooldown;
            BubbleManager.ShowBubble(transform,
                dist < keepAwayDistance * 0.5f ? "더 가까이 오지 마!" : "거리 좀 둬.");
        }
        return true;
    }

    private bool TryCollectPickup()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pickupSearchRadius);
        PickupBase nearest = null;
        float minD = float.MaxValue;

        foreach (var h in hits)
        {
            if (h == null) continue;
            var pickup = h.GetComponent<PickupBase>();
            if (pickup == null || pickup.IsPicked) continue;
            float d = Vector2.Distance(transform.position, pickup.transform.position);
            if (d < minD) { minD = d; nearest = pickup; }
        }
        if (nearest == null) return false;

        if (minD <= pickupCollectRadius)
        {
            switch (nearest)
            {
                case GoldPickup gp:      gp.TryPickupBy(Stats);     break;
                case EquipmentPickup ep: ep.TryPickupBy(Inventory); break;
            }
            return true;
        }

        Vector2 dir = ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * pickupChaseSpeed;
        return true;
    }

    private void Wander()
    {
        _wanderTimer -= Time.fixedDeltaTime;
        if (_wanderTimer <= 0f)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadius;
            _wanderTarget = (Vector2)transform.position + offset;
            _wanderTimer  = UnityEngine.Random.Range(wanderIntervalRange.x, wanderIntervalRange.y);
        }
        Vector2 toTarget = _wanderTarget - (Vector2)transform.position;
        _rb.linearVelocity = toTarget.sqrMagnitude > 0.04f
            ? toTarget.normalized * wanderSpeed
            : Vector2.zero;
    }

    private void TryIdleChatter()
    {
        if (_chatterTimer > 0f) return;
        _chatterTimer = UnityEngine.Random.Range(chatterIntervalRange.x, chatterIntervalRange.y);

        var player = PlayerCharacter.Instance;
        if (player != null && player.Health.IsAlive &&
            Vector2.Distance(transform.position, player.transform.position) < 8f)
        {
            if (Stats.Greed >= 70f && player.Stats.FinalWealth >= 30f)
            { BubbleManager.ShowBubble(transform, PickRandom(GreedSpotLines)); return; }
            if (Stats.Fear >= 70f && player.Stats.FinalThreat >= 30f)
            { BubbleManager.ShowBubble(transform, PickRandom(FearSpotLines));  return; }
        }
        BubbleManager.ShowBubble(transform, PickRandom(IdleLines));
    }

    // ────────────────────────────────────────────────────
    // 피격 / 사망 처리
    // ────────────────────────────────────────────────────

    private void OnHealthDamaged(float amount, GameObject attacker)
    {
        // 휴식 중인 동료는 즉시 깨움
        if (Allegiance == NPCAllegiance.Companion &&
            RestSystem.Instance != null && RestSystem.Instance.IsResting)
        {
            RestSystem.Instance.ForceWake($"{Stats.NPCName}이(가) 공격받았다!");
        }

        // Neutral 상태에서 플레이어에게 피격 → Fear에 따라 도주/적대화
        if (Allegiance == NPCAllegiance.Neutral && Behavior == NPCBehavior.Idle)
        {
            if (attacker == null) return;
            int playerLayer = LayerMask.NameToLayer(Layers.Player);
            int companionLayer = LayerMask.NameToLayer(Layers.Companion);
            if (attacker.layer != playerLayer && attacker.layer != companionLayer) return;

            if (Stats.Fear >= hostileFearThreshold)
            {
                StartFleeing(attacker);
                BubbleManager.ShowBubble(transform, Stats.Fear >= 80f ? "살려줘!" : "왜 이러는 거야!");
                LogManager.AddLog($"{Stats.NPCName}이(가) 도망친다.");
            }
            else
            {
                BecomeHostile("감히 나한테!");
                LogManager.AddLog($"{Stats.NPCName}이(가) 적대화되었다.");
            }
        }
    }

    private void OnDied(GameObject attacker)
    {
        if (Allegiance == NPCAllegiance.Companion)
            PartyRoster.Instance?.RemoveMember(this);

        NPCDropHandler.HandleDrop(Stats, Inventory, transform.position);
        Destroy(gameObject);
    }

    protected override void HandleDeath(GameObject attacker) { /* OnDied에서 처리 */ }

    // ────────────────────────────────────────────────────
    // Companion 이탈 처리
    // ────────────────────────────────────────────────────

    private void HandleBetrayal()
    {
        bool wasResting = RestSystem.Instance != null && RestSystem.Instance.IsResting;
        string name = Stats.NPCName;

        BubbleManager.ShowBubble(transform, "미안하지만... 이게 낫겠어.");
        LogManager.AddLog($"{name}이(가) 배신했다!");

        if (wasResting && PartyRoster.Instance != null)
        {
            foreach (var c in PartyRoster.Instance.Members)
            {
                if (c == null || c == this) continue;
                BubbleManager.ShowBubble(c.transform, $"{name}이(가) 배신했어!");
            }
        }

        BecomeHostile(null);

        if (wasResting) RestSystem.Instance.ForceWake($"{name}의 배신!");
    }

    private void HandleFlee()
    {
        BubbleManager.ShowBubble(transform, "못 하겠어. 미안.");
        LogManager.AddLog($"{Stats.NPCName}이(가) 전투 중 도망쳤다.");
        if (Allegiance == NPCAllegiance.Companion)
            PartyRoster.Instance?.RemoveMember(this);
        // 진영은 Neutral로 (Layer Wanderer로 복귀 X — 도주 중인 NPC는 적대로 가지 않고 그냥 fleeing)
        SetAllegiance(NPCAllegiance.Neutral);
        StartFleeing(PlayerCharacter.Instance != null ? PlayerCharacter.Instance.gameObject : null);
    }

    private void HandleRetirement()
    {
        string name = Stats.NPCName;
        if (Stats.Trust >= 50f)
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

    private void HandleTheft()
    {
        string name = Stats.NPCName;
        var player = PlayerCharacter.Instance;

        int stolenGold = 0;
        if (player != null && player.Stats.Gold > 0)
        {
            stolenGold = Mathf.Min(player.Stats.Gold, Mathf.RoundToInt(Stats.Greed * 2f));
            if (stolenGold > 0)
            {
                player.Stats.SpendGold(stolenGold);
                Stats.AddGold(stolenGold);
            }
        }

        int itemCount = Inventory.Slots.Inventory.Count;
        BubbleManager.ShowBubble(transform, "잘 챙겨갈게.");
        LogManager.AddLog($"{name}이(가) {stolenGold}G와 장비 {itemCount}개를 들고 도망쳤다!");

        // 적대 진영으로 전환 (다시 영입/메뉴 차단)
        if (Allegiance == NPCAllegiance.Companion)
            PartyRoster.Instance?.RemoveMember(this);

        SetAllegiance(NPCAllegiance.Hostile);

        // Fear에 따라 즉시 공격 또는 도주
        if (Stats.Fear >= 60f)
            StartFleeing(player != null ? player.gameObject : null);
        else
            SetBehavior(NPCBehavior.Engaging);
    }

    // ────────────────────────────────────────────────────
    // 반응 트리거 (Trust/Stamina 변화)
    // ────────────────────────────────────────────────────

    private void OnTrustChanged(float newTrust)
    {
        if (Allegiance != NPCAllegiance.Companion) return;

        if ((_prevTrust > 40f && newTrust <= 40f) ||
            (_prevTrust > 25f && newTrust <= 25f))
        {
            Reaction.TriggerLowTrust();
        }
        _prevTrust = newTrust;

        var ps = PlayerCharacter.Instance?.Stats;
        if (ps != null)
        {
            Reaction.TriggerGreedWealthReaction(ps.FinalWealth);
            Reaction.TriggerFearThreatReaction(ps.FinalThreat);
        }
    }

    private void OnStaminaChanged()
    {
        if (Allegiance != NPCAllegiance.Companion) return;

        float stamina = Stats.Stamina;
        if ((_prevStamina > 30f && stamina <= 30f) ||
            (_prevStamina > 10f && stamina <= 10f))
        {
            Reaction.TriggerLowStamina();
        }
        _prevStamina = stamina;
    }

    // ────────────────────────────────────────────────────
    // Companion 외부 명령 (UI에서 호출)
    // ────────────────────────────────────────────────────

    public void ToggleWaiting()
    {
        if (Allegiance != NPCAllegiance.Companion) return;
        SetBehavior(Behavior == NPCBehavior.Waiting
            ? NPCBehavior.Following
            : NPCBehavior.Waiting);
    }

    private static string PickRandom(string[] arr)
        => arr[UnityEngine.Random.Range(0, arr.Length)];
}
