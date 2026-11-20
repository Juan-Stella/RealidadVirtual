using UnityEngine;
using FrankaVR.V5;
using FrankaVR.Welding;

namespace FrankaVR.V6
{
    /// <summary>
    /// Panel lateral limpio con resumen del curso y atajos, y el documento de CONTROLES
    /// (tabla teclado / mando que se abre con el boton CONTROLES o F1).
    /// No reemplaza la logica del curso; solo mejora presentacion.
    /// </summary>
    public class CourseWorldMenuV6 : MonoBehaviour
    {
        public CourseTrainingManagerV5 Course;
        public Camera PlayerCamera;
        public float Distance = 1.8f;
        public float SideOffset = -0.72f;
        public float VerticalOffset = 0.02f;
        public bool FollowCameraYaw = true;
        [Tooltip("Oculta el panel grande mientras se esta soldando; queda solo el HUD compacto.")]
        public bool HideWhileRunning = true;

        [Header("Documento de controles")]
        public float ControlsDistance = 1.3f;
        public float ControlsVerticalOffset = 0f;
        public bool ControlsOpen { get; private set; }

        Transform _root;
        TextMesh _title, _exercise, _list, _keys;
        Material _bgMat;
        VRTrainingHUDV6 _hud;
        DesktopNavigatorV6 _navigator;
        CoursePhaseV5 _lastPhase;
        GazeButton[] _exerciseButtons = new GazeButton[5];
        Transform _doc;
        Material _docBgMat, _closeMat;
        const float DocWidth = 1.62f, DocHeight = 1.24f;

        // Accion | teclado | mando. Las filas con '#' son titulos de seccion.
        static readonly string[,] ControlRows =
        {
            { "#MOVER LA TORCHA", "TECLADO", "MANDO" },
            { "Mover", "W A S D", "Palanca" },
            { "Subir / bajar", "E / Q", "Apretar palanca + mover" },
            { "Inclinar la torcha", "I K J L", "Inclinar el mando" },
            { "Girar · enderezar", "U O · V", "—" },
            { "Lento / rápido", "Ctrl / Shift", "Poca / mucha palanca" },
            { "#ENSEÑAR (GRABAR)", "TECLADO", "MANDO" },
            { "Ir al inicio de la junta", "B", "ACERCAR" },
            { "Grabar (mantener)", "Espacio", "GATILLO" },
            { "Deshacer último tramo", "Z", "DESHACER" },
            { "Borrar todo", "R", "DESHACER largo" },
            { "#EJECUTAR", "TECLADO", "MANDO" },
            { "El robot repite con arco", "Enter", "OK" },
            { "Retirar / abortar", "C", "RETIRAR" },
            { "Velocidad del programa", "- / +", "—" },
            { "Alambre, corriente, careta", "Tab, [ ], H", "—" },
            { "#VISTA Y MENÚ", "TECLADO", "MANDO" },
            { "Elegir un botón", "Mirarlo 1 s o clic", "Mirarlo + OK" },
            { "Cambiar vista", "N / P o VISTA", "ACERCAR largo" },
            { "Calibrar mando y vista", "—", "OK largo" },
            { "Moverse y mirar (PC)", "Flechas, RePág/AvPág, mouse", "—" },
            { "Zoom (PC)", "Rueda · Retroceso", "—" },
            { "Menú · terminar · ayuda", "M · F · F1", "CONTROLES" },
        };

        public void ToggleControls() => SetControlsOpen(!ControlsOpen);

        public void SetControlsOpen(bool open)
        {
            ControlsOpen = open;
            if (_doc != null && _doc.gameObject.activeSelf != open) _doc.gameObject.SetActive(open);
        }

        void Start()
        {
            if (Course == null) Course = FindObjectOfType<CourseTrainingManagerV5>();
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            _hud = FindFirstObjectByType<VRTrainingHUDV6>();
            _navigator = FindFirstObjectByType<DesktopNavigatorV6>();
            Build();
        }

        void Build()
        {
            if (PlayerCamera == null) return;
            GameObject root = new GameObject("CourseWorldMenuV6_Runtime");
            _root = root.transform;

            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.transform.SetParent(_root, false);
            bg.transform.localScale = new Vector3(1.02f, 0.88f, 1f);
            SafeDestroy(bg.GetComponent<Collider>());
            bg.GetComponent<Renderer>().sortingOrder = -10;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _bgMat = new Material(sh); _bgMat.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
                bg.GetComponent<Renderer>().sharedMaterial = _bgMat;
            }

