using UnityEngine;

/// <summary>
/// 적 AI. 감지 범위 내 가장 가까운 대상(플레이어/동료)을 추격 후 공격.
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
        if (!GameManager.Instance.IsPlaying) return;

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
            Vector2 dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;
        }
    }

    private void UpdateTarget()
    {
        // 한번 인식한 타겟이 살아있으면 끝까지 추적 (거리 무시)
        if (_target != null)
        {
            var h = _target.GetComponent<HealthComponent>();
            if (h != null && h.IsAlive) return;
            _target = null;
        }

        // 감지 범위 내 플레이어/동료 중 가장 가까운 대상 선택
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius,
                                                        Layers.EnemyTargetMask);
        float minDist  = float.MaxValue;
        Transform best = null;

        foreach (var hit in hits)
        {
            if (!hit.GetComponent<HealthComponent>()?.IsAlive ?? true) continue;
            float d = Vector2.Distance(transform.position, hit.transform.position);
            if (d < minDist) { minDist = d; best = hit.transform; }
        }

        _target = best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
