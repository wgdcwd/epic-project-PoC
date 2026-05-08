using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 동료 카드 목록 관리. PartyRoster 이벤트를 구독해 카드를 추가/제거.
/// </summary>
public sealed class PartyPanelView : MonoBehaviour
{
    [SerializeField] private CompanionCardView cardPrefab;
    [SerializeField] private Transform         cardContainer;

    private readonly Dictionary<CompanionCharacter, CompanionCardView> _cards = new();

    void Start()
    {
        if (PartyRoster.Instance == null) return;
        PartyRoster.Instance.OnMemberAdded   += AddCard;
        PartyRoster.Instance.OnMemberRemoved += RemoveCard;

        // 이미 있는 멤버 반영
        foreach (var member in PartyRoster.Instance.Members)
            AddCard(member);
    }

    void OnDestroy()
    {
        if (PartyRoster.Instance == null) return;
        PartyRoster.Instance.OnMemberAdded   -= AddCard;
        PartyRoster.Instance.OnMemberRemoved -= RemoveCard;
    }

    private void AddCard(CompanionCharacter companion)
    {
        if (_cards.ContainsKey(companion)) return;
        var card = Instantiate(cardPrefab, cardContainer);
        card.Bind(companion);
        _cards[companion] = card;
    }

    private void RemoveCard(CompanionCharacter companion)
    {
        if (!_cards.TryGetValue(companion, out var card)) return;
        card.Unbind();
        Destroy(card.gameObject);
        _cards.Remove(companion);
    }
}
