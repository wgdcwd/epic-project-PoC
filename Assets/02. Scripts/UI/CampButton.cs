using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬에 하나만 존재하는 야영 버튼 UI 컴포넌트.
/// StageFlowSystem이 정산 완료 후 Activate(room)을 호출해 버튼을 표시한다.
/// 클릭 시 현재 등록된 RoomStageController.TryCamp()에 위임.
/// 야영 시작 후 자동으로 비활성화.
///
/// StageFlowSystem이 인스펙터에서 이 컴포넌트를 참조한다.
/// (CampButton은 StageFlowSystem을 모른다 — 단방향 의존)
/// </summary>
public sealed class CampButton : MonoBehaviour
{
    [SerializeField] private Button     button;
    [SerializeField] private GameObject buttonObj; // 표시/숨김할 오브젝트
    [SerializeField] private CampSystem  campSystem;

    private RoomStageController _currentRoom;

    void Awake()
    {
        if (button != null)
            button.onClick.AddListener(OnClick);

        SetVisible(false);
    }

    /// <summary>
    /// StageFlowSystem이 정산 완료 후 호출.
    /// 해당 방을 현재 방으로 등록하고 버튼을 표시한다.
    /// </summary>
    public void Activate(RoomStageController room)
    {
        _currentRoom = room;
        SetVisible(true);
    }

    /// <summary>야영 완료 또는 사용 불가 시 버튼을 숨긴다.</summary>
    public void Deactivate()
    {
        _currentRoom = null;
        SetVisible(false);
    }

    private void OnClick()
    {
        if (_currentRoom == null) return;
        if (campSystem == null)
        {
            Debug.LogWarning("[CampButton] CampSystem이 연결되지 않았습니다.");
            return;
        }

        bool success = _currentRoom.TryCamp();
        if (success)
        {
            Deactivate();
            campSystem.StartCamp();
        }
    }

    private void SetVisible(bool visible)
    {
        if (buttonObj != null) buttonObj.SetActive(visible);
        else if (button != null) button.gameObject.SetActive(visible);
    }
}
