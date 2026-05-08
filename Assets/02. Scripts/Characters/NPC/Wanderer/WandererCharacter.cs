using UnityEngine;

/// <summary>
/// 비동료 NPC. 평소엔 Idle. 플레이어에게 피격되면 Fear에 따라 도주 또는 적대.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class WandererCharacter : NPCCharacter
{
    public enum WandererMode { Idle, Fleeing, Hostile }

    [Header("Reaction")]
    [SerializeField] private float fleeSpeed           = 4f;
    [SerializeField] private float hostileAttackRadius = 7f;
    [SerializeField] private float hostileMoveSpeed    = 3f;
    [SerializeField] private float fleeDespawnDistance = 25f;
    [SerializeField] private float hostileFearThreshold = 60f; // Fear >= → 도주, < → 적대

    public WandererMode Mode { get; private set; } = WandererMode.Idle;

    private Rigidbody2D _rb;
    private Shooter     _shooter;
    private Vector2     _fleeDirection;

    protected override void Awake()
    {
        base.Awake();
        Team             = CharacterTeam.Neutral;
        gameObject.layer = LayerMask.NameToLayer(Layers.WandererNPC);

        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;

        _shooter = GetComponent<Shooter>(); // optional, 적대 시 사용
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
            case WandererMode.Idle:
                _rb.linearVelocity = Vector2.zero;
                break;
            case WandererMode.Fleeing:
                UpdateFleeing();
                break;
            case WandererMode.Hostile:
                UpdateHostile();
                break;
        }
    }

    // ── 피격 반응 ────────────────────────────────────────

    private void OnDamaged(float amount, GameObject attacker)
    {
        if (Mode != WandererMode.Idle) return;          // 이미 모드 결정됨
        if (attacker == null) return;
        if (attacker.layer == LayerMask.NameToLayer(Layers.Enemy)) return; // 적이 쏜 건 무시

        if (NPCStats.Fear >= hostileFearThreshold)
            EnterFleeMode(attacker);
        else
            EnterHostileMode();
    }

    private void EnterFleeMode(GameObject from)
    {
        Mode = WandererMode.Fleeing;
        _fleeDirection = ((Vector2)transform.position - (Vector2)from.transform.position).normalized;
        if (_fleeDirection.sqrMagnitude < 0.01f) _fleeDirection = Random.insideUnitCircle.normalized;

        BubbleManager.ShowBubble(transform, NPCStats.Fear >= 80f ? "살려줘!" : "왜 이러는 거야!");
        LogManager.AddLog($"{NPCStats.NPCName}이(가) 도망친다.");
    }

    private void EnterHostileMode()
    {
        Mode = WandererMode.Hostile;
        // Enemy 레이어로 전환 → 동료/플레이어가 적으로 인식, InteractionDetector도 자동 제외
        gameObject.layer = LayerMask.NameToLayer(Layers.Enemy);
        if (_shooter != null) _shooter.SetOwner(ProjectileOwner.Enemy);

        BubbleManager.ShowBubble(transform, "감히 나한테!");
        LogManager.AddLog($"{NPCStats.NPCName}이(가) 적대화되었다.");
    }

    // ── 상태별 업데이트 ──────────────────────────────────

    private void UpdateFleeing()
    {
        _rb.linearVelocity = _fleeDirection * fleeSpeed;

        var player = PlayerCharacter.Instance;
        if (player != null &&
            Vector2.Distance(transform.position, player.transform.position) > fleeDespawnDistance)
        {
            Destroy(gameObject);
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

    private void OnDied(GameObject attacker) => Destroy(gameObject);

    protected override void HandleDeath(GameObject attacker) { /* OnDied에서 처리 */ }

    /// <summary>영입 성공 시 CompanionCharacter로 전환하고 WandererCharacter를 제거.</summary>
    public CompanionCharacter ConvertToCompanion(float initialTrust)
    {
        // Idle 상태에서만 영입 가능 (도주/적대 중에는 InteractionDetector에서 자동 차단)
        var companion    = gameObject.AddComponent<CompanionCharacter>();
        var brain        = gameObject.AddComponent<CompanionBrain>();
        var relationship = gameObject.AddComponent<CompanionRelationship>();
        var reaction     = gameObject.AddComponent<CompanionReaction>();
        var shooter      = gameObject.GetComponent<Shooter>() ?? gameObject.AddComponent<Shooter>();

        shooter.SetOwner(ProjectileOwner.Companion);

        NPCStats.SetTrust(initialTrust);

        Destroy(this);
        return companion;
    }
}
