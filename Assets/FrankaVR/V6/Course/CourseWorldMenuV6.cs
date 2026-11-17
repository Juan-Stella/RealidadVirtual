using UnityEngine;
using FrankaVR.V5;
using FrankaVR.Welding;

namespace FrankaVR.V6
{
    /// <summary>
    /// Panel lateral limpio con resumen del curso y atajos.
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

        Transform _root;
        TextMesh _title, _exercise, _list, _keys;
        Material _bgMat;
        VRTrainingHUDV6 _hud;
        DesktopNavigatorV6 _navigator;
        CoursePhaseV5 _lastPhase;
        GazeButton[] _exerciseButtons = new GazeButton[5];

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
        }

        TextMesh MakeText(string name, Vector3 localPos, int fontSize, float charSize, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.color = color;
            go.GetComponent<Renderer>().sortingOrder = 10;
            return tm;
        }

        void LateUpdate()
        {
            if (_root == null || PlayerCamera == null) return;
            if (Course != null)
            {
                if (Course.Phase == CoursePhaseV5.Results && _lastPhase != CoursePhaseV5.Results && _hud != null)
                    _hud.ShowHelpDuringRunning = false;
                _lastPhase = Course.Phase;
            }

            bool help = _hud != null && _hud.ShowHelpDuringRunning;
            bool shouldShow = help || !(HideWhileRunning && Course != null && Course.Phase == CoursePhaseV5.Running);
            if (_root.gameObject.activeSelf != shouldShow) _root.gameObject.SetActive(shouldShow);
            if (!shouldShow) return;

            Transform cam = PlayerCamera.transform;
            Transform body = Application.platform == RuntimePlatform.Android && cam.parent != null ? cam.parent : cam;
            float scale = _navigator != null ? _navigator.InterfaceScale : 1f;
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
            bool selection = Course != null && Course.Phase == CoursePhaseV5.Selection &&
                (_hud == null || !_hud.ShowHelpDuringRunning);
            foreach (GazeButton button in _exerciseButtons) if (button != null) button.gameObject.SetActive(selection);
            if (selection) body = "";

            if (Course != null && Course.Phase == CoursePhaseV5.Briefing && Course.SelectedOrder != null)
            {
                WorkOrder order = Course.SelectedOrder;
                body = Wrap(order.Brief, 45) + "\n\n" +
                    "1. ENSEÑAR: con Espacio sobre la junta, recorré la\ncostura con la torcha. Queda grabada.\n" +
                    "2. EJECUTAR (Enter): el robot la repite con arco.\n\n" +
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
            if (_hud != null && _hud.ShowHelpDuringRunning)
                body = "ROBOT  WASD/QE: mover (acelera al mantener)\n" +
                    "I/K J/L: inclinar · U/O: girar · V: enderezar\n" +
                    "ENSEÑAR  Espacio sobre la junta: graba y las\n" +
                    "teclas siguen la costura · Q/E: distancia\n" +
                    "Z: deshacer trazo · R: borrar · Enter: ejecutar\n" +
                    "Práctica: B ir al inicio · V ángulo recomendado\n" +
                    "Tab: alambre · [ / ]: corriente · - / +: velocidad\n" +
                    "C: retirar / abortar · Ctrl: fino · Shift: rápido\n" +
                    "VISTA  Flechas/Pag · Mouse derecho · Rueda: zoom\n" +
                    "N/P: estaciones · H: careta · M: menú\n" +
                    "F: finalizar · F1: cerrar ayuda";
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
            if (_bgMat != null) SafeDestroy(_bgMat);
        }
    }
}
