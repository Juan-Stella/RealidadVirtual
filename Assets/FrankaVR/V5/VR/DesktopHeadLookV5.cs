using UnityEngine;

namespace FrankaVR.V5
{
    /// <summary>
    /// Emula en PC el movimiento de cabeza: boton derecho + mouse rota SOLO la camara,
    /// nunca el rig/cuerpo usado como referencia del guante.
    /// En Android se desactiva automaticamente.
    /// </summary>
    public class DesktopHeadLookV5 : MonoBehaviour
    {
        public Camera PlayerCamera;
        public bool EnabledOnDesktop = true;
        public float Sensitivity = 2.0f;
        public float MinPitch = -65f;
        public float MaxPitch = 70f;

        float _yaw, _pitch;

        void Start()
        {
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            if (PlayerCamera != null)
            {
                Vector3 e = PlayerCamera.transform.localEulerAngles;
                _yaw = Normalize(e.y); _pitch = Normalize(e.x);
            }
        }

        void LateUpdate()
        {
            if (!EnabledOnDesktop || Application.platform == RuntimePlatform.Android || PlayerCamera == null) return;
            if (!Input.GetMouseButton(1)) return;

            _yaw += Input.GetAxis("Mouse X") * Sensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * Sensitivity;
            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            PlayerCamera.transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        static float Normalize(float a)
        {
            while (a > 180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }
    }
}
