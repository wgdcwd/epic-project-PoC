using UnityEngine;

public enum ProjectileOwner { Player, Companion, Enemy }

/// <summary>
/// 투사체. 초기화 후 직선 이동, 충돌 시 데미지 처리.
/// 충돌 레이어 마스크는 Physics 2D Layer Collision Matrix로 설정 (에디터).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class Projectile : MonoBehaviour
{
    private float          _damage;
    private float          _speed;
    private ProjectileOwner _owner;
    private GameObject     _shooter;
    private Rigidbody2D    _rb;

    [SerializeField] private float lifetime = 5f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
    }

    public void Initialize(Vector2 direction, float damage, float speed,
                           ProjectileOwner owner, GameObject shooter)
    {
        _damage  = damage;
        _speed   = speed;
        _owner   = owner;
        _shooter = shooter;

        _rb.linearVelocity = direction * speed;

        // 방향에 맞게 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == _shooter) return;

        var health = other.GetComponent<HealthComponent>();
        if (health == null)
        {
            // 벽/장애물에 닿으면 투사체 소멸
            Destroy(gameObject);
            return;
        }
        if (!health.IsAlive) return;

        // Owner-Target 호환성 체크 (기획서 15-2 투사체 피격 범위)
        if (!IsValidTarget(other.gameObject)) return;

        health.TakeDamage(_damage, _shooter);

        // 동료가 플레이어 투사체에 맞으면 Trust 감소 처리
        if (_owner == ProjectileOwner.Player)
        {
            var npc = other.GetComponent<NPCCharacter>();
            if (npc != null && npc.Allegiance == NPCAllegiance.Companion)
                npc.Relationship.OnHitByPlayer(_damage);
        }

        Destroy(gameObject);
    }

    private bool IsValidTarget(GameObject target)
    {
        int layer = target.layer;
        int player    = LayerMask.NameToLayer(Layers.Player);
        int companion = LayerMask.NameToLayer(Layers.Companion);
        int enemy     = LayerMask.NameToLayer(Layers.Enemy);
        int wanderer  = LayerMask.NameToLayer(Layers.WandererNPC);

        return _owner switch
        {
            ProjectileOwner.Player    => layer == companion || layer == enemy || layer == wanderer,
            ProjectileOwner.Companion => layer == enemy,
            ProjectileOwner.Enemy     => layer == player    || layer == companion,
            _                         => false
        };
    }
}
