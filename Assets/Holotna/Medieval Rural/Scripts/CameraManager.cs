using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Holotna
{
    public class CameraManager : MonoBehaviour
    {
        [SerializeField] private bool _focusOnEnable;
        [SerializeField] private float _acceleration;
        [SerializeField] private float _dampingCoefficient;
        [SerializeField] private float _sprintMultiplier;
        [SerializeField] private float _lookSensitivity;

        private Vector3 _currentVelocity;
        private float _yaw;
        private float _pitch;

        private bool Focused
        {
            get => Cursor.lockState == CursorLockMode.Locked;
            set
            {
                Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !value;
            }
        }

        private void OnEnable()
        {
            if (_focusOnEnable)
            {
                Focused = true;
            }

            _yaw = transform.rotation.eulerAngles.y;
            _pitch = transform.rotation.eulerAngles.x;
        }

        private void OnDisable()
        {
            Focused = false;
        }

        private void Update()
        {
            if (Focused)
            {
                UpdateInput();
            }
            else if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Focused = true;
            }

            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, _dampingCoefficient * Time.deltaTime);
            transform.position += _currentVelocity * Time.deltaTime;
        }

        private void UpdateInput()
        {
            _currentVelocity += GetAccelerationVector() * Time.deltaTime;
            
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            mouseDelta *= _lookSensitivity;
            _yaw += mouseDelta.x;
            _pitch -= mouseDelta.y;
            _pitch = Mathf.Clamp(_pitch, -90f, 90f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Focused = false;
            }
        }

        private Vector3 GetAccelerationVector()
        {
            Vector3 moveInput = default;

            if (Keyboard.current.wKey.isPressed) moveInput += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) moveInput += Vector3.back;
            if (Keyboard.current.dKey.isPressed) moveInput += Vector3.right;
            if (Keyboard.current.aKey.isPressed) moveInput += Vector3.left;
            if (Keyboard.current.eKey.isPressed) moveInput += Vector3.up;
            if (Keyboard.current.qKey.isPressed) moveInput += Vector3.down;

            Vector3 direction = transform.TransformVector(moveInput.normalized);

            if (Keyboard.current.leftShiftKey.isPressed)
            {
                return direction * (_acceleration * _sprintMultiplier);
            }

            return direction * _acceleration;
        }

    }
}