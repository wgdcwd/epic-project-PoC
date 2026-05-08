using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 인벤토리. 장비 수령 시 성향 점수 비교 후 자동 착용 결정.
/// </summary>
[RequireComponent(typeof(NPCStats), typeof(EquipmentSlots), typeof(HealthComponent))]
public sealed class NPCInventory : MonoBehaviour
{
    public EquipmentSlots Slots { get; private set; }

    private NPCStats        _stats;
    private HealthComponent _health;

    void Awake()
    {
        Slots   = GetComponent<EquipmentSlots>();
        _stats  = GetComponent<NPCStats>();
        _health = GetComponent<HealthComponent>();

        Slots.OnEquipmentChanged += ApplyBonuses;
    }

    void OnDestroy() => Slots.OnEquipmentChanged -= ApplyBonuses;

    // ── 장비 수령 ────────────────────────────────────────

    public void ReceiveItem(EquipmentData item)
    {
        Slots.AddItem(item);
        TryAutoEquip(item);

        GetComponent<CompanionReaction>()?.TriggerEquipmentLent();
    }

    // ── 장비 반환 (플레이어에게) ──────────────────────────

    public bool ReturnItem(EquipmentData item)
    {
        bool wasEquipped = (Slots.Weapon    == item ||
                            Slots.Armor     == item ||
                            Slots.Accessory == item);

        if (!Slots.RemoveItem(item)) return false;

        bool isDanger = _stats.Stamina <= 30f || _stats.Trust <= 40f;
        GetComponent<CompanionReaction>()?.TriggerEquipmentRetrieved(wasEquipped, isDanger);

        if (wasEquipped && (isDanger || _stats.Trust <= 40f))
            GetComponent<CompanionRelationship>()?.AddComplaintScore(5);

        return true;
    }

    /// <summary>자진 탈퇴 또는 Trust 높음 탈퇴 시 전체 반환.</summary>
    public void ReturnAllToPlayer()
    {
        var player = PlayerCharacter.Instance?.Inventory;
        if (player == null) return;

        // 복사본으로 순회 (RemoveItem이 원본 리스트 수정)
        var items = new List<EquipmentData>(Slots.Inventory);
        foreach (var item in items)
        {
            Slots.RemoveItem(item);
            player.Slots.AddItem(item);
        }
    }

    // ── 자동 착용 로직 ────────────────────────────────────

    private void TryAutoEquip(EquipmentData newItem)
    {
        EquipmentData current = newItem.type switch
        {
            EquipmentType.Weapon    => Slots.Weapon,
            EquipmentType.Armor     => Slots.Armor,
            EquipmentType.Accessory => Slots.Accessory,
            _                       => null
        };

        float newScore     = newItem.ScoreFor(_stats.Greed, _stats.Fear, _stats.Morality);
        float currentScore = current != null
            ? current.ScoreFor(_stats.Greed, _stats.Fear, _stats.Morality)
            : 0f;

        if (newScore > currentScore)
            Slots.Equip(newItem);
    }

    private void ApplyBonuses()
    {
        _stats.BonusATK = Slots.TotalATK;
        _health.SetMaxHP(_stats.BaseHP + Slots.TotalHP);
    }
}
