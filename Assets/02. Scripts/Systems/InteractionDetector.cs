using UnityEngine;

/// <summary>
/// 플레이어 주변 상호작용 가능한 NPC 탐지. 우클릭 시 적절한 UI를 열어준다.
/// </summary>
public sealed class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionRange = 6f;

    // UI 참조 (Inspector 연결)
    [SerializeField] private InteractionMenuView     wandererMenuView;
    [SerializeField] private CompanionManagementView companionMenuView;

    [SerializeField] private Color highlightColor = new(1f, 1f, 0.5f, 1f);

    private NPCCharacter _highlighted;
    private Color        _originalColor;
    private SpriteRenderer _highlightedRenderer;

    void Update()
    {
        // 가장 가까운 상호작용 가능 NPC 강조 표시
        NPCCharacter nearest = FindNearest();
        if (nearest == _highlighted) return;

        // 이전 강조 해제
        if (_highlightedRenderer != null)
            _highlightedRenderer.color = _originalColor;

        _highlighted         = nearest;
        _highlightedRenderer = null;

        // 새 강조 적용
        if (nearest != null)
        {
            _highlightedRenderer = nearest.GetComponentInChildren<SpriteRenderer>();
            if (_highlightedRenderer != null)
            {
                _originalColor = _highlightedRenderer.color;
                _highlightedRenderer.color = highlightColor;
            }
        }
    }

    public void TryInteract(Vector2 worldPos)
    {
        NPCCharacter nearest = FindNearest();
        if (nearest == null) return;

        if (nearest is CompanionCharacter companion)
        {
            companionMenuView?.Open(companion);
        }
        else if (nearest is WandererCharacter wanderer)
        {
            wandererMenuView?.Open(wanderer);
        }
    }

    public NPCCharacter FindNearest()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, interactionRange, Layers.InteractableMask);

        float         minDist = float.MaxValue;
        NPCCharacter  best    = null;

        foreach (var hit in hits)
        {
            var npc = hit.GetComponent<NPCCharacter>();
            if (npc == null || !npc.Health.IsAlive) continue;

            // 도주/적대 중인 Wanderer는 상호작용 불가
            if (npc is WandererCharacter w && w.Mode != WandererCharacter.WandererMode.Idle) continue;

            float d = Vector2.Distance(transform.position, hit.transform.position);
            if (d < minDist) { minDist = d; best = npc; }
        }

        return best;
    }

    public bool IsInRange(NPCCharacter npc)
    {
        if (npc == null) return false;
        return Vector2.Distance(transform.position, npc.transform.position) <= interactionRange;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
