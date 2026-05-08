using UnityEngine;

/// <summary>
/// 투사체 발사 컴포넌트. Player/Companion/Enemy 공유.
/// </summary>
public sealed class Shooter : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      fireRate   = 0.5f; // 초당 발사 간격
    [SerializeField] private float      damage     = 10f;
    [SerializeField] private float      speed      = 10f;
    [SerializeField] private ProjectileOwner ownerType = ProjectileOwner.Player;

    private float _cooldown;

    public float  Damage    => damage;
    public bool   CanFire   => _cooldown <= 0f;

    public void SetOwner(ProjectileOwner owner) => ownerType = owner;

    void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
    }

    /// <summary>direction: 월드 방향 벡터 (정규화 불필요)</summary>
    public bool TryFire(Vector2 direction)
    {
        if (!CanFire || projectilePrefab == null) return false;
        _cooldown = fireRate;

        var go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj != null)
            proj.Initialize(direction.normalized, damage, speed, ownerType, gameObject);

        return true;
    }

    /// <summary>월드 좌표 목표 지점을 향해 발사</summary>
    public bool TryFireAt(Vector2 targetWorldPos)
    {
        Vector2 dir = targetWorldPos - (Vector2)transform.position;
        return TryFire(dir);
    }
}
