using System.Linq;
using UnityEngine;

/// <summary>
/// 플레이어 인벤토리. EquipmentSlots를 래핑하고 장비 보너스를 PlayerStats에 반영.
/// </summary>
[RequireComponent(typeof(EquipmentSlots), typeof(PlayerStats), typeof(HealthComponent))]
public sealed class PlayerInventory : MonoBehaviour
{
    public EquipmentSlots Slots { get; private set; }

    private PlayerStats     _stats;
    private HealthComponent _health;

    void Awake()
    {
        Slots   = GetComponent<EquipmentSlots>();
        _stats  = GetComponent<PlayerStats>();
        _health = GetComponent<HealthComponent>();

        Slots.OnEquipmentChanged += ApplyBonuses;
    }

    void Start() => ApplyBonuses();

    void OnDestroy() => Slots.OnEquipmentChanged -= ApplyBonuses;

    private void ApplyBonuses()
    {
        _stats.RecalculateBonuses(Slots.TotalATK, Slots.TotalThreat, Slots.TotalWealth);
        _health.SetMaxHP(_stats.BaseHP + Slots.TotalHP);
    }

    public bool HasItem(EquipmentData item) => Slots.Inventory.Contains(item);

    /// <summary>NPC에게 장비 대여. 성공 시 true 반환.</summary>
    public bool LendTo(EquipmentData item, NPCInventory npcInventory)
    {
        if (!Slots.RemoveItem(item)) return false;
        npcInventory.ReceiveItem(item);
        return true;
    }

    /// <summary>NPC에서 장비 회수. 성공 시 true 반환.</summary>
    public bool RetrieveFrom(EquipmentData item, NPCInventory npcInventory)
    {
        if (!npcInventory.ReturnItem(item)) return false;
        Slots.AddItem(item);
        return true;
    }
}
