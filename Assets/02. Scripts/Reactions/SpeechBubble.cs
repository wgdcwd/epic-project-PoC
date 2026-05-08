using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 말풍선 프리팹 컴포넌트. BubbleManager가 생성/반환.
/// Canvas: World Space, TMP 텍스트 포함 필요.
/// </summary>
public sealed class SpeechBubble : MonoBehaviour
{
    [SerializeField] private TextMeshPro tmpText;
    [SerializeField] private float       displayDuration = 3.5f;
    [SerializeField] private float       fadeTime        = 0.5f;

    private Transform _target;
    private Vector3   _offset = new(0f, 1.5f, 0f);

    public Transform Target => _target;

    public void Show(Transform target, string text)
    {
        if (this == null) return;          // 자신이 destroyed면 무시
        if (target == null) return;

        _target = target;
        if (tmpText != null)
        {
            tmpText.text  = $"\"{text}\"";
            Color c = tmpText.color; c.a = 1f;
            tmpText.color = c;
        }
        transform.position = target.position + _offset;
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(LifetimeRoutine());
    }

    void LateUpdate()
    {
        if (_target != null)
            transform.position = _target.position + _offset;
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(displayDuration);

        // 페이드 아웃
        if (tmpText != null)
        {
            float elapsed = 0f;
            Color c = tmpText.color;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
                tmpText.color = c;
                yield return null;
            }
        }

        BubbleManager.Instance?.ReturnBubble(this);
    }
}
