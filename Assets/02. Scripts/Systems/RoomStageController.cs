using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Room 프리팹에 붙이는 스테이지 컨트롤러.
/// 방의 물리적 상태(진입/봉쇄/적/클리어)만 관리한다.
///
/// [이벤트]
/// - OnCleared : 적 전멸 시 발행 → StageFlowSystem이 야영 버튼 활성화
/// - OnExited  : 플레이어가 출구를 통과할 때 발행 → StageFlowSystem이 정산 창 표시
///
/// [인스펙터 설정]
/// - entranceBlockers : 입구 쪽 벽 오브젝트들 (진입 시 활성화)
/// - exitBlockers     : 출구 쪽 벽 오브젝트들 (클리어 시 비활성화)
/// - exitTrigger      : 출구 통과 감지용 별도 트리거 오브젝트 (ExitTrigger 컴포넌트 부착)
/// - exitPoint        : 절도 성공 시 플레이어를 이동시킬 출구 밖 위치
/// - enemies          : 이 방에 배치된 적 오브젝트들 (비워두면 자식에서 자동 수집)
/// </summary>
public sealed class RoomStageController : MonoBehaviour
{
    // ── 인스펙터 ──────────────────────────────────────────

    [Header("Blockers")]
    [Tooltip("진입 시 활성화할 입구 벽 오브젝트들")]
    [SerializeField] private List<GameObject> entranceBlockers = new();

    [Tooltip("클리어 시 비활성화할 출구 벽 오브젝트들")]
    [SerializeField] private List<GameObject> exitBlockers = new();

    [Header("Exit")]
    [Tooltip("출구 통과 감지용 트리거 오브젝트 (RoomExitTrigger 컴포넌트 부착)")]
    [SerializeField] private RoomExitTrigger exitTrigger;

    [Tooltip("절도 성공 시 플레이어를 이동시킬 출구 밖 위치")]
    [SerializeField] private Transform exitPoint;
    public Transform ExitPoint => exitPoint;

    [Header("Enemies")]
    [Tooltip("이 방에 배치된 적 오브젝트들. 비워두면 자식에서 EnemyCharacter 자동 수집.")]
    [SerializeField] private List<GameObject> enemies = new();

    // ── 이벤트 ────────────────────────────────────────────

    /// <summary>적 전멸 시 발행. StageFlowSystem이 야영 버튼 활성화에 사용.</summary>
    public event Action<RoomStageController> OnCleared;

    /// <summary>플레이어가 출구를 통과할 때 발행. StageFlowSystem이 정산 창 표시에 사용.</summary>
    public event Action<RoomStageController> OnExited;

    // ── 상태 ──────────────────────────────────────────────

    public bool IsActivated { get; private set; }
    public bool IsCleared   { get; private set; }
    public bool CampUsed    { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────

    void Awake()
    {
        SetBlockers(exitBlockers, true);
        SetBlockers(entranceBlockers, false);

        if (enemies.Count == 0)
            CollectEnemiesFromChildren();

        SetEnemiesActive(false);

        // 출구 트리거 연결
        if (exitTrigger != null)
            exitTrigger.OnPlayerExited += HandlePlayerExited;
    }

    void Start()
    {
        // StageFlowSystem에 자동 등록
        if (StageFlowSystem.Instance != null)
            StageFlowSystem.Instance.Register(this);
        else
            Debug.LogWarning($"[RoomStageController] StageFlowSystem.Instance가 없습니다. ({gameObject.name})");
    }

    void OnDestroy()
    {
        if (exitTrigger != null)
            exitTrigger.OnPlayerExited -= HandlePlayerExited;

        StageFlowSystem.Instance?.Unregister(this);
    }

    // ── 트리거 진입 (방 입장) ─────────────────────────────

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
        SetBlockers(entranceBlockers, true);

        if (enemies.Count == 0)
        {
            HandleCleared();
            return;
        }

        SetEnemiesActive(true);
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
                HandleCleared();
                yield break;
            }
        }
    }

    private bool AllEnemiesDead()
    {
        foreach (var e in enemies)
        {
            if (e == null) continue;
            var hc = e.GetComponent<HealthComponent>();
            if (hc != null && hc.IsAlive) return false;
            if (hc == null && e.activeInHierarchy) return false;
        }
        return true;
    }

    // ── 클리어 처리 ───────────────────────────────────────

    private void HandleCleared()
    {
        if (IsCleared) return;
        IsCleared = true;

        SetBlockers(exitBlockers, false);
        LogManager.AddLog("전투 종료! 앞으로 나아갈 수 있다.");

        OnCleared?.Invoke(this);
    }

    // ── 출구 통과 처리 ────────────────────────────────────

    private void HandlePlayerExited()
    {
        if (!IsCleared) return;
        Debug.Log("[RoomStageController] Invoking OnExited.");
        OnExited?.Invoke(this);
    }

    // ── 야영 ──────────────────────────────────────────────

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
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = IsCleared ? Color.green : IsActivated ? Color.red : Color.cyan;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
