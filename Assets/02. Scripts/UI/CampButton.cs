using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 야영 버튼. RoomStageController.TryCamp()에 위임.
/// 버튼 오브젝트 자체를 RoomStageController.campButtonObj에 연결하고,
/// 이 컴포넌트를 같은 오브젝트에 붙인다.
/// </summary>
public sealed class CampButton : MonoBehaviour
{
    [Tooltip("이 버튼이 속한 방의 RoomStageController")]
    [SerializeField] private RoomStageController room;

    [SerializeField] private Button button;

    void Awake()
    {
        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (room == null)
        {
            Debug.LogWarning("[CampButton] RoomStageController가 연결되지 않았습니다.");
            return;
        }
        room.TryCamp();
    }
}
