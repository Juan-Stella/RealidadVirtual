using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// HUD minimo en world-space frente a la cabeza. Mantiene lo importante siempre legible
    /// sin obligar al operador a girar hacia un panel lateral mientras esta soldando.
    /// </summary>
    public class CardboardStatusHUD : MonoBehaviour
    {
        public Camera PlayerCamera;
        public WeldingTorch Torch;
        public WeldingMachine Machine;
        public WeldingTaskManager Task;
        public ViewpointManager Viewpoints;

        [Header("Colocacion")]
        public float Distance = 1.35f;
        public float VerticalOffset = -0.38f;
        public float HorizontalOffset = 0f;

        [Header("Aspecto")]
        public int FontSize = 54;
        public float CharacterSize = 0.009f;
        public Color TextColor = new Color(0.94f, 0.97f, 1f, 1f);
        public Color BackgroundColor = new Color(0.015f, 0.025f, 0.04f, 0.72f);

        Transform _panel;
        TextMesh _text;
        Renderer _bgRenderer;
        Material _bgMat;

        void Start()
        {
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            if (Machine == null) Machine = FindObjectOfType<WeldingMachine>();
            if (Task == null) Task = FindObjectOfType<WeldingTaskManager>();
            if (Viewpoints == null) Viewpoints = FindObjectOfType<ViewpointManager>();
            Build();
        }

        void Build()
        {
            if (PlayerCamera == null) return;
            GameObject root = new GameObject("CardboardStatusHUD");
            _panel = root.transform;

            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            Collider col = bg.GetComponent<Collider>();
            if (col != null) Destroy(col);
            bg.transform.SetParent(_panel, false);
            bg.transform.localScale = new Vector3(0.90f, 0.24f, 1f);
            _bgRenderer = bg.GetComponent<Renderer>();

            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh != null)
            {
                _bgMat = new Material(sh);
                _bgMat.color = BackgroundColor;
                _bgRenderer.material = _bgMat;
            }

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(_panel, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.003f);
            _text = textGo.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontSize = FontSize;
            _text.characterSize = CharacterSize;
            _text.color = TextColor;
            Renderer tr = textGo.GetComponent<Renderer>();
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (PlayerCamera == null || _panel == null || _text == null) return;

            Transform cam = PlayerCamera.transform;
            _panel.position = cam.position + cam.forward * Distance + cam.up * VerticalOffset + cam.right * HorizontalOffset;
            _panel.rotation = Quaternion.LookRotation(_panel.position - cam.position, cam.up);

            string station = Viewpoints != null ? Viewpoints.CurrentLabel : "Operador";
            string arc = Torch != null ? Torch.StateLabel() : "--";
            string len = Torch != null && Torch.HasArcSurface ? Torch.ArcLenMm.ToString("0.0") + " mm" : "--";
            string rod = Torch != null ? Torch.RemainingMm.ToString("0") + " mm" : "--";
            string amps = Machine != null ? Machine.Amperage.ToString("0") + " A" : "--";

            _text.text = station + "   |   " + arc + "\n" +
                         "Arco " + len + "   •   Varilla " + rod + "   •   " + amps;
        }

        void OnDestroy()
        {
            if (_bgMat != null) Destroy(_bgMat);
        }
    }
}
