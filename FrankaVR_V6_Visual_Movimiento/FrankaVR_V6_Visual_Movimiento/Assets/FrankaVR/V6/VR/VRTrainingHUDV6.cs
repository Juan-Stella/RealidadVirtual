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
        public float Width = 1.18f;
        public float Height = 0.35f;

        Transform _root;
        TextMesh _headline, _metrics, _help, _alert;
        Renderer _bg, _alertBg;
        Material _bgMat, _alertMat;

        void Start()
        {
            Resolve();
            Build();
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
            bg.transform.localScale = new Vector3(Width, Height, 1f);
            Destroy(bg.GetComponent<Collider>());
            _bg = bg.GetComponent<Renderer>();

            GameObject alertBox = GameObject.CreatePrimitive(PrimitiveType.Quad);
            alertBox.name = "HUD_AlertBox";
            alertBox.transform.SetParent(_root, false);
            alertBox.transform.localPosition = new Vector3(0f, -0.10f, -0.001f);
            alertBox.transform.localScale = new Vector3(Width * 0.92f, 0.10f, 1f);
            Destroy(alertBox.GetComponent<Collider>());
            _alertBg = alertBox.GetComponent<Renderer>();

            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _bgMat = new Material(sh); _bgMat.color = new Color(0.02f, 0.03f, 0.045f, 0.80f);
                _alertMat = new Material(sh); _alertMat.color = new Color(0.22f, 0.12f, 0.02f, 0.92f);
                _bg.sharedMaterial = _bgMat;
                _alertBg.sharedMaterial = _alertMat;
            }

            _headline = MakeText("Headline", new Vector3(0f, 0.10f, -0.004f), 56, 0.0087f, new Color(0.94f, 0.97f, 1f), TextAnchor.MiddleCenter);
            _metrics  = MakeText("Metrics",  new Vector3(0f, 0.01f, -0.004f), 52, 0.0078f, new Color(0.82f, 0.89f, 0.98f), TextAnchor.MiddleCenter);
            _help     = MakeText("Help",     new Vector3(0f, -0.04f, -0.004f), 42, 0.0062f, new Color(0.68f, 0.76f, 0.86f), TextAnchor.MiddleCenter);
            _alert    = MakeText("Alert",    new Vector3(0f, -0.10f, -0.005f), 58, 0.0082f, new Color(1f, 0.84f, 0.32f), TextAnchor.MiddleCenter);
        }

        TextMesh MakeText(string name, Vector3 localPos, int fontSize, float charSize, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.color = color;
            return tm;
        }

        void LateUpdate()
        {
            if (_root == null || PlayerCamera == null) return;
            Transform cam = PlayerCamera.transform;
            _root.position = cam.position + cam.forward * Distance + cam.up * VerticalOffset;
            _root.rotation = Quaternion.LookRotation(_root.position - cam.position, cam.up);
            UpdateText();
        }

        void UpdateText()
        {
            string phase = Course != null ? Course.PhaseLabel() : "SIMULADOR";
            string order = Course != null && Course.SelectedOrder != null ? Course.SelectedOrder.Id + " · " + Course.SelectedOrder.Title : "--";
            string mode = Course != null ? (Course.Mode == CourseModeV5.Training ? "Entrenamiento" : "Examen") : "--";
            string view = Viewpoints != null ? Viewpoints.CurrentLabel : "Vista";
            string input = HybridInput != null ? HybridInput.ActiveMotionSource : "Teclado";

            _headline.text = phase + "    |    " + order + "    |    " + mode;

            string arc = Torch != null && Torch.HasArcSurface ? Torch.ArcLenMm.ToString("0.0") + " mm" : "--";
            string rod = Torch != null ? Torch.RemainingMm.ToString("0") + " mm" : "--";
            string amps = Machine != null ? Machine.Amperage.ToString("0") + " A" : "--";
            string source = "Entrada: " + input + "    Vista: " + view;
            _metrics.text = "Arco  " + arc + "      Varilla  " + rod + "      Corriente  " + amps + "\n" + source;

            string help = "Robot: W A S D Q E · I J K L U O | Soldar: Espacio · R · Tab | Mover escena: Flechas + RePag/AvPag + Mouse der.";
            _help.text = help;

            string alert = ComputeAlert();
            _alert.text = alert;
            if (_alertMat != null)
                _alertMat.color = string.IsNullOrEmpty(alert) ? new Color(0.02f, 0.03f, 0.045f, 0.0f) : new Color(0.22f, 0.12f, 0.02f, 0.92f);
        }

        string ComputeAlert()
        {
            if (Torch == null) return "";
            if (Torch.State == ArcState.Stuck) return "ELECTRODO PEGADO — SEPARAR HERRAMIENTA";
            if (Machine != null && Machine.ThermalTripped) return "PROTECCION TERMICA — ESPERAR ENFRIAMIENTO";
            if (Torch.RemainingMm <= Torch.StubLengthMm + 10f) return "VARILLA CASI AGOTADA — CAMBIAR ELECTRODO";
            if (!Torch.HasArcSurface) return "";

            if (Torch.SparkRequested && Torch.ArcLenMm >= Torch.MaxArcLenMm) return "ACERQUE EL ELECTRODO PARA CEBAR";
            if (Torch.ArcLenMm < 1.2f) return "ARCO MUY CORTO";
            if (Machine != null && Machine.Current != null)
            {
                float ideal = Machine.Current.ArcLenIdealMm;
                if (Torch.ArcLenMm > ideal * 2.2f && Torch.ArcLenMm < Torch.MaxArcLenMm) return "ARCO LARGO";
                if (Torch.Arcing && Torch.ArcQuality > 0.72f) return "ARCO ESTABLE";
            }
            return "";
        }

        void OnDestroy()
        {
            if (_bgMat != null) Destroy(_bgMat);
            if (_alertMat != null) Destroy(_alertMat);
        }
    }
}