            _title = MakeText("Title", new Vector3(0f, 0.35f, -0.004f), 66, 0.0085f, new Color(0.95f, 0.98f, 1f), TextAnchor.MiddleCenter);
            _exercise = MakeText("Exercise", new Vector3(0f, 0.25f, -0.004f), 54, 0.0078f, new Color(0.82f, 0.90f, 1f), TextAnchor.MiddleCenter);
            _list = MakeText("List", new Vector3(-0.44f, -0.015f, -0.004f), 48, 0.0065f, new Color(0.85f, 0.90f, 0.95f), TextAnchor.MiddleLeft);
            _keys = MakeText("Keys", new Vector3(-0.44f, -0.32f, -0.004f), 42, 0.0062f, new Color(0.70f, 0.78f, 0.87f), TextAnchor.MiddleLeft);
            WorkOrder[] catalog = WorkOrder.Catalog;
            for (int i = 0; i < Mathf.Min(catalog.Length, _exerciseButtons.Length); i++)
            {
                int exercise = i;
                _exerciseButtons[i] = GazeButton.CreateUI(_root, catalog[i].Id + " · " + catalog[i].Title.ToUpperInvariant(),
                    new Vector3(0f, 0.14f - i * 0.07f, -0.014f), 0.86f, () => Course.SelectExercise(exercise), _bgMat);
            }
            GazeButton.CreateUI(_root, "PRACTICAR", new Vector3(-0.30f,-0.32f,-0.014f), 0.27f, () => Course.StartTraining(), _bgMat);
            GazeButton.CreateUI(_root, "EXAMEN", new Vector3(0f,-0.32f,-0.014f), 0.27f, () => Course.StartExam(), _bgMat);
            GazeButton.CreateUI(_root, "MENU", new Vector3(0.30f,-0.32f,-0.014f), 0.27f, () => Course.BackToMenu(), _bgMat);
            GazeButton.CreateUI(_root, "CONTROLES", new Vector3(0f,-0.395f,-0.014f), 0.57f, () => SetControlsOpen(true), _bgMat);
            BuildControlsDocument(sh);
        }

        /// Documento de controles: panel grande delante de la camara, tabla de tres columnas.
        void BuildControlsDocument(Shader sh)
        {
            GameObject doc = new GameObject("ControlesV6_Runtime");
            _doc = doc.transform;
            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Fondo";
            bg.transform.SetParent(_doc, false);
            bg.transform.localScale = new Vector3(DocWidth, DocHeight, 1f);
            SafeDestroy(bg.GetComponent<Collider>());
            Renderer bgRenderer = bg.GetComponent<Renderer>();
            bgRenderer.sortingOrder = -10;
            // Documento modal: fondo y botones encima de la escena (el robot suele quedar mas cerca).
            Shader panel = Resources.Load<Shader>("FrankaVRUIPanel");
            if (panel != null) sh = panel;
            if (sh != null)
            {
                _docBgMat = new Material(sh); _docBgMat.color = new Color(0.03f, 0.045f, 0.065f, 1f);
                bgRenderer.sharedMaterial = _docBgMat;
                _closeMat = new Material(sh); _closeMat.color = new Color(0.30f, 0.10f, 0.06f, 0.95f);
            }

            TextMesh title = MakeText(_doc, "Titulo", new Vector3(-DocWidth * 0.5f + 0.06f, DocHeight * 0.5f - 0.07f, -0.004f), 66, 0.0085f,
                new Color(0.95f, 0.98f, 1f), TextAnchor.MiddleLeft);
            SetText(title, "CONTROLES · TECLADO Y MANDO", 1.10f, 0.06f);
            GazeButton.CreateUI(_doc, "CERRAR", new Vector3(DocWidth * 0.5f - 0.16f, DocHeight * 0.5f - 0.07f, -0.014f), 0.24f,
                () => SetControlsOpen(false), _closeMat != null ? _closeMat : _bgMat);

            // Tres columnas con la misma cantidad de lineas y la misma escala: las filas quedan alineadas.
            int rows = ControlRows.GetLength(0);
            float[] x = { -DocWidth * 0.5f + 0.06f, -0.21f, 0.29f };
            float[] width = { 0.51f, 0.47f, 0.47f };
            Color[] colors = { new Color(0.86f, 0.91f, 0.97f), new Color(0.62f, 0.86f, 1f), new Color(0.55f, 1f, 0.70f) };
            TextMesh[] columns = new TextMesh[3];
            float top = DocHeight * 0.5f - 0.15f, height = DocHeight - 0.28f, scale = 1f;
            for (int c = 0; c < 3; c++)
            {
                var text = new System.Text.StringBuilder();
                for (int r = 0; r < rows; r++)
                {
                    string cell = ControlRows[r, c];
                    bool section = ControlRows[r, 0].StartsWith("#");
                    if (c == 0 && section) cell = cell.Substring(1);
                    if (section) cell = "<color=#FFC857><b>" + cell + "</b></color>";
                    if (r > 0) text.Append('\n');
                    text.Append(cell);
                }
                columns[c] = MakeText(_doc, "Columna" + c, new Vector3(x[c], top, -0.004f), 64, 0.0062f, colors[c], TextAnchor.UpperLeft);
                columns[c].text = text.ToString();
                Bounds b = columns[c].GetComponent<Renderer>().localBounds;
                scale = Mathf.Min(scale, width[c] / Mathf.Max(0.001f, b.size.x), height / Mathf.Max(0.001f, b.size.y));
            }
            foreach (TextMesh column in columns) column.transform.localScale = Vector3.one * scale;

            TextMesh note = MakeText(_doc, "Nota", new Vector3(0f, -DocHeight * 0.5f + 0.05f, -0.004f), 46, 0.0065f,
                new Color(0.68f, 0.76f, 0.86f), TextAnchor.MiddleCenter);
            SetText(note, "Mirá un botón 1 s (o miralo y apretá OK en el mando) para elegirlo · F1, Esc o CERRAR para volver",
                DocWidth - 0.12f, 0.04f);
            // El orden de dibujo transparente va primero por sortingOrder: sin este salto,
            // vidrios y carteles del taller se dibujan despues y atraviesan el documento.
            foreach (Renderer r in doc.GetComponentsInChildren<Renderer>(true)) r.sortingOrder += 1000;
            doc.SetActive(false);
        }

