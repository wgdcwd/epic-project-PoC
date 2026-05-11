using UnityEngine;

/// <summary>
/// 전리품 드랍 시스템. 골드 픽업 프리팹을 인스펙터에서 등록하고 사용.
/// 장비 시스템 제거로 SpawnEquipmentPickup 삭제.
/// </summary>
public sealed class LootSystem : MonoBehaviour
{
    public static LootSystem Instance { get; private set; }

    [SerializeField] private GameObject goldPickupPrefab;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 적 사망 시 호출. 월드에 골드 주머니 드랍.
    /// </summary>
    public static void HandleEnemyDeath(int goldDrop, GameObject attacker, Vector3 position)
    {
        if (goldDrop <= 0) return;
        SpawnGoldPickup(goldDrop, position);
    }

    public static void SpawnGoldPickup(int amount, Vector3 pos)
    {
        if (Instance == null || Instance.goldPickupPrefab == null || amount <= 0) return;
        var go = Instantiate(Instance.goldPickupPrefab, pos, Quaternion.identity);
        go.GetComponent<GoldPickup>()?.Initialize(amount);
    }
}
