using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장비 슬롯 (무기/방어구/장신구 각 1개). 플레이어와 NPC 공통 사용.
/// </summary>
public sealed class EquipmentSlots : MonoBehaviour
{
    // 장착 슬롯
    private EquipmentData _weapon;
    private EquipmentData _armor;
    private EquipmentData _accessory;

    // 보유 장비 (장착 포함)
    private readonly List<EquipmentData> _inventory = new();

    public IReadOnlyList<EquipmentData> Inventory => _inventory;
    public EquipmentData Weapon    => _weapon;
    public EquipmentData Armor     => _armor;
    public EquipmentData Accessory => _accessory;

    public event Action OnEquipmentChanged;

    // ── 인벤토리 조작 ────────────────────────────────────

    public void AddItem(EquipmentData item)
    {
        _inventory.Add(item);
        OnEquipmentChanged?.Invoke();
    }

    public bool RemoveItem(EquipmentData item)
    {
        if (!_inventory.Contains(item)) return false;

        // 장착 중이면 해제
        if (_weapon    == item) _weapon    = null;
        if (_armor     == item) _armor     = null;
        if (_accessory == item) _accessory = null;

        _inventory.Remove(item);
        OnEquipmentChanged?.Invoke();
        return true;
    }

    // ── 장착 ─────────────────────────────────────────────

    public void Equip(EquipmentData item)
    {
        if (!_inventory.Contains(item)) return;
        switch (item.type)
        {
            case EquipmentType.Weapon:    _weapon    = item; break;
            case EquipmentType.Armor:     _armor     = item; break;
            case EquipmentType.Accessory: _accessory = item; break;
        }
        OnEquipmentChanged?.Invoke();
    }

    public void Unequip(EquipmentType type)
    {
        switch (type)
        {
            case EquipmentType.Weapon:    _weapon    = null; break;
            case EquipmentType.Armor:     _armor     = null; break;
            case EquipmentType.Accessory: _accessory = null; break;
        }
        OnEquipmentChanged?.Invoke();
    }

    // ── 스탯 합계 ────────────────────────────────────────

    public float TotalATK    => (_weapon?.atk    ?? 0) + (_armor?.atk    ?? 0) + (_accessory?.atk    ?? 0);
    public float TotalHP     => (_weapon?.hp     ?? 0) + (_armor?.hp     ?? 0) + (_accessory?.hp     ?? 0);
    public float TotalThreat => (_weapon?.threat ?? 0) + (_armor?.threat ?? 0) + (_accessory?.threat ?? 0);
    public float TotalWealth => (_weapon?.wealth ?? 0) + (_armor?.wealth ?? 0) + (_accessory?.wealth ?? 0);
}
