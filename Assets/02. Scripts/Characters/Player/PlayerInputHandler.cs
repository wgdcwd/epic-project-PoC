using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System을 코드에서 직접 생성하여 처리.
/// </summary>
public sealed class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }

    /// <summary>현재 프레임 마우스 월드 좌표</summary>
    public Vector2 MouseWorldPos => GetMouseWorldPos();

    public event Action<Vector2> OnFirePressed;      // 발사 목표 월드 위치
    public event Action<Vector2> OnInteractPressed;  // 상호작용 목표 월드 위치
    public event Action          OnRestPressed;

    private InputAction _moveAction;
    private InputAction _fireAction;
    private InputAction _interactAction;
    private InputAction _restAction;
    private Camera      _cam;

    void Awake()
    {
        _cam = Camera.main;

        _moveAction = new InputAction("Move", InputActionType.Value);
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up",    "<Keyboard>/w")
            .With("Up",    "<Keyboard>/upArrow")
            .With("Down",  "<Keyboard>/s")
            .With("Down",  "<Keyboard>/downArrow")
            .With("Left",  "<Keyboard>/a")
            .With("Left",  "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");

        _fireAction = new InputAction("Fire", InputActionType.Button);
        _fireAction.AddBinding("<Mouse>/leftButton");

        _interactAction = new InputAction("Interact", InputActionType.Button);
        _interactAction.AddBinding("<Mouse>/rightButton");

        _restAction = new InputAction("Rest", InputActionType.Button);
        _restAction.AddBinding("<Keyboard>/r");

        _fireAction.performed += ctx => {
            Debug.Log("[Input] Fire pressed");
            OnFirePressed?.Invoke(GetMouseWorldPos());
        };
        _interactAction.performed += ctx => {
            Debug.Log("[Input] Interact pressed");
            OnInteractPressed?.Invoke(GetMouseWorldPos());
        };
        _restAction.performed += ctx => {
            Debug.Log("[Input] Rest pressed");
            OnRestPressed?.Invoke();
        };
    }

    void OnEnable()
    {
        _moveAction.Enable();
        _fireAction.Enable();
        _interactAction.Enable();
        _restAction.Enable();
    }

    void OnDisable()
    {
        _moveAction.Disable();
        _fireAction.Disable();
        _interactAction.Disable();
        _restAction.Disable();
    }

    void OnDestroy()
    {
        _moveAction.Dispose();
        _fireAction.Dispose();
        _interactAction.Dispose();
        _restAction.Dispose();
    }

    void Update() => MoveInput = _moveAction.ReadValue<Vector2>();

    private Vector2 GetMouseWorldPos()
    {
        if (_cam == null) _cam = Camera.main;
        Vector3 screen = Mouse.current.position.ReadValue();
        screen.z = -_cam.transform.position.z;
        return _cam.ScreenToWorldPoint(screen);
    }
}
