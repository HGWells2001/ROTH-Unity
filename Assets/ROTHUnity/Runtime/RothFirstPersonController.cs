using UnityEngine;

namespace ROTHUnity.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RothFirstPersonController : MonoBehaviour
    {
        public float MoveSpeed = 4.5f;
        public float MouseSensitivity = 2.0f;
        public float Gravity = 18f;
        public float JumpSpeed = 5f;
        public bool CaptureMouse = true;
        public Transform View;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _pitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (View == null && Camera.main != null) View = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (CaptureMouse) SetCursor(true);
        }

        private void OnDisable()
        {
            if (CaptureMouse) SetCursor(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursor(false);
            if (Input.GetMouseButtonDown(0) && CaptureMouse) SetCursor(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float yaw = Input.GetAxisRaw("Mouse X") * MouseSensitivity;
                float pitch = Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
                transform.Rotate(0f, yaw, 0f, Space.Self);
                _pitch = Mathf.Clamp(_pitch - pitch, -89f, 89f);
                if (View != null) View.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            Vector3 planar = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f) * MoveSpeed;

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (Input.GetButtonDown("Jump")) _verticalVelocity = JumpSpeed;
            }
            else _verticalVelocity -= Gravity * Time.deltaTime;

            Vector3 velocity = planar + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private static void SetCursor(bool capture)
        {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }
    }
}
