using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 사망 시 보유 골드/장비를 월드에 드랍.
/// 픽업은 플레이어가 직접 가까이 가서 주워야 한다.
/// </summary>
public static class NPCDropHandler
{
    public static void HandleDrop(NPCStats stats, NPCInventory inventory, Vector3 position)
    {
        if (stats == null) return;

        // 골드 드랍
        int goldHeld = stats.TakeAllGold();
        if (goldHeld > 0)
        {
            LootSystem.SpawnGoldPickup(goldHeld, position);
        }

        // 장비 드랍 - 각 장비마다 별도 픽업, 위치 약간씩 분산
        if (inventory != null)
        {
            var items = new List<EquipmentData>(inventory.Slots.Inventory);
            foreach (var item in items)
            {
                inventory.Slots.RemoveItem(item);
                Vector2 offset = Random.insideUnitCircle * 0.6f;
                LootSystem.SpawnEquipmentPickup(item, position + new Vector3(offset.x, offset.y, 0f));
            }

            if (items.Count > 0)
                LogManager.AddLog($"{stats.NPCName}이(가) 가지고 있던 장비 {items.Count}개가 떨어졌다.");
        }

        if (goldHeld > 0)
            LogManager.AddLog($"{stats.NPCName}이(가) {goldHeld}G를 떨어뜨렸다.");
    }
}
