using UnityEngine;

namespace FrankaVR.V6
{
    /// <summary>
    /// Navegacion de depuracion para PC. No reemplaza el control del robot:
    /// usa flechas + RePag/AvPag para mover el rig/cuerpo virtual y boton derecho + mouse para mirar.
    /// En Android se desactiva automaticamente.
    /// </summary>
    public class DesktopNavigatorV6 : MonoBehaviour
    {
        public Camera PlayerCamera;
        public Transform RigRoot;
        public bool EnableOnDesktop = true;

        [Header("Movimiento")]
        public float MoveSpeed = 1.8f;
        public float FastMultiplier = 2.5f;
        public KeyCode UpKey = KeyCode.UpArrow;
        public KeyCode DownKey = KeyCode.DownArrow;
        public KeyCode LeftKey = KeyCode.LeftArrow;
        public KeyCode RightKey = KeyCode.RightArrow;
        public KeyCode AscendKey = KeyCode.PageUp;
        public KeyCode DescendKey = KeyCode.PageDown;

        [Header("Mirada")]
        public float MouseSensitivity = 2.1f;
        public float MinPitch = -70f;
        public float MaxPitch = 75f;

        float _yaw;
        float _pitch;

        void Start()
        {
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            if (RigRoot == null) ResolveRigRoot();

            if (PlayerCamera != null)
            {
                Vector3 e = PlayerCamera.transform.localEulerAngles;
                _yaw = Normalize(e.y);
                _pitch = Normalize(e.x);
            }
        }

        void ResolveRigRoot()
        {
            var viewpoints = FindObjectOfType<ViewpointManager>();
            if (viewpoints != null)
            {
                viewpoints.EnsureRigRoot();
                RigRoot = viewpoints.rigRoot;
            }
            if (RigRoot == null && PlayerCamera != null && PlayerCamera.transform.parent != null)
                RigRoot = PlayerCamera.transform.parent;
            if (RigRoot == null && PlayerCamera != null)
                RigRoot = PlayerCamera.transform;
        }

        void Update()
        {
            if (!EnableOnDesktop || Application.platform == RuntimePlatform.Android) return;
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            if (RigRoot == null) ResolveRigRoot();
            if (PlayerCamera == null || RigRoot == null) return;

            MoveRig();
            RotateLook();
        }

        void MoveRig()
        {
            Vector3 move = Vector3.zero;
            if (Input.GetKey(UpKey)) move += Vector3.forward;
            if (Input.GetKey(DownKey)) move += Vector3.back;
            if (Input.GetKey(LeftKey)) move += Vector3.left;
            if (Input.GetKey(RightKey)) move += Vector3.right;
            if (Input.GetKey(AscendKey)) move += Vector3.up;
            if (Input.GetKey(DescendKey)) move += Vector3.down;
            if (move.sqrMagnitude < 1e-6f) return;

            float speed = MoveSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? FastMultiplier : 1f);

            Vector3 camForward = PlayerCamera.transform.forward;
            Vector3 camRight = PlayerCamera.transform.right;
            camForward.y = 0f; camRight.y = 0f;
            camForward = camForward.sqrMagnitude > 1e-6f ? camForward.normalized : Vector3.forward;
            camRight = camRight.sqrMagnitude > 1e-6f ? camRight.normalized : Vector3.right;

            Vector3 world = camForward * move.z + camRight * move.x + Vector3.up * move.y;
            RigRoot.position += world * speed * Time.deltaTime;
        }

        void RotateLook()
        {
            if (!Input.GetMouseButton(1)) return;
            _yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * MouseSensitivity;
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
