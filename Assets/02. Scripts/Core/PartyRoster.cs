using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PartyRoster : MonoBehaviour
{
    public static PartyRoster Instance { get; private set; }

    private readonly List<NPCCharacter> _members = new();
    public IReadOnlyList<NPCCharacter> Members => _members;

    public event Action<NPCCharacter> OnMemberAdded;
    public event Action<NPCCharacter> OnMemberRemoved;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddMember(NPCCharacter npc)
    {
        if (_members.Contains(npc)) return;
        _members.Add(npc);
        OnMemberAdded?.Invoke(npc);
        LogManager.AddLog($"{npc.Stats.NPCName}이(가) 파티에 합류했다.");
    }

    public void RemoveMember(NPCCharacter npc)
    {
        if (!_members.Remove(npc)) return;
        OnMemberRemoved?.Invoke(npc);
    }

    public void DistributeLoot(int totalLootValue)
    {
        if (_members.Count == 0) return;
        int heads = _members.Count + 1;
        int share = totalLootValue / heads;
        foreach (var member in _members)
            member.Relationship.AddUnpaidAmount(share);
    }

    public int HeadCount => _members.Count + 1;
}
