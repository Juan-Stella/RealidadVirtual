using UnityEngine;
using FrankaVR.Teleop;

namespace FrankaVR.V6
{
    /// <summary>
    /// Boton MOVERSE: el operario se acomoda alrededor de la pieza.
    /// Activo: mirar el piso y tocar la pantalla (OK en el mando, clic en la PC) lleva hasta ahi;
    /// la palanca del mando o WASD caminan. Mientras tanto la torcha no se mueve ni graba.
    /// </summary>
    public class OperatorMoveV6 : MonoBehaviour
    {
        public static bool Active { get; private set; }
        static OperatorMoveV6 _instance;

        public Camera PlayerCamera;
        public ViewpointManager Viewpoints;
        [Tooltip("Hasta donde se puede ir, medido desde la base del robot (m).")]
        public float MaxRadius = 3.5f;
        [Tooltip("Distancia minima a la base del robot (m).")]
        public float MinRadius = 0.8f;
        public float WalkSpeed = 1.0f;

        Transform _marker;
        Material _markerMat;
        Transform _robotBase;
        HandControllerInput _hand;
        GazeInteractor _gaze;
        float _floorY, _nextSearch;
        bool _hasTarget;
        Vector3 _target;

        public static OperatorMoveV6 Ensure(Camera cam, ViewpointManager viewpoints)
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<OperatorMoveV6>();
                if (_instance == null) _instance = new GameObject("OperatorMoveV6_Runtime").AddComponent<OperatorMoveV6>();
            }
            _instance.PlayerCamera = cam;
            _instance.Viewpoints = viewpoints;
            return _instance;
        }

        public void Toggle() { Active = !Active; }

        // OK del mando sin boton mirado: ir al punto marcado.
        public static bool ConfirmFromController() => Active && _instance != null && _instance.Jump();

        void Start()
        {
            Renderer floor = GameObject.Find("Piso")?.GetComponent<Renderer>();
            _floorY = floor != null ? floor.bounds.max.y : 0f;
            PandaArm arm = FindAnyObjectByType<PandaArm>();
            if (arm != null) _robotBase = arm.transform;
            BuildMarker();
        }

        void OnDestroy() { if (_instance == this) { _instance = null; Active = false; } }

        void Update()
        {
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            if (Input.GetKeyDown(KeyCode.F2)) Toggle(); // PC: atajo del boton MOVERSE
            bool show = Active && PlayerCamera != null;
            _hasTarget = show && FindTarget(out _target);
            if (_marker != null)
            {
                _marker.gameObject.SetActive(_hasTarget);
                if (_hasTarget) _marker.position = _target + Vector3.up * 0.01f;
            }
            if (!show) return;

            if (_hand == null && Time.unscaledTime >= _nextSearch)
            { _nextSearch = Time.unscaledTime + 1f; _hand = FindAnyObjectByType<HandControllerInput>(); }
            if (_gaze == null) _gaze = FindAnyObjectByType<GazeInteractor>();
            bool overButton = _gaze != null && _gaze.Current != null;

            bool tap = !overButton && Input.GetMouseButtonDown(0) && Application.platform != RuntimePlatform.Android;
#if UNITY_ANDROID && !UNITY_EDITOR
            tap |= !overButton && Google.XR.Cardboard.Api.IsTriggerPressed;
#endif
            if (tap) Jump();

            Vector2 walk = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) walk.y += 1f;
            if (Input.GetKey(KeyCode.S)) walk.y -= 1f;
            if (Input.GetKey(KeyCode.D)) walk.x += 1f;
            if (Input.GetKey(KeyCode.A)) walk.x -= 1f;
            if (_hand != null && _hand.Connected && _hand.Stick.sqrMagnitude > 0.02f) walk = _hand.Stick;
            if (walk.sqrMagnitude > 0f) Walk(Vector2.ClampMagnitude(walk, 1f) * WalkSpeed * Time.deltaTime);
        }

        bool FindTarget(out Vector3 point)
        {
            point = Vector3.zero;
            Ray ray = Application.platform == RuntimePlatform.Android
                ? new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward)
                : PlayerCamera.ScreenPointToRay(Input.mousePosition);
            if (ray.direction.y > -0.05f) return false; // hay que mirar hacia el piso
            float t = (_floorY - ray.origin.y) / ray.direction.y;
            if (t <= 0f || t > 8f) return false;
            point = ray.origin + ray.direction * t;
            // Algo en el medio (mesa, robot, cerco) tapa el piso: no se puede ir.
            if (Physics.Raycast(ray, out RaycastHit hit, t - 0.05f, ~(1 << 5), QueryTriggerInteraction.Ignore)) return false;
            return Allowed(point);
        }

        bool Allowed(Vector3 p)
        {
            if (_robotBase == null) return true;
            Vector3 d = p - _robotBase.position; d.y = 0f;
            return d.magnitude >= MinRadius && d.magnitude <= MaxRadius;
        }

        bool Jump()
        {
            if (!_hasTarget) return false;
            Transform rig = Rig();
            if (rig == null) return false;
            Vector3 cam = PlayerCamera.transform.position;
            // A la altura de ojos de la vista del operario (desde la cenital no se queda en el aire).
            float eye = Viewpoints != null && Viewpoints.stations.Count > 0 && Viewpoints.stations[0].anchor != null
                ? Viewpoints.stations[0].anchor.position.y : _floorY + 1.6f;
            rig.position += new Vector3(_target.x - cam.x, eye - cam.y, _target.z - cam.z);
            return true;
        }

        void Walk(Vector2 step)
        {
            Transform rig = Rig();
            if (rig == null) return;
            Vector3 f = PlayerCamera.transform.forward; f.y = 0f;
            f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 move = f * step.y + r * step.x;
            Vector3 next = PlayerCamera.transform.position + move;
            next.y = _floorY;
            if (Allowed(next)) rig.position += move;
        }

        Transform Rig()
        {
            if (Viewpoints != null) { Viewpoints.EnsureRigRoot(); if (Viewpoints.rigRoot != null) return Viewpoints.rigRoot; }
            return PlayerCamera != null ? PlayerCamera.transform.parent : null;
        }

        void BuildMarker()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "MarcaMoverse";
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.45f, 0.005f, 0.45f);
            Shader sh = Resources.Load<Shader>("FrankaVRUIPanel");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            Renderer r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (sh != null) { _markerMat = new Material(sh); _markerMat.color = new Color(0.15f, 0.85f, 0.95f, 0.55f); r.sharedMaterial = _markerMat; }
            _marker = go.transform;
            go.SetActive(false);
        }
    }
}
