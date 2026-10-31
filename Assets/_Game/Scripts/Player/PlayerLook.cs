using UnityEngine;
using UnityEngine.InputSystem;

// First-person look with the mouse or right stick.
// Turns the camera holder instead of the Rigidbody: rotating an interpolated
// Rigidbody by hand every frame fights the interpolation and makes the view jitter.
public class PlayerLook : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference lookAction;

    [Header("Camera")]
    [Tooltip("Child of the player at eye height that holds the camera.")]
    [SerializeField] private Transform cameraRoot;

    [Header("Sensitivity")]
    [Tooltip("Degrees turned per pixel of mouse movement.")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [Tooltip("Degrees turned per second with the stick pushed all the way.")]
    [SerializeField] private float stickSensitivity = 180f;
    [SerializeField] private bool invertY;

    [Header("Pitch Limits")]
    [Tooltip("How far up the player can look. Negative is up.")]
    [SerializeField, Range(-90f, 0f)] private float minPitch = -85f;
    [Tooltip("How far down the player can look.")]
    [SerializeField, Range(0f, 90f)] private float maxPitch = 85f;

    private float yaw;
    private float pitch;

    // Facing around the Y axis in degrees. Movement uses this so W walks where the camera points.
    public float Yaw => yaw;

    private void Awake()
    {
        // Start facing whichever way the player was placed in the scene.
        yaw = transform.eulerAngles.y;
    }

    private void OnEnable()
    {
        lookAction.action.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        lookAction.action.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        Vector2 input = lookAction.action.ReadValue<Vector2>();

        // Mouse delta is already "per frame". The stick is a direction, so scale it by frame time.
        bool fromMouse = lookAction.action.activeControl?.device is Pointer;
        Vector2 delta = fromMouse
            ? input * mouseSensitivity
            : input * (stickSensitivity * Time.deltaTime);

        yaw = Mathf.Repeat(yaw + delta.x, 360f);
        pitch = Mathf.Clamp(pitch + (invertY ? delta.y : -delta.y), minPitch, maxPitch);

        cameraRoot.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
