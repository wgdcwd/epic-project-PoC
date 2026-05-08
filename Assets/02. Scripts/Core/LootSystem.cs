using UnityEngine;

/// <summary>
/// 전리품 드랍 시스템. 픽업 프리팹을 인스펙터에서 등록하고 사용.
/// - 적 사망 시 attacker가 Player/Companion이면 골드 주머니를 월드에 드랍
/// - attacker가 Wanderer면 그 NPC 자기 지갑에 누적
/// - NPC 사망 시 보유 골드/장비 모두 월드 드랍
/// </summary>
public sealed class LootSystem : MonoBehaviour
{
    public static LootSystem Instance { get; private set; }

    [SerializeField] private GameObject goldPickupPrefab;
    [SerializeField] private GameObject equipmentPickupPrefab;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 적 사망 시 호출. 누가 죽였든 월드에 골드 주머니 드랍.
    /// 누구든 가까이 가서 주울 수 있다 (플레이어 자석 / Wanderer 능동 픽업).
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

    public static void SpawnEquipmentPickup(EquipmentData data, Vector3 pos)
    {
        if (Instance == null || Instance.equipmentPickupPrefab == null || data == null) return;
        var go = Instantiate(Instance.equipmentPickupPrefab, pos, Quaternion.identity);
        go.GetComponent<EquipmentPickup>()?.Initialize(data);
    }
}
