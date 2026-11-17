using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Mejora visual simple para el entorno: hace que paneles y carteles en mundo miren a la camara
    /// solo sobre el eje Y, evitando texto torcido en VR/mobile.
    /// </summary>
    public class WorldBillboardY : MonoBehaviour
    {
        public Camera TargetCamera;
        public bool   InvertForward = true;
        public float  YOffsetDeg = 0f;

        void LateUpdate()
        {
            Camera cam = TargetCamera != null ? TargetCamera : Camera.main;
            if (cam == null) return;

            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-6f) return;

            Quaternion q = Quaternion.LookRotation(InvertForward ? -toCam.normalized : toCam.normalized, Vector3.up);
            if (Mathf.Abs(YOffsetDeg) > 0.001f) q *= Quaternion.Euler(0f, YOffsetDeg, 0f);
            transform.rotation = q;
        }
    }
}
