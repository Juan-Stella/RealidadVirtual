using System.Collections;
using UnityEngine;
using FrankaVR.Teleop;
using FrankaVR.V5;

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
        [Header("Precision de teclado (Ctrl)")]
        public float FinePositionTolerance = 0.001f;
        public float FineAngleTolerance = 0.5f;
        bool _precisionSolverActive;
        float _normalPositionTolerance, _normalAngleTolerance;

        [Header("Control de punta en PC")]
        public bool ControlPhysicalTip = true;
        public bool TrainingAssistance = true;
        public float AssistedArcGap = 0.00325f;
        public float MaximumManualLead = 0.025f;
        public float ApproachSpeed = 0.08f;

        [Header("Soldadura sobre la costura (teclado)")]
        [Tooltip("Con Espacio cerca de la costura, las teclas avanzan/desplazan sobre la junta.")]
        public bool SeamLockWhileArcing = true;
        public float SeamLockRadius = 0.03f;
        public float WeldTravelSpeed = 0.004f, WeldTravelFast = 0.007f, WeldTravelFine = 0.002f;
        public float WeldLateralSpeed = 0.003f;
        public float ArcAdjustSpeed = 0.002f, ArcLiftSpeed = 0.02f;
        public bool SeamLocked { get; private set; }
        public string SeamHelp { get; private set; } = "";
        public bool HasSeamAngles { get; private set; }
        public float TravelAngleDeg { get; private set; }
        public float WorkAngleDeg { get; private set; }
        public float TravelSpeedMmS { get; private set; }
        float _seamTravel, _seamLateral, _seamFeed, _liftHeld;
        Vector3 _seamRightAxis = Vector3.right, _seamForwardAxis = Vector3.forward, _lastTip;
        bool _hasLastTip, _lastTravelOnForward;
        string _lastAdvance;
        Vector3 _physicalTarget, _physicalSmooth, _physicalVelocity;
        bool _physicalInit;
        CourseTrainingManagerV5 _course;
        KeyboardTeleopInput _keyboard;
        Renderer _logicalMarker;
        bool _logicalMarkerVisible;
        int _approachStage = -1;
        Vector3 _approachLift, _approachPoint, _approachHand;
        float _approachTime;
        readonly RaycastHit[] _motionHits = new RaycastHit[32];
        readonly Collider[] _nearSurfaces = new Collider[32];
        PandaArm _arm;
        Vector3 _stallStart, _blockedDirection, _retreatDestination, _retreatProbe;
        float _stallTime, _retreatTime, _retreatProbeTime;
        public bool MotionBlocked { get; private set; }
        public bool Retracting { get; private set; }
        public bool Approaching => _approachStage >= 0;
        public int ApproachStage => _approachStage;
        public bool UsedTrainingAssistance { get; private set; }
        public bool AssistanceSuspended { get; private set; }
        public string AssistMessage { get; private set; }
        public Vector3 PhysicalTarget => _physicalSmooth;
        public bool AssistanceAvailable => TrainingAssistance && _course != null &&
            _course.Mode == CourseModeV5.Training && _course.Phase == CoursePhaseV5.Running;
        float DesiredArcGap => Machine != null && Machine.Current != null
            ? Mathf.Clamp(Machine.Current.ArcLenIdealMm * 0.001f, 0.0025f, 0.006f) : AssistedArcGap;

        public void SetPrecisionSolver(bool active)
        {
            if (Ik == null || _precisionSolverActive == active) return;
            if (active)
            {
                _normalPositionTolerance = Ik.toleranceMeters;
                _normalAngleTolerance = Ik.toleranceDegrees;
                Ik.toleranceMeters = Mathf.Min(_normalPositionTolerance, FinePositionTolerance);
                Ik.toleranceDegrees = Mathf.Min(_normalAngleTolerance, FineAngleTolerance);
            }
            else
            {
                Ik.toleranceMeters = _normalPositionTolerance;
                Ik.toleranceDegrees = _normalAngleTolerance;
            }
            _precisionSolverActive = active;
        }

        void OnDisable()
        {
            SetPrecisionSolver(false);
            if (_logicalMarker != null) _logicalMarker.enabled = _logicalMarkerVisible;
        }

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

            if (Machine == null && Torch != null) Machine = Torch.Machine;
            _course = FindObjectOfType<CourseTrainingManagerV5>();
            if (Ik != null) _arm = Ik.GetComponent<PandaArm>();
            _keyboard = InputSource is HybridTeleopInput hybrid ? hybrid.Keyboard : InputSource as KeyboardTeleopInput;
            if (Ik != null && Ik.target != null)
            {
                _logicalMarker = Ik.target.GetComponent<Renderer>();
                if (_logicalMarker != null) _logicalMarkerVisible = _logicalMarker.enabled;
            }

            if (ReferenceCamera == null) ReferenceCamera = Camera.main;
            ResolveMovementReference();
        }

        IEnumerator Start()
        {
            ValidateTcpHierarchy();
            if (ShowTargetMarker) CreateMarker();
            // Los generadores preparan pieza y orden en Start. Iniciar despues evita
            // que LoadWorkOrder deje la sesion en Briefing segun el orden de arranque.
            yield return null;
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
            if (c != null) { c.enabled = false; Destroy(c); }
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.2f, 1f, 0.4f, 1f);
            go.transform.localScale = Vector3.one * (MarkerRadius * 2f);
            _marker = go.transform;
        }

        /// startPos es la posicion deseada de la PUNTA del electrodo (no del flange).
        public void ResetTo(Vector3 startPos)
        {
            _targetOrigin = "TipStart";
            _targetPos = ClampWorkspace(startPos - TipOffset());
            _smoothedPos = _targetPos;
            _smoothVel = Vector3.zero;
            _init = true;
            _physicalInit = false;
            _approachStage = -1;
            UsedTrainingAssistance = false;
            AssistanceSuspended = false;
            AssistMessage = "";
            MotionBlocked = Retracting = false;
            _stallTime = 0f;
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

            if (_keyboard != null)
            {
                // Sobre la costura, I/K cambian el angulo de avance y J/L el de trabajo.
                if (SeamLocked) { _keyboard.TiltRight = _seamRightAxis; _keyboard.TiltForward = _seamForwardAxis; }
                else { ViewAxes(out Vector3 f, out Vector3 r); _keyboard.TiltRight = r; _keyboard.TiltForward = f; }
            }
            TeleopCommand cmd = _in.Poll(Ik.endEffector.rotation);
            bool keyboardMotion = _in is KeyboardTeleopInput ||
                (_in is HybridTeleopInput hybrid && !hybrid.GloveMotionActive);
            // Practica: V cerca de la junta pone el angulo recomendado en vez de la vertical.
            if (keyboardMotion && cmd.ResetOrientation && _keyboard != null && AssistanceAvailable && Torch != null &&
                NearestSeam(Torch.ArcOriginPosition, out Vector3 vp, out Vector3 vn, out Vector3 vt) &&
                Vector3.Distance(vp, Torch.ArcOriginPosition) < 0.08f)
            {
                Quaternion recommended = Quaternion.FromToRotation(BaseApproach.normalized, LimitTilt(-RecommendedHand(vn, vt)));
                _keyboard.SetOrientation(recommended);
                cmd.Orientation = recommended;
                cmd.ResetOrientation = false;
            }
            if (keyboardMotion && Input.GetKeyDown(KeyCode.B))
            {
                if (Approaching) _approachStage = -1;
                else BeginAssistedApproach();
            }
            if (keyboardMotion && Input.GetKeyDown(KeyCode.C)) BeginSafeRetreat();
            ApplyMotion(cmd, keyboardMotion, Time.deltaTime);

            if (Machine != null)
            {
                if (cmd.CycleElectrode) { Machine.CycleElectrode(); if (Torch) Torch.LoadNewElectrode(); }
                if (cmd.AmperageStep != 0) Machine.StepAmperage(cmd.AmperageStep);
            }
            if (Torch != null)
                Torch.SetSparkRequested(cmd.SparkHeld && !Approaching && !Retracting && !MotionBlocked && (Task == null || Task.State == SessionState.Running));
            if (cmd.ReplaceElectrode && Task != null) Task.ReplaceElectrode();
            if (cmd.ToggleHelmet && Helmet != null) Helmet.ToggleHelmet();

            if (LogDiagnostics) Diagnostics();
        }

        // Conversion externa al solver: Soldador sigue siendo el TCP calibrado.
        // Se conserva la posicion pedida de la punta al inclinar o consumir varilla.
        public void ApplyMotion(TeleopCommand cmd, bool keyboardMotion, float deltaTime)
        {
            float dt = Mathf.Clamp(deltaTime, 0f, 0.05f);
            Vector3 axis = LimitTilt(cmd.ResetOrientation ? BaseApproach : cmd.Orientation * BaseApproach);
            bool physical = keyboardMotion && ControlPhysicalTip && Torch != null;
            if (_logicalMarker != null) _logicalMarker.enabled = !physical && _logicalMarkerVisible;
            if (_marker != null) _marker.localScale = Vector3.one * (physical ? 0.006f : MarkerRadius * 2f);
            SetPrecisionSolver(physical);
            Vector3 rate = cmd.MoveClutch ? ToWorldRate(cmd.TranslationRate) * MoveSpeed : Vector3.zero;
            if (!cmd.SparkHeld) AssistanceSuspended = false;
            if (!physical)
            {
                SeamLocked = HasSeamAngles = false;
                _physicalInit = false;
                _approachStage = -1;
                // El bloqueo y la retirada son estados del control de punta: no deben
                // seguir negando el arco al pasar al mando/IMU.
                if (MotionBlocked || Retracting) { MotionBlocked = Retracting = false; AssistMessage = ""; }
                _stallTime = 0f;
                _targetPos = ClampWorkspace(_targetPos + rate * dt);
                _smoothedPos = Vector3.SmoothDamp(_smoothedPos, _targetPos, ref _smoothVel,
                    Mathf.Max(0.001f, PositionSmoothTime), Mathf.Infinity, dt);
                Ik.target.position = _smoothedPos;
                Ik.approachDirection = axis;
                if (_marker != null) _marker.position = _smoothedPos;
                return;
            }
            Vector3 actual = Torch.ArcOriginPosition;
            Vector3 offset = actual - Ik.endEffector.position;
            if (MotionBlocked)
            {
                bool changedDirection = rate.sqrMagnitude < 1e-8f || Vector3.Dot(rate.normalized, _blockedDirection) < 0.5f;
                if (!changedDirection)
                {
                    Ik.solving = false;
                    if (_marker != null) _marker.position = actual;
                    return;
                }
                MotionBlocked = false;
                ResetPhysicalCommand(actual);
                AssistMessage = "";
            }
            Ik.solving = true;
            if (!_physicalInit)
            {
                _physicalTarget = _targetPos + offset;
                _physicalSmooth = _physicalTarget;
                _physicalVelocity = Vector3.zero;
                _physicalInit = true;
            }
            SeamLocked = false;
            if (SeamLockWhileArcing && cmd.SparkHeld && !Approaching && !Retracting && Task != null &&
                Task.State == SessionState.Running && NearestSeam(_physicalTarget, out Vector3 sp, out Vector3 sn, out Vector3 st) &&
                Vector3.Distance(sp, _physicalTarget) < SeamLockRadius)
            {
                SeamLocked = true;
                rate = SeamRate(cmd.MoveClutch ? cmd.TranslationRate : Vector3.zero, sn, st, dt);
            }
            if (Retracting)
            {
                _retreatTime += dt;
                if (rate.sqrMagnitude > 0f) Retracting = false;
                else
                {
                    // La retirada conserva la inclinacion y avanza a 40 mm/s.
                    axis = Ik.approachDirection;
                    _physicalTarget = Vector3.MoveTowards(_physicalTarget, _retreatDestination, 0.04f * dt);
                    if (Vector3.Distance(actual, _retreatDestination) < 0.005f) Retracting = false;
                    // Sin avance real (limite de alcance o contacto): terminar ya, no esperar el timeout.
                    if (Retracting && _retreatTime - _retreatProbeTime > 0.8f)
                    {
                        if (Vector3.Distance(actual, _retreatProbe) < 0.002f)
                        {
                            Retracting = false;
                            ResetPhysicalCommand(actual);
                            AssistMessage = NearJointLimit() ? "RETIRADA AL LÍMITE DE ALCANCE" : "RETIRADA LIMITADA — mover en otra dirección";
                        }
                        _retreatProbe = actual; _retreatProbeTime = _retreatTime;
                    }
                    if (_retreatTime > 6f)
                    {
                        Retracting = false;
                        ResetPhysicalCommand(actual);
                        AssistMessage = "RETIRADA LIMITADA — mover en otra direccion";
                    }
                }
            }
            if (Approaching && (!AssistanceAvailable || rate.sqrMagnitude > 0f || cmd.SparkHeld))
            {
                _approachStage = -1;
                _physicalTarget = actual;
                _physicalVelocity = Vector3.zero;
            }
            if (Approaching)
            {
                _approachTime += dt;
                Vector3 destination = _approachStage == 0 ? _approachLift :
                    _approachPoint + _approachHand * (_approachStage == 1 ? 0.08f : DesiredArcGap);
                if (_approachStage > 0)
                {
                    axis = LimitTilt(-_approachHand);
                    if (_keyboard != null) _keyboard.SetOrientation(Quaternion.FromToRotation(BaseApproach.normalized, axis));
                }
                _physicalTarget = Vector3.MoveTowards(_physicalTarget, destination, ApproachSpeed * dt);
                if (Vector3.Distance(actual, destination) < 0.005f)
                {
                    if (++_approachStage > 2) _approachStage = -1;
                    _approachTime = 0f;
                }
                if (_approachTime > 12f)
                {
                    Debug.LogWarning("[AyudaPunta] Timeout etapa=" + _approachStage + " punta=" + actual +
                        " destino=" + destination + " objetivo=" + _physicalTarget + " TCP=" + Ik.endEffector.position);
                    AssistMessage = "NO SE PUDO ACERCAR — mover y volver a pulsar B";
                    _physicalTarget = actual;
                    _approachStage = -1;
                }
            }
            else if (rate.sqrMagnitude > 0f)
            {
                // No acumular un destino lejano si el brazo no puede seguirlo.
                _physicalTarget = actual + Vector3.ClampMagnitude(_physicalTarget - actual, MaximumManualLead);
                _physicalTarget = SeamLocked ? SeamStep(_physicalTarget, dt) : _physicalTarget + rate * dt;
            }
            if (AssistanceAvailable && !Approaching && !Retracting && cmd.SparkHeld && !Torch.Stuck &&
                NearestSeam(_physicalTarget, out Vector3 point, out Vector3 normal, out Vector3 tangent))
            {
                Vector3 hand = -Torch.PointingDir;
                float dot = Vector3.Dot(hand, normal);
                Vector3 lateral = Vector3.ProjectOnPlane(_physicalTarget - point, normal);
                float height = Vector3.Dot(_physicalTarget - point, normal);
                // La orden del operador tiene prioridad sobre el iman de la guia.
                // Mantener la suspension evita recapturar la punta a mitad de retirada.
                Vector3 sideways = Vector3.ProjectOnPlane(rate, tangent);
                if (Vector3.Dot(rate, normal) > 0.0005f ||
                    (sideways.magnitude > 0.001f && Vector3.Dot(sideways, lateral) > 0.000001f))
                    AssistanceSuspended = true;
                if (!AssistanceSuspended && dot > 0.3f && lateral.magnitude < 0.012f && height > -0.003f && height < 0.025f)
                {
                    // Centrado suave sobre el trazo interpolado, sin avance automatico.
                    // Soltar el arco permite alejarse libremente de la guia.
                    _physicalTarget -= Vector3.ClampMagnitude(lateral, 0.012f * dt);
                    // La guia puede estar elevada sobre la chapa: regular con el
                    // sensor real, no confundir esa altura visual con arco fisico.
                    if (Torch.HasArcSurface && Torch.ArcLenMm < 40f)
                    {
                        float error = Torch.ArcLenMm * 0.001f - DesiredArcGap;
                        _physicalTarget += Torch.PointingDir * Mathf.Clamp(error * 4f * dt, -0.01f * dt, 0.01f * dt);
                    }
                    UsedTrainingAssistance = true;
                }
            }
            _physicalTarget = GuardSurface(actual, _physicalTarget);
            _physicalSmooth = Vector3.SmoothDamp(_physicalSmooth, _physicalTarget, ref _physicalVelocity,
                Mathf.Max(0.08f, PositionSmoothTime), Mathf.Infinity, dt);
            _physicalSmooth = GuardSurface(actual, _physicalSmooth);
            _smoothedPos = ClampWorkspace(_physicalSmooth - offset);
            _targetPos = _smoothedPos;
            _physicalSmooth = _smoothedPos + offset;
            Ik.target.position = _smoothedPos;
            Ik.approachDirection = axis;
            if (_marker != null) _marker.position = _physicalSmooth;
            UpdateSeamTelemetry(actual, dt);
            CheckForStall(actual, rate, dt);
        }

        // Ejes horizontales de la vista: marco de las teclas y de la inclinacion.
        void ViewAxes(out Vector3 forward, out Vector3 right)
        {
            Transform refT = !ReferenceRelative ? null : MovementReference != null ? MovementReference :
                ReferenceCamera != null ? ReferenceCamera.transform : null;
            forward = refT != null ? Vector3.ProjectOnPlane(refT.forward, Vector3.up) : Vector3.forward;
            right = refT != null ? Vector3.ProjectOnPlane(refT.right, Vector3.up) : Vector3.right;
            forward = forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;
            right = right.sqrMagnitude < 1e-4f ? Vector3.right : right.normalized;
        }

        static float Sign(float v) => v >= 0f ? 1f : -1f;
        static float Step(float v) => v > 1e-5f ? 1f : v < -1e-5f ? -1f : 0f;

        /// Con arco sobre la junta cada tecla queda alineada con un eje de la costura:
        /// la que mas se parece a la tangente avanza, la otra desplaza de costado y Q/E
        /// acercan o alejan la punta sobre el eje del electrodo.
        Vector3 SeamRate(Vector3 keys, Vector3 n, Vector3 t, float dt)
        {
            Vector3 b = Vector3.Cross(n, t).normalized;
            ViewAxes(out Vector3 f, out Vector3 r);
            Vector3 flatTangent = Vector3.ProjectOnPlane(t, Vector3.up);
            bool travelOnForward = Mathf.Abs(Vector3.Dot(f, flatTangent)) >= Mathf.Abs(Vector3.Dot(r, flatTangent));
            _seamForwardAxis = travelOnForward ? t * Sign(Vector3.Dot(f, t)) : b * Sign(Vector3.Dot(f, b));
            _seamRightAxis = travelOnForward ? b * Sign(Vector3.Dot(r, b)) : t * Sign(Vector3.Dot(r, t));
            bool fine = _keyboard != null && _keyboard.Fine, fast = _keyboard != null && _keyboard.Fast;
            Vector3 dir = _seamRightAxis * Step(keys.x) + _seamForwardAxis * Step(keys.z);
            _seamTravel = Vector3.Dot(dir, t) * (fine ? WeldTravelFine : fast ? WeldTravelFast : WeldTravelSpeed);
            _seamLateral = Vector3.Dot(dir, b) * WeldLateralSpeed * (fine ? 0.4f : 1f);
            // E sostenida levanta cada vez mas rapido: un toque ajusta el arco, mantener lo corta.
            _liftHeld = keys.y > 1e-5f ? _liftHeld + dt : 0f;
            float lift = Mathf.Lerp(ArcAdjustSpeed, ArcLiftSpeed, Mathf.Clamp01((_liftHeld - 0.5f) / 0.6f));
            _seamFeed = keys.y < -1e-5f ? ArcAdjustSpeed * (fine ? 0.4f : 1f) : keys.y > 1e-5f ? -lift : 0f;
            string advance = travelOnForward ? (Vector3.Dot(f, t) >= 0f ? "W" : "S") : (Vector3.Dot(r, t) >= 0f ? "D" : "A");
            if (advance != _lastAdvance || travelOnForward != _lastTravelOnForward)
            {
                _lastAdvance = advance; _lastTravelOnForward = travelOnForward;
                SeamHelp = "SOBRE LA COSTURA · " + advance + ": avanzar · " + (travelOnForward ? "A/D" : "W/S") +
                    ": lateral · Q/E: arco · I/K/J/L: ángulos";
            }
            return t * _seamTravel + b * _seamLateral + Torch.PointingDir * _seamFeed;
        }

        // Avanza sobre la costura conservando separacion lateral y altura, tambien en el caño.
        Vector3 SeamStep(Vector3 target, float dt)
        {
            if (!NearestSeam(target, out Vector3 p, out Vector3 n, out Vector3 t)) return target;
            Vector3 b = Vector3.Cross(n, t).normalized;
            float lateral = Vector3.Dot(target - p, b) + _seamLateral * dt;
            float height = Vector3.Dot(target - p, n);
            NearestSeam(p + t * (_seamTravel * dt), out Vector3 p2, out Vector3 n2, out Vector3 t2);
            Vector3 b2 = Vector3.Cross(n2, t2).normalized;
            return p2 + b2 * lateral + n2 * height + Torch.PointingDir * (_seamFeed * dt);
        }

        Vector3 TipOffset() => Torch != null && Ik != null && Ik.endEffector != null
            ? Torch.ArcOriginPosition - Ik.endEffector.position : Vector3.zero;

        Vector3 RecommendedHand(Vector3 normal, Vector3 tangent)
        {
            Vector3 side = Vector3.Cross(normal, tangent).normalized;
            return (normal + tangent * Mathf.Tan(Task.Seam.IdealTravelAngle * Mathf.Deg2Rad) +
                side * Mathf.Tan(Task.Seam.IdealWorkAngle * Mathf.Deg2Rad)).normalized;
        }

        void UpdateSeamTelemetry(Vector3 tip, float dt)
        {
            Vector3 p = tip, n = Vector3.up, t = Vector3.right;
            HasSeamAngles = Task != null && NearestSeam(tip, out p, out n, out t) && Vector3.Distance(p, tip) < 0.05f;
            if (!HasSeamAngles) { TravelSpeedMmS = 0f; _hasLastTip = false; return; }
            WeldingHeatModel.ToolAngles(Torch.ElectrodeDir, t, n, out float travel, out float work);
            TravelAngleDeg = travel; WorkAngleDeg = work;
            if (_hasLastTip && dt > 1e-4f)
                TravelSpeedMmS = Mathf.Lerp(TravelSpeedMmS, Vector3.Dot(tip - _lastTip, t) / dt * 1000f, 1f - Mathf.Exp(-dt / 0.4f));
            _lastTip = tip; _hasLastTip = true;
        }

        void ResetPhysicalCommand(Vector3 actual)
        {
            _physicalTarget = _physicalSmooth = actual;
            _physicalVelocity = Vector3.zero;
            _physicalInit = true;
            _stallTime = 0f;
            _targetPos = _smoothedPos = Ik.endEffector.position;
            Ik.target.position = _smoothedPos;
        }

        void CheckForStall(Vector3 actual, Vector3 rate, float dt)
        {
            if (Approaching || Retracting || rate.sqrMagnitude < 1e-8f ||
                Vector3.Distance(actual, _physicalSmooth) < 0.008f)
            { _stallTime = 0f; return; }
            if (_stallTime == 0f) _stallStart = actual;
            _stallTime += dt;
            if (_stallTime < 0.75f) return;
            if (Vector3.Distance(actual, _stallStart) < Mathf.Max(0.00035f, rate.magnitude * _stallTime * 0.15f))
            {
                _blockedDirection = rate.normalized;
                if (_arm != null) _arm.HoldActualPose();
                ResetPhysicalCommand(actual);
                MotionBlocked = true;
                Ik.solving = false;
                AssistMessage = NearJointLimit() ? "LÍMITE DE ALCANCE — mover en otra dirección" :
                    "MOVIMIENTO BLOQUEADO — E: retirar / C: recuperar";
            }
            _stallTime = 0f;
        }

        // Alguna articulacion a menos de 4 grados de su tope: el brazo esta estirado o plegado al maximo.
        bool NearJointLimit()
        {
            if (_arm == null) return false;
            for (int i = 0; i < PandaArm.N; i++)
            {
                float a = _arm.CurrentDeg(i);
                if (a - PandaArm.Limits[i].x < 4f || PandaArm.Limits[i].y - a < 4f) return true;
                if (i == 3 && Ik != null && a > Ik.elbowKeepBentDeg - 4f) return true; // codo estirado al maximo permitido
            }
            return false;
        }

        public bool BeginSafeRetreat()
        {
            if (Torch == null || Ik == null || Ik.endEffector == null) return false;
            MotionBlocked = false;
            _approachStage = -1;
            AssistanceSuspended = true;
            ResetPhysicalCommand(Torch.ArcOriginPosition);
            Vector3 offset = Torch.ArcOriginPosition - Ik.endEffector.position;
            _retreatDestination = ClampWorkspace(Torch.ArcOriginPosition + Vector3.up * 0.08f - offset) + offset;
            _retreatTime = 0f;
            _retreatProbe = Torch.ArcOriginPosition; _retreatProbeTime = 0f;
            Retracting = true;
            AssistMessage = "";
            Ik.solving = true;
            Torch.SetSparkRequested(false);
            return true;
        }

        public bool BeginAssistedApproach()
        {
            if (!AssistanceAvailable || Torch == null || Torch.SparkRequested || Torch.Stuck ||
                !NearestSeam(Torch.ArcOriginPosition, out _approachPoint, out Vector3 normal, out Vector3 tangent)) return false;
            _approachHand = RecommendedHand(normal, tangent);
            if (Vector3.Angle(-_approachHand, Vector3.down) > MaxTiltFromVertical) return false;
            _physicalTarget = _physicalSmooth = Torch.ArcOriginPosition;
            _physicalVelocity = Vector3.zero;
            _physicalInit = true;
            MotionBlocked = Retracting = false;
            // La pose inicial puede estar dentro del radio minimo del TCP.
            // Retirada alcanzable: no esperar indefinidamente un punto limitado.
            // Subir solo hasta la altura de aproximacion: con el electrodo largo el techo de
            // alcance esta ~15 cm sobre la pieza y una subida fija de 80 mm podia no llegar nunca.
            Vector3 offset = Torch.ArcOriginPosition - Ik.endEffector.position;
            Vector3 lift = Torch.ArcOriginPosition;
            lift.y = Mathf.Max(lift.y, _approachPoint.y + 0.08f);
            _approachLift = ClampWorkspace(lift - offset) + offset;
            AssistMessage = "";
            _approachTime = 0f;
            _approachStage = 0;
            UsedTrainingAssistance = true;
            return true;
        }

        public bool NearestSeam(Vector3 position, out Vector3 point, out Vector3 normal, out Vector3 tangent)
        {
            point = Vector3.zero; normal = Vector3.up; tangent = Vector3.right;
            WeldSeam seam = Task != null ? Task.Seam : null;
            if (seam == null || seam.Count < 2) return false;
            float best = float.PositiveInfinity;
            int segments = seam.Shape == SeamShape.Circle ? seam.Count : seam.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % seam.Count;
                Vector3 a = seam.PointAt(i), d = seam.PointAt(j) - a;
                float t = d.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(position - a, d) / d.sqrMagnitude) : 0f;
                Vector3 candidate = a + d * t;
                float distance = (candidate - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; point = candidate;
                normal = Vector3.Lerp(seam.NormalAt(i), seam.NormalAt(j), t).normalized;
                tangent = Vector3.Lerp(seam.TangentAt(i), seam.TangentAt(j), t).normalized;
            }
            return true;
        }

        public Vector3 GuardSurface(Vector3 origin, Vector3 destination)
        {
            const float margin = 0.003f;
            // Limitar solamente la componente que entra en una superficie proxima.
            // Se conserva el avance tangencial aun estando pegado al margen.
            int nearby = Physics.OverlapSphereNonAlloc(origin, 0.012f, _nearSurfaces, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < nearby; i++)
            {
                Collider collider = _nearSurfaces[i];
                if (!IsMotionObstacle(collider)) continue;
                // ClosestPoint no admite MeshCollider no convexo (cano N5): lo cubre el rayo de abajo.
                if (collider is MeshCollider mesh && !mesh.convex) continue;
                Vector3 surface = collider.ClosestPoint(origin), normal = origin - surface;
                float distance = normal.magnitude;
                if (distance < 1e-6f) continue; // Permitir salir de una penetracion inicial.
                normal /= distance;
                float wanted = Vector3.Dot(destination - surface, normal);
                if (wanted < margin && Vector3.Dot(destination - origin, normal) < 0f)
                    destination += normal * (Mathf.Min(distance, margin) - wanted);
            }
            Vector3 cursor = origin;
            for (int pass = 0; pass < 3; pass++)
            {
                Vector3 delta = destination - cursor;
                float length = delta.magnitude;
                if (length < 1e-6f) return destination;
                int count = Physics.RaycastNonAlloc(cursor, delta / length, _motionHits,
                    length + margin, ~0, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    if (!IsMotionObstacle(_motionHits[i].collider) ||
                        Vector3.Dot(delta / length, _motionHits[i].normal) >= -0.001f) continue;
                    if (_motionHits[i].distance >= best) continue;
                    best = _motionHits[i].distance; normal = _motionHits[i].normal;
                }
                if (float.IsPositiveInfinity(best)) return destination;
                Vector3 contact = cursor + delta / length * Mathf.Clamp(best - margin, 0f, length);
                destination = contact + Vector3.ProjectOnPlane(destination - contact, normal);
                cursor = contact;
            }
            return cursor;
        }

        bool IsMotionObstacle(Collider collider)
        {
            if (collider == null || collider.gameObject.layer == 5) return false;
            Transform hit = collider.transform;
            if (Torch != null && Torch.IgnoreRoot != null && hit.IsChildOf(Torch.IgnoreRoot)) return false;
            return hit != Ik.target && hit != _marker;
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
                return ClampWorkspace(Task.Seam.PointAt(0) + Vector3.up * 0.04f - TipOffset());
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
