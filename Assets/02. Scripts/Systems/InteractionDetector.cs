using UnityEngine;

/// <summary>
/// 상호작용 감지 + 마우스 hover 강조.
/// - Update: 마우스 hover 위치의 NPC를 강조 (interactionRange 안에 있을 때만)
/// - TryInteract(worldPos): 우클릭 위치의 NPC와 상호작용 (interactionRange 안에 있어야 함)
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
public sealed class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionRange = 6f;
    [SerializeField] private float clickPickRadius  = 0.5f; // 마우스 클릭 허용 반경

    [SerializeField] private Color highlightColor = new(1f, 1f, 0.5f, 1f);

    [SerializeField] private InteractionMenuView     wandererMenuView;
    [SerializeField] private CompanionManagementView companionMenuView;

    private PlayerInputHandler _input;
    private NPCCharacter       _highlighted;
    private Color              _originalColor;
    private SpriteRenderer     _highlightedRenderer;

    void Awake() => _input = GetComponent<PlayerInputHandler>();

    void Update()
    {
        Vector2 mousePos = _input.MouseWorldPos;
        NPCCharacter hovered = FindNPCAtPosition(mousePos);

        // 거리 밖이면 강조 안 함
        if (hovered != null && !IsInRange(hovered)) hovered = null;

        if (hovered == _highlighted) return;

        // 이전 강조 해제
        if (_highlightedRenderer != null)
            _highlightedRenderer.color = _originalColor;

        _highlighted         = hovered;
        _highlightedRenderer = null;

        // 새 강조 적용
        if (hovered != null)
        {
            _highlightedRenderer = hovered.GetComponentInChildren<SpriteRenderer>();
            if (_highlightedRenderer != null)
            {
                _originalColor = _highlightedRenderer.color;
                _highlightedRenderer.color = highlightColor;
            }
        }
    }

    public void TryInteract(Vector2 worldPos)
    {
        NPCCharacter clicked = FindNPCAtPosition(worldPos);
        if (clicked == null) return;
        if (!IsInRange(clicked)) return;

        if (clicked is CompanionCharacter companion)
            companionMenuView?.Open(companion);
        else if (clicked is WandererCharacter wanderer)
            wandererMenuView?.Open(wanderer);
    }

    /// <summary>지정 월드 좌표에 있는 상호작용 가능 NPC를 찾는다 (작은 반경 검색).</summary>
    private NPCCharacter FindNPCAtPosition(Vector2 worldPos)
    {
        // 1차: 정확히 콜라이더 안에 있는 NPC
        Collider2D direct = Physics2D.OverlapPoint(worldPos, Layers.InteractableMask);
        var npc = direct != null ? direct.GetComponent<NPCCharacter>() : null;
        if (IsValid(npc)) return npc;

        // 2차: 클릭 허용 반경 안에서 가장 가까운 NPC
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, clickPickRadius, Layers.InteractableMask);
        float minD = float.MaxValue;
        NPCCharacter best = null;

        foreach (var hit in hits)
        {
            var n = hit.GetComponent<NPCCharacter>();
            if (!IsValid(n)) continue;
            float d = Vector2.Distance(worldPos, hit.transform.position);
            if (d < minD) { minD = d; best = n; }
        }
        return best;
    }

    private static bool IsValid(NPCCharacter npc)
    {
        if (npc == null || !npc.Health.IsAlive) return false;

        // 도주/적대 중인 Wanderer 차단
        if (npc is WandererCharacter w && w.Mode != WandererCharacter.WandererMode.Idle) return false;

        // 도주/적대(배신/절도) 중인 Companion 차단
        if (npc is CompanionCharacter c &&
            (c.Brain.State == CompanionState.Hostile || c.Brain.State == CompanionState.Fleeing))
            return false;

        return true;
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
