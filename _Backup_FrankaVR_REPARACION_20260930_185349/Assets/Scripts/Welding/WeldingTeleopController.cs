using UnityEngine;
using FrankaVR.Teleop;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Une ITeleopInput (teclado o guante) con PandaIK.
    /// La cabeza puede mirar libremente: la traslacion del guante usa un marco de CUERPO/rig
    /// separado de la Camera para que mirar a un costado no cambie los ejes de control.
    /// </summary>
    public class WeldingTeleopController : MonoBehaviour
    {
        [Header("Referencias")]
        public MonoBehaviour      InputSource;
        public PandaIK            Ik;
        public WeldingTorch       Torch;
        public WeldingMachine     Machine;
        public WeldingHelmet      Helmet;
        public WeldingTaskManager Task;
        public Camera             ReferenceCamera;
        [Tooltip("Marco horizontal de traslacion. Para Cardboard usar el RigRoot/ViewerRig, NO la Camera.")]
        public Transform          MovementReference;

        [Header("Traslacion")]
        public float MoveSpeed = 0.2f;
        public bool  ReferenceRelative = true;
        [Tooltip("Si falta MovementReference, usa automaticamente el padre de la camara XR.")]
        public bool AutoUseCameraParentAsMovementReference = true;

        [Header("Orientacion")]
        public Vector3 BaseApproach = Vector3.down;
        [Range(0f, 1f)] public float OrientationWeight = 0.5f;
        public float MaxTiltFromVertical = 65f;

        [Header("Proteccion de singularidad")]
        public float MinBaseRadius = 0.22f;
        public float MaxBaseRadius = 0.75f;

        [Header("Limite vertical")]
        public bool ClampHeight = false;
        public float MinHeightOverBase = -0.60f;
        public float MaxHeightOverBase = 0.60f;

        [Header("Ayuda visual")]
        public bool ShowTargetMarker = true;
        public float MarkerRadius = 0.012f;

        [Header("Suavizado")]
        public float PositionSmoothTime = 0.05f;

        [Header("Diagnostico")]
        public bool LogDiagnostics = false;

        [Header("Arranque")]
        public bool AutoStartTask = true;

        ITeleopInput _in;
        string _targetOrigin = "sin inicializar";
        Vector3 _targetPos;
        Vector3 _smoothedPos;
        Vector3 _smoothVel;
        bool _init;
        Transform _marker;

        void Awake()
        {
            _in = InputSource as ITeleopInput;
            if (_in == null) Debug.LogError("WeldingTeleopController: InputSource no implementa ITeleopInput");

            if (ReferenceCamera == null) ReferenceCamera = Camera.main;
            ResolveMovementReference();
        }

        void Start()
        {
            ValidateTcpHierarchy();
            if (ShowTargetMarker) CreateMarker();
            if (AutoStartTask && Task != null) Task.StartTask();
        }

        void ResolveMovementReference()
        {
            if (MovementReference != null || !AutoUseCameraParentAsMovementReference) return;

            // Preferir el rig explicito del gestor de vistas. Esto evita tomar como referencia
            // un padre interno del plugin XR si la Camera ya viene dentro de otra jerarquia.
            ViewpointManager viewpoints = FindObjectOfType<ViewpointManager>();
            if (viewpoints != null)
            {
                viewpoints.EnsureRigRoot();
                if (viewpoints.rigRoot != null)
                {
                    MovementReference = viewpoints.rigRoot;
                    return;
                }
            }

            if (ReferenceCamera == null) ReferenceCamera = Camera.main;
            if (ReferenceCamera != null && ReferenceCamera.transform.parent != null)
                MovementReference = ReferenceCamera.transform.parent;
        }

        void ValidateTcpHierarchy()
        {
            if (Ik == null || Torch == null || Ik.endEffector == null || Torch.Tip == null) return;
            if (!(Torch.Tip == Ik.endEffector || Torch.Tip.IsChildOf(Ik.endEffector)))
                Debug.LogWarning("[Teleop] WeldingTorch.Tip no es hijo de PandaIK.endEffector. Revisar jerarquia, no se modifica automaticamente.");
        }

        void CreateMarker()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TargetMarker";
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.2f, 1f, 0.4f, 1f);
            go.transform.localScale = Vector3.one * (MarkerRadius * 2f);
            _marker = go.transform;
        }

        public void ResetTo(Vector3 startPos)
        {
            _targetOrigin = "TipStart";
            _targetPos = ClampWorkspace(startPos);
            _smoothedPos = _targetPos;
            _smoothVel = Vector3.zero;
            _init = true;
        }

        void Update()
        {
            if (_in == null || Ik == null || Ik.target == null || Ik.endEffector == null) return;
            if (!_init)
            {
                _targetPos = FallbackStart();
                _smoothedPos = _targetPos;
                _init = true;
            }

            ResolveMovementReference();
            Ik.solving = true;
            Ik.orientationWeight = OrientationWeight;

            TeleopCommand cmd = _in.Poll(Ik.endEffector.rotation);

            if (cmd.MoveClutch)
                _targetPos += ToWorldRate(cmd.TranslationRate) * (MoveSpeed * Time.deltaTime);

            _targetPos = ClampWorkspace(_targetPos);
            _smoothedPos = PositionSmoothTime > 0f
                ? Vector3.SmoothDamp(_smoothedPos, _targetPos, ref _smoothVel, PositionSmoothTime)
                : _targetPos;

            Ik.target.position = _smoothedPos;
            if (_marker != null) _marker.position = _smoothedPos;

            Vector3 axis = cmd.ResetOrientation
                ? BaseApproach.normalized
                : cmd.Orientation * BaseApproach.normalized;
            Ik.approachDirection = LimitTilt(axis);

            if (Machine != null)
            {
                if (cmd.CycleElectrode) { Machine.CycleElectrode(); if (Torch) Torch.LoadNewElectrode(); }
                if (cmd.AmperageStep != 0) Machine.StepAmperage(cmd.AmperageStep);
            }
            if (Torch != null) Torch.SetSparkRequested(cmd.SparkHeld);
            if (cmd.ReplaceElectrode && Task != null) Task.ReplaceElectrode();
            if (cmd.ToggleHelmet && Helmet != null) Helmet.ToggleHelmet();

            if (LogDiagnostics) Diagnostics();
        }

        Vector3 ToWorldRate(Vector3 rate)
        {
            if (!ReferenceRelative) return rate;

            Transform refT = MovementReference;
            if (refT == null)
            {
                Camera cam = ReferenceCamera != null ? ReferenceCamera : Camera.main;
                refT = cam != null ? cam.transform : null;
            }
            if (refT == null) return rate;

            Vector3 fwd = refT.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
            Vector3 right = refT.right; right.y = 0f;
            right = right.sqrMagnitude < 1e-4f ? Vector3.right : right.normalized;

            return right * rate.x + Vector3.up * rate.y + fwd * rate.z;
        }

        Vector3 ClampWorkspace(Vector3 p)
        {
            if (Ik == null) return p;
            Vector3 basePos = Ik.transform.position;
            Vector3 d = p - basePos;
            float h = d.y;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float r = flat.magnitude;

            if (r < 1e-4f) { flat = Vector3.right * MinBaseRadius; r = MinBaseRadius; }
            if (r < MinBaseRadius) flat = flat.normalized * MinBaseRadius;
            if (r > MaxBaseRadius) flat = flat.normalized * MaxBaseRadius;
            if (ClampHeight) h = Mathf.Clamp(h, MinHeightOverBase, MaxHeightOverBase);

            return basePos + new Vector3(flat.x, h, flat.z);
        }

        Vector3 LimitTilt(Vector3 axis)
        {
            Vector3 a = axis.normalized;
            if (a.sqrMagnitude < 1e-4f) return Vector3.down;

            float angleFromDown = Vector3.Angle(a, Vector3.down);
            if (angleFromDown <= MaxTiltFromVertical) return a;

            Vector3 flat = new Vector3(a.x, 0f, a.z);
            if (flat.sqrMagnitude < 1e-6f) return Vector3.down;
            flat.Normalize();

            float rad = MaxTiltFromVertical * Mathf.Deg2Rad;
            return (flat * Mathf.Sin(rad) + Vector3.down * Mathf.Cos(rad)).normalized;
        }

        Vector3 FallbackStart()
        {
            if (Task != null && Task.Seam != null && Task.Seam.Count > 0)
            {
                _targetOrigin = "inicio de costura";
                return ClampWorkspace(Task.Seam.PointAt(0) + Vector3.up * 0.04f);
            }
            _targetOrigin = "punta actual";
            return ClampWorkspace(Ik.endEffector.position);
        }

        float _diagTimer;
        void Diagnostics()
        {
            _diagTimer += Time.deltaTime;
            if (_diagTimer < 0.5f) return;
            _diagTimer = 0f;

            Vector3 basePos = Ik.transform.position;
            Vector3 d = _smoothedPos - basePos;
            float radius = new Vector3(d.x, 0f, d.z).magnitude;
            float tipErr = (Ik.endEffector.position - _smoothedPos).magnitude;
            Debug.Log("[Teleop] radio=" + radius.ToString("0.000") +
                      " err=" + tipErr.ToString("0.000") +
                      " marco=" + (MovementReference != null ? MovementReference.name : "Camera"));
        }
    }
}
