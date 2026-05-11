using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Room 프리팹에 붙이는 스테이지 컨트롤러.
/// 
/// [인스펙터 설정]
/// - entranceBlockers : 입구 쪽 벽 오브젝트들 (진입 시 활성화 → 뒤로 못 돌아감)
/// - exitBlockers     : 출구 쪽 벽 오브젝트들 (클리어 시 비활성화 → 앞으로 진행)
/// - campButton       : 야영 버튼 UI (클리어 시 활성화)
/// - enemies          : 이 방에 배치된 적 오브젝트들 (직접 드래그 또는 자동 수집)
/// 
/// [동작 흐름]
/// 1. 플레이어가 방 트리거(BoxCollider2D IsTrigger=true)에 진입
/// 2. 입구 봉쇄 → 배치된 적 활성화 → 전멸 감시 시작
/// 3. 적 전멸 → 출구 개방 + 야영 버튼 활성화 + 강제 정산 페이즈 시작
/// 4. 야영은 이 방에서 최대 1회
/// </summary>
public sealed class RoomStageController : MonoBehaviour
{
    // ── 인스펙터 ──────────────────────────────────────────

    [Header("Blockers")]
    [Tooltip("진입 시 활성화할 입구 벽 오브젝트들")]
    [SerializeField] private List<GameObject> entranceBlockers = new();

    [Tooltip("클리어 시 비활성화할 출구 벽 오브젝트들")]
    [SerializeField] private List<GameObject> exitBlockers = new();

    [Header("Enemies")]
    [Tooltip("이 방에 배치된 적 오브젝트들. 비워두면 자식에서 EnemyCharacter 자동 수집.")]
    [SerializeField] private List<GameObject> enemies = new();

    [Header("UI")]
    [Tooltip("클리어 시 활성화할 야영 버튼 오브젝트")]
    [SerializeField] private GameObject campButtonObj;

    [Header("Settlement")]
    [Tooltip("정산 UI (SettlementView). 씬의 Canvas에서 연결.")]
    [SerializeField] private SettlementView settlementView;

    // ── 상태 ──────────────────────────────────────────────

    public bool IsActivated { get; private set; }
    public bool IsCleared   { get; private set; }
    public bool CampUsed    { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────

    void Awake()
    {
        // 출구 벽은 처음에 활성화 (막혀있음)
        SetBlockers(exitBlockers, true);
        // 입구 벽은 처음에 비활성화 (열려있음)
        SetBlockers(entranceBlockers, false);
        // 야영 버튼 비활성화
        if (campButtonObj != null) campButtonObj.SetActive(false);

        // enemies 리스트가 비어있으면 자식에서 자동 수집
        if (enemies.Count == 0)
            CollectEnemiesFromChildren();

        // 적은 처음에 비활성화 (방 진입 전에는 잠들어 있음)
        SetEnemiesActive(false);
    }

    // ── 트리거 진입 ───────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsActivated) return;
        if (!other.CompareTag("Player")) return;

        Activate();
    }

    // ── 활성화 ────────────────────────────────────────────

    private void Activate()
    {
        IsActivated = true;

        // 입구 봉쇄
        SetBlockers(entranceBlockers, true);

        // 적 활성화
        SetEnemiesActive(true);

        // 적이 없으면 즉시 클리어
        if (enemies.Count == 0 || AllEnemiesDead())
        {
            OnCleared();
            return;
        }

        // 전멸 감시 시작
        StartCoroutine(WatchEnemies());

        LogManager.AddLog("전투 시작!");
    }

    // ── 전멸 감시 ─────────────────────────────────────────

    private IEnumerator WatchEnemies()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (AllEnemiesDead())
            {
                OnCleared();
                yield break;
            }
        }
    }

    private bool AllEnemiesDead()
    {
        foreach (var e in enemies)
        {
            if (e == null) continue;                          // 이미 Destroy된 경우
            var hc = e.GetComponent<HealthComponent>();
            if (hc != null && hc.IsAlive) return false;      // 살아있는 적 존재
            if (hc == null && e.activeInHierarchy) return false; // HealthComponent 없는 적
        }
        return true;
    }

    // ── 클리어 처리 ───────────────────────────────────────

    private void OnCleared()
    {
        if (IsCleared) return;
        IsCleared = true;

        // 출구 개방
        SetBlockers(exitBlockers, false);

        // 야영 버튼 활성화
        if (campButtonObj != null) campButtonObj.SetActive(true);

        LogManager.AddLog("전투 종료! 앞으로 나아갈 수 있다.");

        // 강제 정산 페이즈 시작 (동료가 있을 때만)
        if (PartyRoster.Instance != null && PartyRoster.Instance.Members.Count > 0)
            StartCoroutine(RunSettlementPhase());
    }

    // ── 강제 정산 페이즈 ──────────────────────────────────

    /// <summary>
    /// 파티 동료를 순서대로 정산 UI에 올린다.
    /// 한 명 정산 완료 → 다음 명 순서로 진행.
    /// </summary>
    private IEnumerator RunSettlementPhase()
    {
        if (settlementView == null)
        {
            Debug.LogWarning("[RoomStageController] SettlementView가 연결되지 않았습니다.");
            yield break;
        }

        // 정산 대상: 미정산금이 있는 동료만
        var members = new List<NPCCharacter>(PartyRoster.Instance.Members);
        var targets = members.FindAll(m => m != null && m.Relationship.UnpaidAmount > 0f);

        if (targets.Count == 0) yield break;

        LogManager.AddLog("정산 페이즈 시작.");

        foreach (var companion in targets)
        {
            if (companion == null) continue;

            bool done = false;
            settlementView.Open(companion, () => done = true);

            // 정산 UI가 닫힐 때까지 대기
            yield return new WaitUntil(() => done);

            yield return new WaitForSeconds(0.2f); // 짧은 텀
        }

        LogManager.AddLog("정산 완료.");
    }

    // ── 야영 진입 (외부에서 호출) ─────────────────────────

    /// <summary>
    /// 야영 버튼에서 호출. 스테이지당 1회만 허용.
    /// 야영 가능 여부를 반환.
    /// </summary>
    public bool TryCamp()
    {
        if (!IsCleared)
        {
            LogManager.AddLog("전투가 끝나야 야영할 수 있다.");
            return false;
        }
        if (CampUsed)
        {
            LogManager.AddLog("이 구역에서는 이미 야영했다.");
            return false;
        }

        CampUsed = true;
        // 야영 버튼 비활성화 (1회 사용 후)
        if (campButtonObj != null) campButtonObj.SetActive(false);

        RestSystem.Instance?.TryRest();
        return true;
    }

    // ── 헬퍼 ──────────────────────────────────────────────

    private static void SetBlockers(List<GameObject> blockers, bool active)
    {
        foreach (var b in blockers)
            if (b != null) b.SetActive(active);
    }

    private void SetEnemiesActive(bool active)
    {
        foreach (var e in enemies)
            if (e != null) e.SetActive(active);
    }

    private void CollectEnemiesFromChildren()
    {
        var found = GetComponentsInChildren<EnemyCharacter>(includeInactive: true);
        foreach (var e in found)
            enemies.Add(e.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        // 방 트리거 영역 시각화
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = IsCleared ? Color.green : IsActivated ? Color.red : Color.cyan;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
