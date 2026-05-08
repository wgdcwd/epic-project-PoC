using UnityEngine;

/// <summary>
/// 적 AI. 처음엔 detection radius 내에서 인식.
/// 한 번 인식하면 영구 적대 모드 - 거리 무관 글로벌 추적.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Shooter))]
public sealed class EnemyBrain : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float attackRadius    = 6f;
    [SerializeField] private float moveSpeed       = 3f;

    private Rigidbody2D _rb;
    private Shooter     _shooter;
    private Transform   _target;
    private bool        _isAggro;

    void Awake()
    {
        _rb      = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;
        _shooter = GetComponent<Shooter>();
        _shooter.SetOwner(ProjectileOwner.Enemy);
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        UpdateTarget();

        if (_target == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, _target.position);

        if (dist <= attackRadius)
        {
            _rb.linearVelocity = Vector2.zero;
            _shooter.TryFireAt(_target.position);
        }
        else
        {
            // 사거리 밖이면 무한 추격
            Vector2 dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;
        }
    }

    private void UpdateTarget()
    {
        // 살아있는 타겟 유지
        if (_target != null)
        {
            var h = _target.GetComponent<HealthComponent>();
            if (h != null && h.IsAlive) return;
            _target = null;
        }

        // 한 번이라도 aggro 됐으면 글로벌 검색으로 끝까지 추적
        if (_isAggro)
        {
            _target = FindNearestGlobalTarget();
            return;
        }

        // 평소엔 detection radius 내에서만 검색
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, detectionRadius, Layers.EnemyTargetMask);

        float minDist  = float.MaxValue;
        Transform best = null;

        foreach (var hit in hits)
        {
            var hc = hit.GetComponent<HealthComponent>();
            if (hc == null || !hc.IsAlive) continue;
            float d = Vector2.Distance(transform.position, hit.transform.position);
            if (d < minDist) { minDist = d; best = hit.transform; }
        }

        _target = best;
        if (_target != null) _isAggro = true; // 첫 인식 → 영구 적대
    }

    private Transform FindNearestGlobalTarget()
    {
        Transform best = null;
        float minD     = float.MaxValue;

        var player = PlayerCharacter.Instance;
        if (player != null && player.Health.IsAlive)
        {
            float d = Vector2.Distance(transform.position, player.transform.position);
            if (d < minD) { minD = d; best = player.transform; }
        }

        var roster = PartyRoster.Instance;
        if (roster != null)
        {
            foreach (var c in roster.Members)
            {
                if (c == null || !c.Health.IsAlive) continue;
                float d = Vector2.Distance(transform.position, c.transform.position);
                if (d < minD) { minD = d; best = c.transform; }
            }
        }

        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
