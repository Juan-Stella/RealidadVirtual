using UnityEngine;
using FrankaVR.Teleop;

namespace FrankaVR.Welding
{
    /// Une la fuente de teleoperacion (teclado/guante) con el IK existente (PandaIK),
    /// la maquina de soldar y el soldador.
    ///
    /// IMPORTANTE sobre como controla la orientacion PandaIK:
    /// el solver NO usa target.rotation. Restringe la DIRECCION del eje de herramienta
    /// contra el vector de mundo 'approachDirection' (2 GDL, dejando libre el giro sobre
    /// el propio eje). Por eso aca la orientacion se escribe en Ik.approachDirection,
    /// no en la rotacion del objetivo.
    ///
    /// Traslacion: control por VELOCIDAD, relativo a la camara.
    /// W/S = lejos/cerca de la vista, A/D = izq/der en pantalla, Q/E = abajo/arriba.
    public class WeldingTeleopController : MonoBehaviour
    {
        [Header("Referencias (arrastrar en Inspector)")]
        public MonoBehaviour      InputSource; // debe implementar ITeleopInput
        public PandaIK            Ik;
        public WeldingTorch       Torch;
        public WeldingMachine     Machine;
        public WeldingHelmet      Helmet;
        public WeldingTaskManager Task;
        [Tooltip("Camara de referencia para el movimiento. Vacio = Camera.main.")]
        public Camera             ReferenceCamera;

        [Header("Traslacion")]
        public float MoveSpeed = 0.2f;   // m/s a rate=1
        public bool  CameraRelative = true;

        [Header("Orientacion")]
        [Tooltip("Direccion inicial del electrodo, en el mundo. Vector3.down = apuntando " +
                 "a la chapa. Las teclas de orientacion inclinan A PARTIR de aca, no de la " +
                 "pose arbitraria en la que haya cargado el brazo.")]
        public Vector3 BaseApproach = Vector3.down;
        [Tooltip("Peso de la alineacion en el IK. 0.5 anda bien; bajarlo si el brazo forcejea.")]
        [Range(0f, 1f)] public float OrientationWeight = 0.5f;
        [Tooltip("Inclinacion maxima del electrodo respecto a la vertical, en grados. " +
                 "Evita pedirle al brazo poses imposibles.")]
        public float MaxTiltFromVertical = 65f;

        [Header("Proteccion de singularidad")]
        [Tooltip("El objetivo no puede acercarse mas que esto al eje vertical de la base. " +
                 "Debajo de este radio joint1 queda indefinido y oscila entre sus topes.")]
        public float MinBaseRadius = 0.22f;
        [Tooltip("Radio maximo respecto a la base, para no pedir puntos fuera de alcance.")]
        public float MaxBaseRadius = 0.75f;
        [Tooltip("Altura minima y maxima del objetivo respecto a la BASE del robot.")]
        public float MinHeightOverBase = -0.10f;
        public float MaxHeightOverBase = 0.60f;

        [Header("Ayuda visual")]
        [Tooltip("Dibuja una esfera chica en el objetivo para ver que se esta comandando.")]
        public bool ShowTargetMarker = true;
        public float MarkerRadius = 0.012f;

        [Header("Suavizado anti-ruido")]
        [Tooltip("Filtra picos de un solo frame (framerate irregular, ruido del solver fisico) " +
                 "antes de mandarle el objetivo al IK. 0 = sin suavizado. 0.04-0.08 recomendado: " +
                 "mata el ruido sin sentirse pesado al controlar.")]
        public float PositionSmoothTime = 0.05f;

        [Header("Diagnostico")]
        [Tooltip("Imprime en la Consola el estado del IK cada medio segundo. " +
                 "Sirve para ver si el problema es alcance, orientacion o el solver.")]
        public bool LogDiagnostics = false;

        [Header("Arranque")]
        [Tooltip("Inicia la orden de trabajo automaticamente al dar Play.")]
        public bool AutoStartTask = true;

        ITeleopInput _in;
        Quaternion _orientRef;   // orientacion del input en el primer frame (referencia cero)
        bool _orientRefSet;
        Vector3 _targetPos;      // objetivo crudo, integrado del input
        Vector3 _smoothedPos;    // lo que realmente se manda al IK
        Vector3 _smoothVel;      // estado interno de SmoothDamp
        bool _init;
        Transform _marker;

        void Awake()
        {
            _in = InputSource as ITeleopInput;
            if (_in == null) Debug.LogError("WeldingTeleopController: InputSource no implementa ITeleopInput");
        }

        void Start()
        {
            if (ShowTargetMarker) CreateMarker();
            if (AutoStartTask && Task != null) Task.StartTask();
        }

