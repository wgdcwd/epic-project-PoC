using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PartyRoster : MonoBehaviour
{
    public static PartyRoster Instance { get; private set; }

    private readonly List<CompanionCharacter> _members = new();
    public IReadOnlyList<CompanionCharacter> Members => _members;

    public event Action<CompanionCharacter> OnMemberAdded;
    public event Action<CompanionCharacter> OnMemberRemoved;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddMember(CompanionCharacter companion)
    {
        if (_members.Contains(companion)) return;
        _members.Add(companion);
        OnMemberAdded?.Invoke(companion);
        LogManager.AddLog($"{companion.NPCStats.NPCName}이(가) 파티에 합류했다.");
    }

    public void RemoveMember(CompanionCharacter companion)
    {
        if (!_members.Remove(companion)) return;
        OnMemberRemoved?.Invoke(companion);
    }

    // 전리품 분배: 각 동료의 미정산금에 몫 추가
    public void DistributeLoot(int totalLootValue)
    {
        if (_members.Count == 0) return;
        int heads = _members.Count + 1; // +1 플레이어
        int share = totalLootValue / heads;

        foreach (var member in _members)
            member.Relationship.AddUnpaidAmount(share);
    }

    public int HeadCount => _members.Count + 1;
}
