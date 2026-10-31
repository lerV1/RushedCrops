using UnityEngine;
using UnityEngine.InputSystem;

namespace RushedCrops.Player
{
    /// <summary>
    /// First-person look with the mouse or right stick.
    /// Yaw and pitch are applied to <see cref="cameraRoot"/>, not to the player body. The body is a
    /// Rigidbody with interpolation, and rotating it by hand every frame fights the interpolation and
    /// makes the camera jitter. Movement reads <see cref="Yaw"/> to walk where the camera faces.
    /// </summary>
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

        [Header("Cursor")]
        [SerializeField] private bool lockCursor = true;

        private float yaw;
        private float pitch;

        /// <summary>World-space facing in degrees around the Y axis.</summary>
        public float Yaw => yaw;

        /// <summary>Up/down angle in degrees. Negative looks up.</summary>
        public float Pitch => pitch;

        private void Awake()
        {
            if (lookAction == null || cameraRoot == null)
            {
                Debug.LogError($"{nameof(PlayerLook)} on '{name}' needs a Look action and a camera root.", this);
                enabled = false;
                return;
            }

            // Start facing whichever way the player was placed in the scene.
            yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            lookAction.action.Enable();
            if (lockCursor)
            {
                SetCursorLocked(true);
            }
        }

        private void OnDisable()
        {
            lookAction.action.Disable();
            if (lockCursor)
            {
                SetCursorLocked(false);
            }
        }

        private void Update()
        {
            InputAction action = lookAction.action;
            Vector2 rawInput = action.ReadValue<Vector2>();
            bool fromMouse = action.activeControl?.device is Pointer;

            Vector2 delta = LookMath.ScaleLookInput(rawInput, fromMouse, mouseSensitivity, stickSensitivity, Time.deltaTime);
            LookMath.ApplyLook(ref yaw, ref pitch, delta, minPitch, maxPitch, invertY);

            cameraRoot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
