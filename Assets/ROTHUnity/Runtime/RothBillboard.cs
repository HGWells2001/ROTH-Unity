using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>Runtime Y-axis billboard used by original ROTH sprite objects.</summary>
    [DisallowMultipleComponent]
    public sealed class RothBillboard : MonoBehaviour
    {
        public Camera TargetCamera;
        public bool LockYAxis = true;

        private void LateUpdate()
        {
            Camera cam = TargetCamera != null ? TargetCamera : Camera.main;
            if (cam == null) return;
            Vector3 toCamera = cam.transform.position - transform.position;
            if (LockYAxis) toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.000001f) return;
            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
    }
}
