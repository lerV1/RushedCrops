using RushedCrops.Player;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float jumpImpulse = 6f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 1.2f;
    [SerializeField] private LayerMask groundLayers;


    private Rigidbody rb;
    private PlayerLook look;
    private Vector2 moveInput;
    private bool jumpRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        look = GetComponent<PlayerLook>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        sprintAction.action.Enable();
        jumpAction.action.Enable();

        jumpAction.action.performed += OnJump;

    }
    private void OnDisable()
    {
        jumpAction.action.performed -= OnJump;

        moveAction.action.Disable();
        sprintAction.action.Disable();
        jumpAction.action.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        moveInput = moveAction.action.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // Walk where the camera faces. Without a PlayerLook, use the body's own facing.
        float yaw = look != null ? look.Yaw : transform.eulerAngles.y;
        Vector3 direction = MoveMath.GetMoveDirection(moveInput, yaw);
        float speed = moveSpeed;

        if (sprintAction.action.IsPressed())
        {
            speed *= sprintMultiplier;
        }
        
        Vector3 velocity = direction * speed;
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);

        if (jumpRequested && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);
        }
        jumpRequested = false;
    }
    private void OnJump(InputAction.CallbackContext context)
    {
        jumpRequested = true;
    }
    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayers);
    }
}