        TextMesh MakeText(string name, Vector3 localPos, int fontSize, float charSize, Color color, TextAnchor anchor)
            => MakeText(_root, name, localPos, fontSize, charSize, color, anchor);

        static TextMesh MakeText(Transform parent, string name, Vector3 localPos, int fontSize, float charSize, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            CrispTextV6.Setup(tm, fontSize, charSize);
            tm.color = color;
            go.GetComponent<Renderer>().sortingOrder = 10;
            return tm;
        }

        void LateUpdate()
        {
            if (_root == null || PlayerCamera == null) return;
            if (Course != null)
            {
                if (Course.Phase == CoursePhaseV5.Results && _lastPhase != CoursePhaseV5.Results) SetControlsOpen(false);
                _lastPhase = Course.Phase;
            }
            // Compatibilidad: el viejo "mostrar ayuda" del HUD ahora abre el documento.
            if (_hud != null && _hud.ShowHelpDuringRunning) { _hud.ShowHelpDuringRunning = false; SetControlsOpen(true); }
            if (Input.GetKeyDown(KeyCode.F1)) ToggleControls();
            if (ControlsOpen && Input.GetKeyDown(KeyCode.Escape)) SetControlsOpen(false);

            Transform cam = PlayerCamera.transform;
            Transform body = Application.platform == RuntimePlatform.Android && cam.parent != null ? cam.parent : cam;
            float scale = _navigator != null ? _navigator.InterfaceScale : 1f;
            if (ControlsOpen && _doc != null)
            {
                _doc.localScale = Vector3.one * scale;
                _doc.position = cam.position + body.forward * ControlsDistance + body.up * (ControlsVerticalOffset * scale);
                _doc.rotation = Quaternion.LookRotation(_doc.position - cam.position, body.up);
            }

            bool shouldShow = !ControlsOpen && !(HideWhileRunning && Course != null && Course.Phase == CoursePhaseV5.Running);
            if (_root.gameObject.activeSelf != shouldShow) _root.gameObject.SetActive(shouldShow);
            if (!shouldShow) return;
            _root.localScale = Vector3.one * scale;
            Vector3 fwd = cam.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; else fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            // Mantener el panel en el campo visual tambien cuando la cabeza mira abajo.
            _root.position = FollowCameraYaw
                ? cam.position + body.forward * Distance + body.right * (SideOffset * scale) + body.up * (VerticalOffset * scale)
                : cam.position + fwd * Distance + right * SideOffset + Vector3.up * VerticalOffset;
            _root.rotation = FollowCameraYaw
                ? Quaternion.LookRotation(_root.position - cam.position, body.up)
                : Quaternion.identity;
            UpdateText();
        }

