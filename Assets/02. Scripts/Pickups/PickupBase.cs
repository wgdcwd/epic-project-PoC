using UnityEngine;

/// <summary>
/// 모든 픽업의 공통 베이스. 자석 흡인 + 자동 수명 + 플레이어 거리 체크.
/// 실제 픽업 동작은 자식의 OnPickup에서 정의.
/// </summary>
public abstract class PickupBase : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] protected float pickupRadius = 0.6f;
    [SerializeField] protected float magnetRadius = 2f;
    [SerializeField] protected float magnetSpeed  = 6f;
    [SerializeField] protected float lifetime     = 60f;

    [Header("Visual")]
    [SerializeField] protected float spawnPopRadius = 0.4f; // 스폰 시 살짝 튐
    [SerializeField] protected float bobAmplitude   = 0.05f;
    [SerializeField] protected float bobSpeed       = 3f;

    private float   _bornAt;
    private Vector3 _baseY;
    protected bool  _picked;

    public bool IsPicked => _picked;

    /// <summary>외부에서 픽업을 시도할 때 사용 (한 번만 성공). NPC가 줍는 경로 등.</summary>
    protected bool TryClaim()
    {
        if (_picked) return false;
        _picked = true;
        return true;
    }

    protected virtual void Start()
    {
        _bornAt = Time.time;
        // 살짝 튀어나오는 위치 분산
        Vector2 pop = Random.insideUnitCircle * spawnPopRadius;
        transform.position += new Vector3(pop.x, pop.y, 0f);
        _baseY = transform.position;
    }

    protected virtual void Update()
    {
        if (_picked) return;
        if (Time.time - _bornAt > lifetime) { Destroy(gameObject); return; }

        var player = PlayerCharacter.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        if (dist <= pickupRadius)
        {
            _picked = true;
            OnPickup(player);
            Destroy(gameObject);
            return;
        }

        if (dist <= magnetRadius)
        {
            // 자석 흡인
            Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            transform.position += (Vector3)(dir * magnetSpeed * Time.deltaTime);
        }
        else
        {
            // 살짝 위아래 부유
            float y = Mathf.Sin((Time.time - _bornAt) * bobSpeed) * bobAmplitude;
            Vector3 pos = transform.position;
            pos.y = _baseY.y + y;
            transform.position = pos;
        }
    }

    protected abstract void OnPickup(PlayerCharacter player);
}
