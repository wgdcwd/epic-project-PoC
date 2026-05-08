using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 사망 시 들고 있던 골드/장비를 정리.
/// 죽인 게 플레이어/동료면 회수해서 플레이어에게 인계, 그 외엔 그냥 사라짐.
/// </summary>
public static class NPCDropHandler
{
    public static void HandleDrop(NPCStats stats, NPCInventory inventory, GameObject attacker)
    {
        if (attacker == null || stats == null) return;

        bool toPlayer = IsAttackerPlayerOrCompanion(attacker);

        int goldHeld = stats.TakeAllGold();
        var items    = inventory != null
            ? new List<EquipmentData>(inventory.Slots.Inventory)
            : new List<EquipmentData>();

        if (!toPlayer)
        {
            LogManager.AddLog($"{stats.NPCName}이(가) 가지고 있던 것들이 사라졌다.");
            return;
        }

        var player = PlayerCharacter.Instance;
        if (player == null) return;

        if (goldHeld > 0)
        {
            player.Stats.AddGold(goldHeld);
            LogManager.AddLog($"{stats.NPCName}에게서 {goldHeld}G 회수.");
        }

        if (items.Count > 0 && inventory != null)
        {
            int recovered = 0;
            foreach (var item in items)
            {
                if (inventory.Slots.RemoveItem(item))
                {
                    player.Inventory.Slots.AddItem(item);
                    recovered++;
                }
            }
            if (recovered > 0)
                LogManager.AddLog($"{stats.NPCName}에게서 장비 {recovered}개 회수.");
        }
    }

    private static bool IsAttackerPlayerOrCompanion(GameObject attacker)
    {
        int playerLayer    = LayerMask.NameToLayer(Layers.Player);
        int companionLayer = LayerMask.NameToLayer(Layers.Companion);
        return attacker.layer == playerLayer || attacker.layer == companionLayer;
    }
}
