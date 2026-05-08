using UnityEngine;

/// <summary>
/// 장비 픽업. Initialize(EquipmentData)으로 장비 데이터 설정.
/// 스프라이트는 EquipmentData.icon, 색상은 등급 색상.
/// 픽업 시 플레이어 인벤토리에 추가.
/// </summary>
public sealed class EquipmentPickup : PickupBase
{
    [SerializeField] private EquipmentData equipment;
    [SerializeField] private SpriteRenderer iconRenderer;

    public void Initialize(EquipmentData data)
    {
        equipment = data;
        if (data == null) return;

        if (iconRenderer != null && data.icon != null)
            iconRenderer.sprite = data.icon;
    }

    protected override void OnPickup(PlayerCharacter player)
    {
        if (equipment == null) return;
        player.Inventory.Slots.AddItem(equipment);
        LogManager.AddLog($"{equipment.itemName} 획득.");
    }

    /// <summary>NPC가 줍는 경로. 성공 시 NPC 인벤토리에 추가하고 자동 착용 판정 후 자체 파괴. (로그 없음 — 사용자 무관)</summary>
    public bool TryPickupBy(NPCInventory inv)
    {
        if (inv == null || equipment == null) return false;
        if (!TryClaim()) return false;

        // ReceiveItem: 인벤토리 추가 + 자동 착용 점수 비교
        // (CompanionReaction이 있으면 "받은 장비를 살펴본다" 로그가 뜰 수 있지만 동료 컴포넌트 한정이라 Wanderer는 무관)
        inv.ReceiveItem(equipment);

        Destroy(gameObject);
        return true;
    }
}
