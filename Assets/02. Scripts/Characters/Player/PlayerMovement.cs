using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D       _rb;
    private PlayerInputHandler _input;

    void Awake()
    {
        _rb    = GetComponent<Rigidbody2D>();
        _rb.gravityScale    = 0f;
        _rb.freezeRotation  = true;
        _input = GetComponent<PlayerInputHandler>();
    }

    void FixedUpdate()
    {
        Vector2 vel = _input.MoveInput.normalized * moveSpeed;
        _rb.linearVelocity = vel;
    }
}
