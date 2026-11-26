using UnityEngine;
using FrankaVR.Welding;
using FrankaVR.Teleop;
using FrankaVR.V5;

namespace FrankaVR.V6
{
    /// <summary>
    /// HUD mas limpio: panel separado por bloques, mensajes cortos y espaciado mayor.
    /// Pensado para verse bien en PC y VR movil.
    /// </summary>
    public class VRTrainingHUDV6 : MonoBehaviour
    {
        public Camera PlayerCamera;
        public CourseTrainingManagerV5 Course;
        public WeldingTaskManager Task;
        public WeldingTorch Torch;
        public WeldingMachine Machine;
        public HybridTeleopInput HybridInput;
        public ViewpointManager Viewpoints;
        public DesktopNavigatorV6 Navigator;

        [Header("Posicion")]
        public float Distance = 1.55f;
        public float VerticalOffset = -0.32f;
        public float HorizontalOffset = 0.60f;
        public float Width = 1.18f;
        public float Height = 0.35f;
        [Tooltip("Compatibilidad: ponerlo en true abre el documento de CONTROLES (CourseWorldMenuV6).")]
        public bool ShowHelpDuringRunning = false;

        Transform _root;
        TextMesh _headline, _metrics, _help, _alert, _gaugeValue;
        Transform _gaugeZone, _gaugeMarker;
        Material _gaugeTrackMat, _gaugeZoneMat, _gaugeMarkerMat, _accentMat;
        const float GaugeWidth = 0.60f, GaugeY = 0.205f, GaugeRangeMm = 30f; // distancia boquilla-pieza
        Renderer _bg, _alertBg;
        Material _bgMat, _alertMat;
        GazeButton _pipeBack, _pipeForward, _clearButton, _executeButton;
        WeldPathRecorder _recorder;
        RobotCollisionGuard _collisions;
        WeldingTeleopController _teleop;
        CourseWorldMenuV6 _menu;
        OperatorMoveV6 _move;

        void Start()
        {
            _teleop = FindAnyObjectByType<WeldingTeleopController>();
            _menu = FindAnyObjectByType<CourseWorldMenuV6>();
            _recorder = FindAnyObjectByType<WeldPathRecorder>();
            _collisions = FindAnyObjectByType<RobotCollisionGuard>();
            Resolve();
            Build();
            GazeInteractor pointer = FindFirstObjectByType<GazeInteractor>();
            if (pointer == null) pointer = gameObject.AddComponent<GazeInteractor>();
            pointer.playerCamera = PlayerCamera;
            pointer.InteractionMask = 1 << 5;
        }

        void Resolve()
        {
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            if (Course == null) Course = FindObjectOfType<CourseTrainingManagerV5>();
            if (Task == null) Task = FindObjectOfType<WeldingTaskManager>();
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            if (Machine == null) Machine = FindObjectOfType<WeldingMachine>();
            if (HybridInput == null) HybridInput = FindObjectOfType<HybridTeleopInput>();
            if (Viewpoints == null) Viewpoints = FindObjectOfType<ViewpointManager>();
            if (Navigator == null) Navigator = FindObjectOfType<DesktopNavigatorV6>();
        }

        void Build()
        {
            if (PlayerCamera == null) return;
            var root = new GameObject("VRTrainingHUDV6_Runtime");
            _root = root.transform;

            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "HUD_Background";
            bg.transform.SetParent(_root, false);
            bg.transform.localScale = new Vector3(Width, Height + 0.15f, 1f);
            bg.transform.localPosition = new Vector3(0f, -0.005f, 0f);
            SafeDestroy(bg.GetComponent<Collider>());
            _bg = bg.GetComponent<Renderer>();
            _bg.sortingOrder = -10;

            GameObject alertBox = GameObject.CreatePrimitive(PrimitiveType.Quad);
            alertBox.name = "HUD_AlertBox";
            alertBox.transform.SetParent(_root, false);
            alertBox.transform.localPosition = new Vector3(0f, -0.128f, -0.001f);
            alertBox.transform.localScale = new Vector3(Width * 0.92f, 0.075f, 1f);
            SafeDestroy(alertBox.GetComponent<Collider>());
            _alertBg = alertBox.GetComponent<Renderer>();
            _alertBg.sortingOrder = -9;

            // Fondo y botones encima de la escena, como el texto: si no, la mesa o el robot los tapan.
            Shader sh = Resources.Load<Shader>("FrankaVRUIPanel");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _bgMat = new Material(sh); _bgMat.color = new Color(0.02f, 0.03f, 0.045f, 0.80f);
                _alertMat = new Material(sh); _alertMat.color = new Color(0.22f, 0.12f, 0.02f, 0.92f);
                _bg.sharedMaterial = _bgMat;
                _alertBg.sharedMaterial = _alertMat;
            }

