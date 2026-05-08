using UnityEngine;

/// <summary>
/// 비동료 NPC.
/// - Idle: wander + 적 사냥 + Fear 거리유지 + 성격에 따른 선제공격 + 다양한 대사
/// - Fleeing: 적당히 멀어지면 다시 Idle
/// - Hostile: 끝까지 추격
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class WandererCharacter : NPCCharacter
{
    public enum WandererMode { Idle, Fleeing, Hostile }

    [Header("Reaction")]
    [SerializeField] private float fleeSpeed            = 4f;
    [SerializeField] private float hostileAttackRadius  = 7f;
    [SerializeField] private float hostileMoveSpeed     = 3f;
    [SerializeField] private float fleeStopDistance     = 12f; // 이만큼 멀어지면 Idle 복귀
    [SerializeField] private float fleeDespawnDistance  = 30f; // 이만큼 더 멀어지면 사라짐
    [SerializeField] private float hostileFearThreshold = 60f;

    [Header("Idle - Wandering")]
    [SerializeField] private float   wanderRadius          = 3f;
    [SerializeField] private Vector2 wanderIntervalRange   = new(3f, 7f);
    [SerializeField] private float   wanderSpeed           = 1.5f;

    [Header("Idle - Combat")]
    [SerializeField] private float enemyDetectRadius     = 7f;
    [SerializeField] private float idleAttackRadius      = 6f;
    [SerializeField] private float idleAttackChaseSpeed  = 2.5f;

    [Header("Fear - Keep Distance")]
    [SerializeField] private float fearKeepThreshold        = 70f;
    [SerializeField] private float keepAwayDistance         = 4f;
    [SerializeField] private float keepAwaySpeed            = 2.2f;
    [SerializeField] private float keepAwayWarningCooldown  = 5f;

    [Header("Aggression - Preemptive Strike")]
    [Tooltip("이 거리 안에서 선제공격 점수 평가")]
    [SerializeField] private float aggressionDetectRange = 6f;
    [SerializeField] private float aggressionThreshold   = 110f;
    [SerializeField] private float aggressionCheckInterval = 2f;

    [Header("Idle - Pickup")]
    [Tooltip("이 거리 안의 골드/장비 픽업을 발견하면 주우러 감")]
    [SerializeField] private float pickupSearchRadius  = 8f;
    [SerializeField] private float pickupCollectRadius = 0.7f;
    [SerializeField] private float pickupChaseSpeed    = 2.2f;

    [Header("Idle - Chatter")]
    [Tooltip("idle wander 중 평균 몇 초마다 대사")]
    [SerializeField] private Vector2 chatterIntervalRange = new(8f, 18f);

    public WandererMode Mode { get; private set; } = WandererMode.Idle;

    private Rigidbody2D _rb;
    private Shooter     _shooter;
    private Vector2     _fleeDirection;

    // Idle 상태
    private Vector2 _wanderTarget;
    private float   _wanderTimer;
    private float   _fearWarningTimer;
    private float   _aggressionTimer;
    private float   _chatterTimer;

    // ── 대사 풀 ───────────────────────────────────────────

    private static readonly string[] IdleLines = {
        "오늘도 살아남자.",
        "여기 위험한 곳이군.",
        "어디로 갈까.",
        "조심해야지...",
        "조용하네.",
        "...",
        "한숨 돌리자.",
        "이쪽은 안전한가?",
    };

    private static readonly string[] GreedSpotLines = {
        "장비가 꽤 좋아 보이는데.",
        "꽤 부유해 보여...",
        "한몫 챙길 기회군.",
        "저 가방 안에 뭐가 있을까."
    };

    private static readonly string[] FearSpotLines = {
        "저 사람 위험해 보여.",
        "조용히 피하자.",
        "마주치지 말자.",
        "위협적이군..."
    };

    private static readonly string[] AggressionLines = {
        "쉬운 먹잇감이군.",
        "기회다.",
        "약해 보이는데?",
        "한 방이면 끝나겠어."
    };

    private static readonly string[] FleeRecoverLines = {
        "휴, 멀어졌네.",
        "이제 좀 안전한가...",
        "도망쳤다.",
    };

    // ── 초기화 ────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        Team             = CharacterTeam.Neutral;
        gameObject.layer = LayerMask.NameToLayer(Layers.WandererNPC);

        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;

        _shooter = GetComponent<Shooter>();
        if (_shooter != null) _shooter.SetOwner(ProjectileOwner.Companion);

        _wanderTimer     = UnityEngine.Random.Range(0f, wanderIntervalRange.y);
        _aggressionTimer = UnityEngine.Random.Range(1f, aggressionCheckInterval);
        _chatterTimer    = UnityEngine.Random.Range(chatterIntervalRange.x, chatterIntervalRange.y);
        _wanderTarget    = transform.position;
    }

    void OnEnable()
    {
        Health.OnDied    += OnDied;
        Health.OnDamaged += OnDamaged;
    }

    void OnDisable()
    {
        Health.OnDied    -= OnDied;
        Health.OnDamaged -= OnDamaged;
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        switch (Mode)
        {
            case WandererMode.Idle:     UpdateIdle();     break;
            case WandererMode.Fleeing:  UpdateFleeing();  break;
            case WandererMode.Hostile:  UpdateHostile();  break;
        }
    }

    // ── Idle ─────────────────────────────────────────────

    private void UpdateIdle()
    {
        TickTimers();

        // 1) 근처 적 사냥
        Transform enemy = FindNearestEnemyInRange(enemyDetectRadius);
        if (enemy != null) { EngageEnemy(enemy); return; }

        // 2) 근처 픽업 줍기
        if (TryCollectPickup()) return;

        // 3) 성격에 따른 선제공격 판정
        if (_aggressionTimer <= 0f)
        {
            _aggressionTimer = aggressionCheckInterval;
            if (TryPreemptiveStrike()) return;
        }

        // 4) Fear 높으면 거리 유지
        if (NPCStats.Fear >= fearKeepThreshold && TryKeepAwayFromPlayer())
            return;

        // 5) Wander + 가끔 대사
        Wander();
        TryIdleChatter();
    }

    /// <summary>주변에 골드/장비 픽업이 있으면 가서 줍는다.</summary>
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
            // 가까이 도달 → 픽업 시도
            switch (nearest)
            {
                case GoldPickup gp:      gp.TryPickupBy(NPCStats);  break;
                case EquipmentPickup ep: ep.TryPickupBy(Inventory); break;
            }
            return true;
        }

        // 픽업 향해 이동
        Vector2 dir = ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * pickupChaseSpeed;
        return true;
    }

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
            _shooter?.TryFireAt(enemy.position);
        }
        else
        {
            Vector2 dir = ((Vector2)enemy.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * idleAttackChaseSpeed;
        }
    }

    /// <summary>성격 + 플레이어 약화 정도로 선제공격 결정.</summary>
    private bool TryPreemptiveStrike()
    {
        var player = PlayerCharacter.Instance;
        if (player == null || !player.Health.IsAlive) return false;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist > aggressionDetectRange) return false;

        // 성격 + 플레이어 약화 보정
        float weakBonus = 0f;
        if (player.Health.HPRatio < 0.4f) weakBonus += 30f;
        else if (player.Health.HPRatio < 0.7f) weakBonus += 10f;
        if (player.Stats.Stamina < 30f) weakBonus += 10f;

        float score =
            (100f - NPCStats.Morality)
            + NPCStats.Greed
            + weakBonus
            - NPCStats.Fear
            - player.Stats.FinalThreat * 0.3f;

        if (score < aggressionThreshold) return false;

        EnterHostileMode(PickRandom(AggressionLines));
        LogManager.AddLog($"{NPCStats.NPCName}이(가) 선제공격을 시작했다! (공격성 {score:F0})");
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
            string line = dist < keepAwayDistance * 0.5f ? "더 가까이 오지 마!" : "거리 좀 둬.";
            BubbleManager.ShowBubble(transform, line);
        }
        return true;
    }

    private void Wander()
    {
        _wanderTimer -= Time.fixedDeltaTime;
        if (_wanderTimer <= 0f)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadius;
            _wanderTarget  = (Vector2)transform.position + offset;
            _wanderTimer   = UnityEngine.Random.Range(wanderIntervalRange.x, wanderIntervalRange.y);
        }

        Vector2 toTarget = _wanderTarget - (Vector2)transform.position;
        if (toTarget.sqrMagnitude > 0.04f)
            _rb.linearVelocity = toTarget.normalized * wanderSpeed;
        else
            _rb.linearVelocity = Vector2.zero;
    }

    /// <summary>idle 도중 가끔 한마디. 플레이어가 가시 거리 안이면 성향에 맞는 대사도.</summary>
    private void TryIdleChatter()
    {
        if (_chatterTimer > 0f) return;
        _chatterTimer = UnityEngine.Random.Range(chatterIntervalRange.x, chatterIntervalRange.y);

        var player = PlayerCharacter.Instance;
        if (player != null && player.Health.IsAlive)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);

            // 플레이어가 보이는 거리 (8) 안: 성향 반응 우선
            if (dist < 8f)
            {
                if (NPCStats.Greed >= 70f && player.Stats.FinalWealth >= 30f)
                {
                    BubbleManager.ShowBubble(transform, PickRandom(GreedSpotLines));
                    return;
                }
                if (NPCStats.Fear >= 70f && player.Stats.FinalThreat >= 30f)
                {
                    BubbleManager.ShowBubble(transform, PickRandom(FearSpotLines));
                    return;
                }
            }
        }

        // 일반 idle 대사
        BubbleManager.ShowBubble(transform, PickRandom(IdleLines));
    }

    // ── Damage Reaction ───────────────────────────────────

    private void OnDamaged(float amount, GameObject attacker)
    {
        if (Mode != WandererMode.Idle) return;
        if (attacker == null) return;
        if (attacker.layer == LayerMask.NameToLayer(Layers.Enemy)) return;

        if (NPCStats.Fear >= hostileFearThreshold)
            EnterFleeMode(attacker);
        else
            EnterHostileMode("감히 나한테!");
    }

    private void EnterFleeMode(GameObject from)
    {
        Mode = WandererMode.Fleeing;
        _fleeDirection = ((Vector2)transform.position - (Vector2)from.transform.position).normalized;
        if (_fleeDirection.sqrMagnitude < 0.01f) _fleeDirection = UnityEngine.Random.insideUnitCircle.normalized;

        BubbleManager.ShowBubble(transform, NPCStats.Fear >= 80f ? "살려줘!" : "왜 이러는 거야!");
        LogManager.AddLog($"{NPCStats.NPCName}이(가) 도망친다.");
    }

    private void EnterHostileMode(string bubbleLine)
    {
        Mode = WandererMode.Hostile;
        gameObject.layer = LayerMask.NameToLayer(Layers.Enemy);
        if (_shooter != null) _shooter.SetOwner(ProjectileOwner.Enemy);

        BubbleManager.ShowBubble(transform, bubbleLine);
        LogManager.AddLog($"{NPCStats.NPCName}이(가) 적대화되었다.");
    }

    // ── Mode Updates ──────────────────────────────────────

    private void UpdateFleeing()
    {
        _rb.linearVelocity = _fleeDirection * fleeSpeed;

        var player = PlayerCharacter.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        if (dist >= fleeDespawnDistance)
        {
            Destroy(gameObject);
            return;
        }

        if (dist >= fleeStopDistance)
        {
            // 충분히 멀어짐 → idle 복귀
            Mode = WandererMode.Idle;
            _rb.linearVelocity = Vector2.zero;
            BubbleManager.ShowBubble(transform, PickRandom(FleeRecoverLines));
        }
    }

    private void UpdateHostile()
    {
        var player = PlayerCharacter.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist <= hostileAttackRadius)
        {
            _rb.linearVelocity = Vector2.zero;
            _shooter?.TryFireAt(player.transform.position);
        }
        else
        {
            Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * hostileMoveSpeed;
        }
    }

    // ── 사망 / 영입 ──────────────────────────────────────

    private void OnDied(GameObject attacker)
    {
        NPCDropHandler.HandleDrop(NPCStats, Inventory, transform.position);
        Destroy(gameObject);
    }

    protected override void HandleDeath(GameObject attacker) { }

    public CompanionCharacter ConvertToCompanion(float initialTrust)
    {
        var companion = gameObject.AddComponent<CompanionCharacter>();

        var shooter = gameObject.GetComponent<Shooter>() ?? gameObject.AddComponent<Shooter>();
        shooter.SetOwner(ProjectileOwner.Companion);

        NPCStats.SetTrust(initialTrust);

        Destroy(this);
        return companion;
    }

    private static string PickRandom(string[] arr)
        => arr[UnityEngine.Random.Range(0, arr.Length)];
}
