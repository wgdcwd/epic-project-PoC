using UnityEngine;

public enum CompanionState { Following, Waiting, Fleeing, Hostile }

/// <summary>
/// 동료 AI: 플레이어 추종, 적 자동 공격, 도주, 배신(적대) 상태 관리.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Shooter))]
public sealed class CompanionBrain : MonoBehaviour
{
    [SerializeField] private float followDistance     = 2f;   // 플레이어와 유지할 거리
    [SerializeField] private float attackRadius       = 7f;
    [SerializeField] private float moveSpeed          = 4.5f;

    [Header("Separation (다른 동료와 거리 유지)")]
    [SerializeField] private float separationDistance = 1.5f;
    [SerializeField] private float separationStrength = 3.5f;

    public CompanionState State { get; private set; } = CompanionState.Following;

    private Rigidbody2D _rb;
    private Shooter     _shooter;
    private NPCStats    _stats;

    // 도주 목표 (화면 끝 방향)
    private Vector2 _fleeDirection;

    void Awake()
    {
        _rb      = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;
        _shooter = GetComponent<Shooter>();
        _stats   = GetComponent<NPCStats>();
    }

    void FixedUpdate()
    {
        if (!GameManager.Instance.IsPlaying) return;

        switch (State)
        {
            case CompanionState.Following: UpdateFollowing(); break;
            case CompanionState.Waiting:   _rb.linearVelocity = Vector2.zero; break;
            case CompanionState.Fleeing:   UpdateFleeing(); break;
            case CompanionState.Hostile:   UpdateHostile(); break;
        }
    }

    // ── 상태 전환 ────────────────────────────────────────

    public event System.Action<CompanionState> OnStateChanged;

    public void SetState(CompanionState newState)
    {
        if (State == newState) return;
        Debug.Log($"[CompanionBrain] {name} : {State} → {newState}");
        State = newState;
        OnStateChanged?.Invoke(newState);

        if (newState == CompanionState.Fleeing)
        {
            // 플레이어 반대 방향으로 도주
            var player = PlayerCharacter.Instance;
            _fleeDirection = player != null
                ? ((Vector2)transform.position - (Vector2)player.transform.position).normalized
                : Random.insideUnitCircle.normalized;
        }

        if (newState == CompanionState.Hostile)
        {
            _shooter.SetOwner(ProjectileOwner.Companion); // 배신 시 레이어 변경은 CompanionCharacter에서
        }
    }

    // ── 상태별 업데이트 ──────────────────────────────────

    private void UpdateFollowing()
    {
        var player = PlayerCharacter.Instance;
        if (player == null) return;

        Vector2 separation = GetSeparationForce() * separationStrength;

        // 주변 적 탐색
        Transform enemy = FindNearestEnemy();
        if (enemy != null && Vector2.Distance(transform.position, enemy.position) <= attackRadius)
        {
            // 공격 중에도 분리 force만큼은 적용 (밀착 방지)
            _rb.linearVelocity = separation;
            _shooter.TryFireAt(enemy.position);
            return;
        }

        // 플레이어 추종 + 분리
        float playerDist = Vector2.Distance(transform.position, player.transform.position);
        Vector2 toPlayer = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;

        if (playerDist > followDistance)
            _rb.linearVelocity = toPlayer * moveSpeed + separation;
        else
            _rb.linearVelocity = separation; // 가까울 땐 분리만 적용
    }

    /// <summary>다른 동료와 separationDistance 안에 있으면 멀어지는 방향의 정규화된 벡터 합.</summary>
    private Vector2 GetSeparationForce()
    {
        if (PartyRoster.Instance == null) return Vector2.zero;

        Vector2 result = Vector2.zero;
        foreach (var other in PartyRoster.Instance.Members)
        {
            if (other == null || other.gameObject == gameObject) continue;

            Vector2 diff   = (Vector2)transform.position - (Vector2)other.transform.position;
            float   dist   = diff.magnitude;
            if (dist <= 0f || dist >= separationDistance) continue;

            // 가까울수록 강하게 밀어냄 (선형 감쇠)
            float weight = (separationDistance - dist) / separationDistance;
            result += diff.normalized * weight;
        }
        return result;
    }

    private void UpdateFleeing()
    {
        _rb.linearVelocity = _fleeDirection * moveSpeed * 1.2f;

        // 일정 거리 이상 멀어지면 씬에서 제거
        if (PlayerCharacter.Instance != null &&
            Vector2.Distance(transform.position, PlayerCharacter.Instance.transform.position) > 30f)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateHostile()
    {
        // 배신 상태: 플레이어를 공격
        var player = PlayerCharacter.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist <= attackRadius)
        {
            _rb.linearVelocity = Vector2.zero;
            _shooter.TryFireAt(player.transform.position);
        }
        else
        {
            Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;
        }
    }

    private Transform FindNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRadius,
                                                        LayerMask.GetMask(Layers.Enemy));
        float minD = float.MaxValue;
        Transform best = null;
        foreach (var h in hits)
        {
            if (!h.GetComponent<HealthComponent>()?.IsAlive ?? true) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < minD) { minD = d; best = h.transform; }
        }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
