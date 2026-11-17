using UnityEngine;
using FrankaVR.Welding;
using FrankaVR.Teleop;

namespace FrankaVR.V5
{
    /// <summary>HUD mundial compacto + alertas contextuales para entrenamiento.</summary>
    public class VRTrainingHUDV5 : MonoBehaviour
    {
        public Camera PlayerCamera;
        public CourseTrainingManagerV5 Course;
        public WeldingTaskManager Task;
        public WeldingTorch Torch;
        public WeldingMachine Machine;
        public HybridTeleopInput HybridInput;
        public ViewpointManager Viewpoints;

        [Header("Colocacion")]
        public float Distance = 1.40f;
        public float VerticalOffset = -0.40f;
        public float CharacterSize = 0.0085f;

        Transform _root;
        TextMesh _status;
        TextMesh _alert;
        Material _bgMat;

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
        }

        void Build()
        {
            if (PlayerCamera == null) return;
            GameObject root = new GameObject("VRTrainingHUDV5_Runtime");
            _root = root.transform;

            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "HUDBackground";
            bg.transform.SetParent(_root, false);
            bg.transform.localScale = new Vector3(1.02f, 0.26f, 1f);
            Collider c = bg.GetComponent<Collider>(); if (c != null) Destroy(c);
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _bgMat = new Material(sh); _bgMat.color = new Color(0.015f, 0.022f, 0.032f, 0.74f);
                bg.GetComponent<Renderer>().material = _bgMat;
            }

            _status = MakeText("Status", new Vector3(0f, 0.025f, -0.004f), 50, CharacterSize, new Color(0.93f, 0.96f, 1f));
            _alert  = MakeText("Alert", new Vector3(0f, -0.065f, -0.005f), 56, CharacterSize * 1.08f, new Color(1f, 0.72f, 0.16f));
        }

        TextMesh MakeText(string name, Vector3 localPos, int font, float size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = font;
            tm.characterSize = size;
            tm.color = color;
            return tm;
        }

        void LateUpdate()
        {
            if (PlayerCamera == null || _root == null) return;
            Transform cam = PlayerCamera.transform;
            _root.position = cam.position + cam.forward * Distance + cam.up * VerticalOffset;
            _root.rotation = Quaternion.LookRotation(_root.position - cam.position, cam.up);
            UpdateText();
        }

        void UpdateText()
        {
            string order = Course != null && Course.SelectedOrder != null ? Course.SelectedOrder.Id : "--";
            string phase = Course != null ? Course.PhaseLabel() : "SIMULADOR";
            string view = Viewpoints != null ? Viewpoints.CurrentLabel : "Operador";
            string input = HybridInput != null ? HybridInput.ActiveMotionSource : "Teclado";
            string arc = Torch != null && Torch.HasArcSurface ? Torch.ArcLenMm.ToString("0.0") + " mm" : "--";
            string rod = Torch != null ? Torch.RemainingMm.ToString("0") + " mm" : "--";
            string amps = Machine != null ? Machine.Amperage.ToString("0") + " A" : "--";

            if (_status != null)
                _status.text = phase + "  |  " + order + "  |  " + view + "  |  " + input + "\n" +
                               "Arco " + arc + "   •   Varilla " + rod + "   •   " + amps;

            if (_alert != null)
                _alert.text = AlertText();
        }

        string AlertText()
        {
            if (Torch == null) return "";
            if (Torch.State == ArcState.Stuck) return "ELECTRODO PEGADO — SEPARE LA HERRAMIENTA";
            if (Machine != null && Machine.ThermalTripped) return "PROTECCION TERMICA — ESPERE ENFRIAMIENTO";
            if (Torch.RemainingMm <= Torch.StubLengthMm + 8f) return "CAMBIAR ELECTRODO";

            bool training = Course == null || Course.Mode == CourseModeV5.Training;
            if (!training || Course == null || Course.Phase != CoursePhaseV5.Running) return "";
            if (!Torch.HasArcSurface) return "";

            float ideal = Machine != null && Machine.Current != null ? Machine.Current.ArcLenIdealMm : 3.0f;
            if (Torch.ArcLenMm < Mathf.Max(0.8f, ideal * 0.45f)) return "ARCO MUY CORTO — SEPARE UN POCO";
            if (Torch.ArcLenMm > ideal * 2.4f && Torch.ArcLenMm < Torch.MaxArcLenMm) return "ARCO LARGO — ACERQUE EL ELECTRODO";
            if (Torch.SparkRequested && Torch.ArcLenMm >= Torch.MaxArcLenMm) return "ACERQUE PARA CEBAR";
            if (Torch.Arcing && Torch.ArcQuality > 0.70f) return "ARCO ESTABLE";
            return "";
        }

        void OnDestroy() { if (_bgMat != null) Destroy(_bgMat); }
    }
}