        void UpdateText()
        {
            SetText(_title, "CELDA MIG · MÉNSULA", 0.88f, 0.055f);
            if (Course != null && Course.SelectedOrder != null)
            {
                SetText(_exercise, Course.SelectedOrder.Id + "  ·  " + Course.SelectedOrder.Title + "\n" + Course.PhaseLabel(), 0.88f, 0.085f);
            }
            else SetText(_exercise, "Seleccione un ejercicio", 0.88f, 0.085f);

            string body = "TRABAJOS";
            WorkOrder[] catalog = WorkOrder.Catalog;
            for (int i = 0; i < catalog.Length; i++) body += "\n" + BuildExerciseLine(i, catalog[i].Id + "  " + catalog[i].Title);
            bool selection = Course != null && Course.Phase == CoursePhaseV5.Selection;
            foreach (GazeButton button in _exerciseButtons) if (button != null) button.gameObject.SetActive(selection);
            if (selection) body = "";

            if (Course != null && Course.Phase == CoursePhaseV5.Briefing && Course.SelectedOrder != null)
            {
                WorkOrder order = Course.SelectedOrder;
                body = Wrap(order.Brief, 45) + "\n\n" +
                    "1. ENSEÑAR: Espacio (GATILLO en el mando) sobre la\njunta y recorré la costura. Queda grabada.\n" +
                    "2. EJECUTAR: Enter (OK): el robot la repite con arco.\n" +
                    "CONTROLES: todos los comandos.\n\n" +
                    order.RecommendedElectrode + " · " + order.RecommendedAmps.ToString("0") + " A · " +
                    order.RecommendedSpeedMmS.ToString("0") + " mm/s";
            }
            if (Course != null && Course.Phase == CoursePhaseV5.Results && Course.Task != null)
            {
                WeldingTaskManager task = Course.Task;
                var result = WeldingSessionReport.Snapshot(task, Course.Mode.ToString(), Course.UsesPipeIndexer, Course.Recorder, Course.ProgramSpeedMmS);
                body = "Nota: " + task.FinalScore.ToString("0") + "/100 · " + (task.Passed() ? "APROBADO" : "SEGUIR PRACTICANDO") + "\n" +
                    "Cobertura: " + result.coveragePercent.ToString("0") + "% · Tiempo: " + task.Elapsed.ToString("0") + " s\n" +
                    "Correctos: " + task.GoodPoints + " · Sin soldar: " + task.UntouchedPoints + "\n" +
                    "Frios: " + task.ColdPoints + " · Perforacion: " + task.BurnPoints + "\n" +
                    "Socavado: " + task.UndercutPoints + " · Porosidad: " + task.PorosityPoints + "\n" +
                    "Trayectoria: " + result.teachPoints + " puntos · calidad " + result.teachQualityPercent.ToString("0") + "%" +
                    (result.teachCollisionPoints > 0 ? " · " + result.teachCollisionPoints + " con colisión" : "") + "\n\n" +
                    Wrap(result.advice, 45) + "\n" +
                    (string.IsNullOrEmpty(Course.ResultSaveError) ? "Resultado y trayectoria guardados (JSON/CSV)" : Course.ResultSaveError);
            }
            SetText(_list, body, 0.88f, 0.36f);
            SetText(_keys, "", 0.88f, 0.10f);
        }

        static string Wrap(string value, int columns)
        {
            var result = new System.Text.StringBuilder();
            int lineLength = 0;
            foreach (string word in value.Split(' '))
            {
                if (lineLength > 0 && lineLength + word.Length + 1 > columns)
                { result.Append('\n'); lineLength = 0; }
                if (lineLength > 0) { result.Append(' '); lineLength++; }
                result.Append(word); lineLength += word.Length;
            }
            return result.ToString();
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

        string BuildExerciseLine(int idx, string label)
        {
            bool sel = Course != null && Course.SelectedExercise == idx;
            return (sel ? "▶ " : "   ") + label;
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
            if (_doc != null) SafeDestroy(_doc.gameObject);
            if (_bgMat != null) SafeDestroy(_bgMat);
            if (_docBgMat != null) SafeDestroy(_docBgMat);
            if (_closeMat != null) SafeDestroy(_closeMat);
        }
    }
}
