using System;
using UnityEngine;

/// <summary>
/// 출구 통과 감지 전용 트리거 컴포넌트.
/// 출구 위치에 별도 오브젝트를 만들고 이 컴포넌트와 BoxCollider2D(IsTrigger=ON)를 붙인다.
/// RoomStageController가 OnPlayerExited 이벤트를 구독해 정산 흐름을 시작한다.
///
/// [씬 설정]
/// 1. Room 안에 빈 오브젝트 생성 (이름: ExitTrigger)
/// 2. BoxCollider2D 추가 → Is Trigger = ON, 출구 통로 크기로 설정
/// 3. RoomExitTrigger 컴포넌트 추가
/// 4. RoomStageController의 exitTrigger 필드에 연결
/// </summary>
public sealed class RoomExitTrigger : MonoBehaviour
{
    public event Action OnPlayerExited;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        OnPlayerExited?.Invoke();
    }
}