            if (sh != null)
            {
                // Franja de estado e indicador de arco: distancia de un vistazo, sin leer numeros.
                _accentMat = new Material(sh);
                MakeBar("HUD_Accent", new Vector3(0f, 0.241f, -0.002f), new Vector2(Width, 0.008f), _accentMat, -8);
                _gaugeTrackMat = new Material(sh); _gaugeTrackMat.color = new Color(0.16f, 0.20f, 0.25f, 0.95f);
                _gaugeZoneMat = new Material(sh); _gaugeZoneMat.color = new Color(0.16f, 0.62f, 0.34f, 0.95f);
                _gaugeMarkerMat = new Material(sh);
                MakeBar("HUD_GaugeTrack", new Vector3(0f, GaugeY, -0.002f), new Vector2(GaugeWidth, 0.016f), _gaugeTrackMat, -8);
                _gaugeZone = MakeBar("HUD_GaugeZone", new Vector3(0f, GaugeY, -0.003f), new Vector2(0.1f, 0.016f), _gaugeZoneMat, -7);
                _gaugeMarker = MakeBar("HUD_GaugeMarker", new Vector3(0f, GaugeY, -0.004f), new Vector2(0.007f, 0.034f), _gaugeMarkerMat, -6);
                MakeText("GaugeLabel", new Vector3(-0.40f, GaugeY, -0.004f), 42, 0.0062f, new Color(0.68f, 0.76f, 0.86f), TextAnchor.MiddleCenter).text = "DIST.";
                _gaugeValue = MakeText("GaugeValue", new Vector3(0.41f, GaugeY, -0.004f), 46, 0.0068f, new Color(0.94f, 0.97f, 1f), TextAnchor.MiddleCenter);
            }

            _headline = MakeText("Headline", new Vector3(0f, 0.12f, -0.004f), 56, 0.0087f, new Color(0.94f, 0.97f, 1f), TextAnchor.MiddleCenter);
            _metrics  = MakeText("Metrics",  new Vector3(0f, 0.035f, -0.004f), 52, 0.0078f, new Color(0.82f, 0.89f, 0.98f), TextAnchor.MiddleCenter);
            _help     = MakeText("Help",     new Vector3(0f, -0.052f, -0.004f), 42, 0.0062f, new Color(0.68f, 0.76f, 0.86f), TextAnchor.MiddleCenter);
            _alert    = MakeText("Alert",    new Vector3(0f, -0.128f, -0.005f), 58, 0.0082f, new Color(1f, 0.84f, 0.32f), TextAnchor.MiddleCenter);
            _move = OperatorMoveV6.Ensure(PlayerCamera, Viewpoints);
            GazeButton.CreateUI(_root, "FIN", new Vector3(-0.475f,-0.21f,-0.014f), 0.17f, () => Course.FinishExercise(), _bgMat);
            GazeButton.CreateUI(_root, "CONTROLES", new Vector3(-0.285f,-0.21f,-0.014f), 0.17f, () => { if (_menu != null) _menu.ToggleControls(); }, _bgMat);
            GazeButton.CreateUI(_root, "VISTA", new Vector3(-0.095f,-0.21f,-0.014f), 0.17f, () => Viewpoints.Next(), _bgMat);
            GazeButton.CreateUI(_root, "MOVERSE", new Vector3(0.095f,-0.21f,-0.014f), 0.17f, () => _move.Toggle(), _bgMat);
            _pipeBack = GazeButton.CreateUI(_root, "CAÑO -", new Vector3(0.285f,-0.21f,-0.014f), 0.17f, () => Course.IndexPipe(-15f), _bgMat);
            _pipeForward = GazeButton.CreateUI(_root, "CAÑO +", new Vector3(0.475f,-0.21f,-0.014f), 0.17f, () => Course.IndexPipe(15f), _bgMat);
            _clearButton = GazeButton.CreateUI(_root, "BORRAR", new Vector3(0.285f,-0.21f,-0.014f), 0.17f, () => Course.ClearProgram(), _bgMat);
            _executeButton = GazeButton.CreateUI(_root, "EJECUTAR", new Vector3(0.475f,-0.21f,-0.014f), 0.17f, () => Course.ExecuteProgram(), _bgMat);
            // Encima de vidrios y brillos del taller (el orden transparente va primero por sortingOrder).
            foreach (Renderer r in _root.GetComponentsInChildren<Renderer>(true)) r.sortingOrder += 1000;
        }