        void CreateMarker()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TargetMarker";
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);          // el marcador no debe chocar con nada
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && r.material != null) r.material.color = new Color(0.2f, 1f, 0.4f, 1f);
            go.transform.localScale = Vector3.one * (MarkerRadius * 2f);
            _marker = go.transform;
        }

        public void ResetTo(Vector3 startPos)
        {
            _targetPos = ClampWorkspace(startPos);
            _smoothedPos = _targetPos;
            _smoothVel = Vector3.zero;
            _orientRefSet = false;   // recalibra la orientacion al reiniciar
            _init = true;
        }

        void Update()
        {
            if (_in == null || Ik == null || Ik.target == null || Ik.endEffector == null) return;
            if (!_init) { _targetPos = Ik.endEffector.position; _smoothedPos = _targetPos; _init = true; }

            Ik.solving = true;
            Ik.orientationWeight = OrientationWeight;

            TeleopCommand cmd = _in.Poll(Ik.endEffector.rotation);

            // --- Traslacion (rate control) ---
            if (cmd.MoveClutch)
                _targetPos += ToWorldRate(cmd.TranslationRate) * (MoveSpeed * Time.deltaTime);

            _targetPos = ClampWorkspace(_targetPos);

            // El IK recibe la version suavizada, no el objetivo crudo. Asi un pico de un
            // solo frame (deltaTime irregular, ruido del solver fisico) no llega intacto.
            _smoothedPos = PositionSmoothTime > 0f
                ? Vector3.SmoothDamp(_smoothedPos, _targetPos, ref _smoothVel, PositionSmoothTime)
                : _targetPos;

            Ik.target.position = _smoothedPos;
            if (_marker != null) _marker.position = _smoothedPos;

            // --- Orientacion: se escribe en approachDirection, NO en target.rotation ---
            // El electrodo arranca en BaseApproach (por defecto, apuntando a la chapa) y las
            // teclas/el guante lo inclinan A PARTIR de ahi. Antes se heredaba la rotacion
            // arbitraria en la que hubiera cargado el brazo, lo que dejaba el electrodo
            // torcido de entrada y obligaba a poses imposibles segun la zona de la mesa.
            if (!_orientRefSet) { _orientRef = cmd.Orientation; _orientRefSet = true; }
            Quaternion delta = cmd.Orientation * Quaternion.Inverse(_orientRef);
            Vector3 axis = delta * BaseApproach.normalized;
            Ik.approachDirection = LimitTilt(axis);

            // --- Maquina y soldador ---
            if (Machine != null)
            {
                if (cmd.CycleElectrode)    { Machine.CycleElectrode(); if (Torch) Torch.LoadNewElectrode(); }
                if (cmd.AmperageStep != 0) Machine.StepAmperage(cmd.AmperageStep);
            }
            if (Torch != null) Torch.SetSparkRequested(cmd.SparkHeld);
            if (cmd.ReplaceElectrode && Task != null) Task.ReplaceElectrode();
            if (cmd.ToggleHelmet && Helmet != null)   Helmet.ToggleHelmet();

            if (LogDiagnostics) Diagnostics();
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
            float tilt = Vector3.Angle(Ik.approachDirection, Vector3.down);

            Debug.Log(string.Format(
                "[Teleop] radio={0:0.000}m  alturaSobreBase={1:+0.000;-0.000}m  " +
                "errorPunta={2:0.000}m  inclinacion={3:0.0}deg  approach=({4:0.00},{5:0.00},{6:0.00})",
                radius, d.y, tipErr, tilt,
                Ik.approachDirection.x, Ik.approachDirection.y, Ik.approachDirection.z));
        }

        /// Rate (x=derecha, y=arriba, z=adelante) -> ejes del mundo, segun la camara.
        Vector3 ToWorldRate(Vector3 rate)
        {
            if (!CameraRelative) return rate;
            Camera cam = ReferenceCamera != null ? ReferenceCamera : Camera.main;
            if (cam == null) return rate;

            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
            Vector3 rightv = cam.transform.right; rightv.y = 0f;
            rightv = rightv.sqrMagnitude < 1e-4f ? Vector3.right : rightv.normalized;

            return rightv * rate.x + Vector3.up * rate.y + fwd * rate.z;
        }

        /// Mantiene el objetivo dentro de un anillo alrededor de la base del robot.
        /// El limite INTERIOR es el importante: es lo que evita la singularidad de hombro,
        /// donde joint1 queda indefinido y salta entre -166 y +166 grados.
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

            h = Mathf.Clamp(h, MinHeightOverBase, MaxHeightOverBase);
            return basePos + new Vector3(flat.x, h, flat.z);
        }

        /// Limita cuanto se puede inclinar el electrodo respecto a la vertical.
        Vector3 LimitTilt(Vector3 axis)
        {
            Vector3 a = axis.normalized;
            if (a.sqrMagnitude < 1e-4f) return Vector3.down;

            float angleFromDown = Vector3.Angle(a, Vector3.down);
            if (angleFromDown <= MaxTiltFromVertical) return a;

            // Recomponer: misma direccion horizontal, inclinacion recortada al maximo.
            Vector3 flat = new Vector3(a.x, 0f, a.z);
            if (flat.sqrMagnitude < 1e-6f) return Vector3.down;
            flat = flat.normalized;

            float rad = MaxTiltFromVertical * Mathf.Deg2Rad;
            return (flat * Mathf.Sin(rad) + Vector3.down * Mathf.Cos(rad)).normalized;
        }

        void OnDrawGizmosSelected()
        {
            if (Ik == null) return;
            Vector3 b = Ik.transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(b, MinBaseRadius);   // zona prohibida (singularidad)
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(b, MaxBaseRadius);   // alcance maximo
        }
    }
}