        Transform MakeBar(string name, Vector3 localPos, Vector2 size, Material material, int order)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SafeDestroy(go.GetComponent<Collider>());
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return go.transform;
        }

        void UpdateGauge(string alert)
        {
            if (_gaugeMarker == null || Torch == null) return;
            bool running = Course != null && Course.Phase == CoursePhaseV5.Running;
            bool exam = Course != null && Course.Mode == CourseModeV5.Exam;
            bool measured = Torch.HasArcSurface && Torch.ArcLenMm < 60f;
            // MIG: distancia boquilla-pieza (CTWD) = alambre libre + arco. Ideal ~15 mm.
            float stickOut = Torch.RemainingMm < 40f ? Torch.RemainingMm : 0f;
            float ideal = (Machine != null && Machine.Current != null ? Machine.Current.ArcLenIdealMm : 3.2f) + stickOut;
            float half = 3f;
            float arc = measured ? Torch.ArcLenMm + stickOut : GaugeRangeMm;
            bool good = measured && Mathf.Abs(arc - ideal) <= half;
            // En examen no se muestra la zona ideal: seria una pista de calidad.
            _gaugeZone.gameObject.SetActive(running && !exam);
            _gaugeZone.localPosition = new Vector3((ideal / GaugeRangeMm - 0.5f) * GaugeWidth, GaugeY, -0.003f);
            _gaugeZone.localScale = new Vector3(2f * half / GaugeRangeMm * GaugeWidth, 0.016f, 1f);
            _gaugeMarker.gameObject.SetActive(running && measured);
            _gaugeMarker.localPosition = new Vector3((Mathf.Clamp01(arc / GaugeRangeMm) - 0.5f) * GaugeWidth, GaugeY, -0.004f);
            Color neutral = new Color(0.94f, 0.97f, 1f);
            _gaugeMarkerMat.color = exam ? neutral : good ? new Color(0.35f, 1f, 0.55f) : arc < ideal - half ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.78f, 0.25f);
            string value = !running ? "" : !measured ? "lejos" : arc > GaugeRangeMm ? "> 30 mm" : arc.ToString("0") + " mm";
            if (_gaugeValue.text != value) _gaugeValue.text = value;
            bool recording = _recorder != null && _recorder.RecordingNow;
            _accentMat.color = !running ? new Color(0.20f, 0.55f, 0.75f, 0.95f) :
                recording ? new Color(1f, 0.25f, 0.25f, 0.95f) :
                Torch.Arcing ? new Color(0.30f, 0.95f, 0.50f, 0.95f) :
                !string.IsNullOrEmpty(alert) ? new Color(1f, 0.70f, 0.20f, 0.95f) : new Color(0.20f, 0.55f, 0.75f, 0.95f);
        }

        TextMesh MakeText(string name, Vector3 localPos, int fontSize, float charSize, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            CrispTextV6.Setup(tm, fontSize, charSize);
            tm.color = color;
            go.GetComponent<Renderer>().sortingOrder = 10;
            return tm;
        }

        void LateUpdate()
        {
            if (_root == null || PlayerCamera == null) return;
            // Con el documento de controles abierto el HUD se oculta: si no, su texto se ve encima.
            bool document = _menu != null && _menu.ControlsOpen;
            if (_root.gameObject.activeSelf == document) _root.gameObject.SetActive(!document);
            if (document) return;
            Transform cam = PlayerCamera.transform;
            Transform body = Application.platform == RuntimePlatform.Android && cam.parent != null ? cam.parent : cam;
            float scale = Navigator != null ? Navigator.InterfaceScale : 1f;
            _root.localScale = Vector3.one * scale;
            _root.position = cam.position + body.forward * Distance + body.up * (VerticalOffset * scale) + body.right * (HorizontalOffset * scale);
            _root.rotation = Quaternion.LookRotation(_root.position - cam.position, body.up);
            bool index = Course != null && Course.UsesPipeIndexer && Course.Phase == CoursePhaseV5.Running;
            _pipeBack.gameObject.SetActive(index);
            _pipeForward.gameObject.SetActive(index);
            bool teach = !index && Course != null && Course.Phase == CoursePhaseV5.Running && Course.Stage == CourseStageV5.Teach;
            _clearButton.gameObject.SetActive(teach);
            _executeButton.gameObject.SetActive(teach);
            // 20 veces por segundo alcanza para leer: armar los textos en cada cuadro generaba
            // basura constante y, en el celular, tirones periodicos del recolector.
            if (Time.unscaledTime >= _nextText)
            {
                _nextText = Time.unscaledTime + TextIntervalSec;
                UpdateText();
            }
        }

        [Tooltip("Cada cuanto se rearman los textos y el indicador de distancia (s).")]
        public float TextIntervalSec = 0.05f;
        float _nextText;

        void UpdateText()
        {
            string phase = Course != null ? Course.PhaseLabel() : "SIMULADOR";
            string view = Viewpoints != null ? Viewpoints.CurrentLabel : "Vista";
            string input = HybridInput != null ? HybridInput.ActiveMotionSource : "Teclado";

            string id = Course != null ? Course.CurrentOrderId : "--";
            SetText(_headline, id + "  ·  " + phase, Width * 0.90f, 0.045f);

            bool running = Course != null && Course.Phase == CoursePhaseV5.Running;
            bool teaching = running && Course.Stage == CourseStageV5.Teach;
            string wire = Machine != null && Machine.Current != null ? Machine.Current.DiameterMm.ToString("0.0") + " mm" : "--";
            string amps = Machine != null ? Machine.Amperage.ToString("0") + " A" : "--";
            string speed = Course != null ? Course.ProgramSpeedMmS.ToString("0.0") + " mm/s" : "--";
            string source = "Entrada: " + input + "    Vista: " + view;
            if (running && _recorder != null)
                source = Course.Stage == CourseStageV5.Execute
                    ? "Ejecutando programa  " + (_teleop != null ? (_teleop.PlaybackProgress * 100f).ToString("0") : "0") + " %"
                    : "Trayectoria  " + _recorder.Count + " puntos   Cubierto  " + _recorder.CoveragePercent.ToString("0") + " %";
            // En practica, angulos reales contra los ideales: lo que corrige un instructor.
            if (teaching && _teleop != null && _teleop.HasSeamAngles && Course.Mode == CourseModeV5.Training)
                source = "Ángulo avance " + _teleop.TravelAngleDeg.ToString("0") + "° (" + Task.Seam.IdealTravelAngle.ToString("0") +
                    "°)  trabajo " + _teleop.WorkAngleDeg.ToString("0") + "° (" + Task.Seam.IdealWorkAngle.ToString("0") + "°)\n" + source;
            string metrics = "Alambre " + wire + "   " + amps + "   Velocidad programa " + speed + "\n" + source;
            if (Task != null && Task.State == SessionState.Finished)
                metrics = "Nota  " + Task.FinalScore.ToString("0") + "/100   Tiempo  " + Task.Elapsed.ToString("0") + " s\n" +
                    "Puntos correctos  " + Task.GoodPoints + "   Sin soldar  " + Task.UntouchedPoints;
            SetText(_metrics, metrics, Width * 0.90f, 0.085f);

            if (OperatorMoveV6.Active)
            {
                SetText(_help, input == "Mando"
                    ? "MOVERSE · mirá el piso + OK: ir ahí · palanca: caminar · MOVERSE: volver a soldar"
                    : "MOVERSE · mirá el piso y tocá (clic): ir ahí · WASD: caminar · MOVERSE: volver a soldar", Width * 0.90f, 0.045f);
            }
            else if (running && input == "Mando")
            {
                // Con el mando ESP32 (lentes puestos) se muestran sus botones, no el teclado.
                if (!teaching) SetText(_help, "El robot repite tu trayectoria con arco · RETIRAR: abortar", Width * 0.90f, 0.045f);
                else if (_teleop != null && _teleop.SeamLocked) SetText(_help, "GRABANDO · palanca: avanzar (más palanca, más rápido) · apretada: distancia · inclinar el mando: ángulos", Width * 0.90f, 0.045f);
                else SetText(_help, "ACERCAR: ir al inicio · gatillo: grabar · DESHACER (largo: borrar) · OK: ejecutar o elegir lo que mirás", Width * 0.90f, 0.045f);
            }
            else if (running)
            {
                if (!teaching) SetText(_help, "El robot repite tu trayectoria con arco · C: abortar", Width * 0.90f, 0.045f);
                else if (_teleop != null && _teleop.SeamLocked) SetText(_help, "GRABANDO · " + _teleop.SeamHelp, Width * 0.90f, 0.045f);
                else SetText(_help, (Course.Mode == CourseModeV5.Training ? "B: ir al inicio · V: ángulo · " : "") +
                    "Espacio: grabar · Z: deshacer · R: borrar · Enter: ejecutar · F1: controles", Width * 0.90f, 0.045f);
            }
            else
            {
                SetText(_help, "CONTROLES (o F1): todos los comandos de teclado y mando", Width * 0.90f, 0.045f);
            }

            string alert = ComputeAlert();
            SetText(_alert, alert, Width * 0.87f, 0.045f);
            UpdateGauge(alert);
            if (_alertMat != null)
                _alertMat.color = string.IsNullOrEmpty(alert) ? new Color(0.02f, 0.03f, 0.045f, 0.0f) : new Color(0.22f, 0.12f, 0.02f, 0.92f);
        }

        string ComputeAlert()
        {
            if (Torch == null) return "";
            if (Course != null && Course.Phase != CoursePhaseV5.Running) return "";
            if (_teleop != null && _teleop.Retracting) return "RETIRANDO HERRAMIENTA — mover para cancelar";
            if (_teleop != null && _teleop.Approaching) return "ACERCANDO PUNTA — B / ACERCAR o mover para cancelar";
            if (_collisions != null && _collisions.Colliding) return _collisions.CollisionLabel;
            if (_teleop != null && !string.IsNullOrEmpty(_teleop.AssistMessage)) return _teleop.AssistMessage;
            if (Course != null && !string.IsNullOrEmpty(Course.ProgramMessage)) return Course.ProgramMessage;
            if (_recorder != null && !string.IsNullOrEmpty(_recorder.Hint)) return _recorder.Hint;
            if (Torch.State == ArcState.Stuck) return "ELECTRODO PEGADO — SEPARAR HERRAMIENTA";
            if (Machine != null && Machine.ThermalTripped) return "PROTECCION TERMICA — ESPERAR ENFRIAMIENTO";
            bool rod = Machine != null && Machine.Current != null && !Machine.Current.WireFeed;
            if (rod && Torch.RemainingMm <= Torch.StubLengthMm + 10f) return "VARILLA CASI AGOTADA — CAMBIAR ELECTRODO";
            if (Course != null && Course.Stage == CourseStageV5.Teach && _recorder != null && _recorder.RecordingNow)
            {
                if (Course.Mode == CourseModeV5.Exam) return "● GRABANDO";
                float ctwd = Torch.HasArcSurface ? Torch.ArcLenMm + Torch.RemainingMm : 99f;
                float ideal = Machine != null && Machine.Current != null ? Machine.Current.ArcLenIdealMm + Torch.RemainingMm : 15f;
                if (ctwd > ideal + 3f) return "● GRABANDO — ACERCÁ LA TORCHA";
                if (ctwd < ideal - 3f) return "● GRABANDO — ALEJÁ LA TORCHA";
                return "● GRABANDO";
            }
            if (!Torch.HasArcSurface) return "";

            // En examen se conservan avisos de estado, sin pistas de calidad.
            if (Course != null && Course.Mode == CourseModeV5.Exam) return "";

            if (Torch.SparkRequested && Torch.ArcLenMm >= Torch.MaxArcLenMm) return "ACERQUE LA TORCHA PARA CEBAR";
            if (Torch.Arcing && Torch.ArcLenMm < 1.2f) return "TORCHA MUY CERCA";
            if (Machine != null && Machine.Current != null)
            {
                float ideal = Machine.Current.ArcLenIdealMm;
                if (Torch.ArcLenMm > ideal * 2.2f && Torch.ArcLenMm < Torch.MaxArcLenMm) return "ARCO LARGO";
                if (Torch.Arcing && Torch.ArcQuality > 0.72f) return "ARCO ESTABLE";
            }
            return "";
        }

        static void SetText(TextMesh text, string value, float width, float height)
        {
            if (text.text == value) return;
            text.text = value;
            text.transform.localScale = Vector3.one;
            Bounds bounds = text.GetComponent<Renderer>().localBounds;
            float scale = Mathf.Min(1f, width / Mathf.Max(0.001f, bounds.size.x),
                height / Mathf.Max(0.001f, bounds.size.y));
            text.transform.localScale = Vector3.one * scale;
        }

        static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy()
        {
            if (_root != null) SafeDestroy(_root.gameObject);
            if (_bgMat != null) SafeDestroy(_bgMat);
            if (_alertMat != null) SafeDestroy(_alertMat);
            if (_accentMat != null) SafeDestroy(_accentMat);
            if (_gaugeTrackMat != null) SafeDestroy(_gaugeTrackMat);
            if (_gaugeZoneMat != null) SafeDestroy(_gaugeZoneMat);
            if (_gaugeMarkerMat != null) SafeDestroy(_gaugeMarkerMat);
        }
    }
}
