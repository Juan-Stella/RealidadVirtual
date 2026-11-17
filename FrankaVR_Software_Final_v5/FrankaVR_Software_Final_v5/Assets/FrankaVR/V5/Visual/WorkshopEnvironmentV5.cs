using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V5
{
    /// <summary>
    /// Escenografia low-poly de taller. No agrega colliders a la decoracion para no interferir
    /// con el raycast del arco ni con el robot.
    /// </summary>
    public class WorkshopEnvironmentV5 : MonoBehaviour
    {
        public TrainingWorkpieceFactoryV5 Workpieces;
        public WeldingTorch Torch;
        public bool BuildOnStart = true;

        [Header("Aspecto")]
        public Color FloorColor = new Color(0.105f, 0.115f, 0.125f);
        public Color WallColor = new Color(0.16f, 0.175f, 0.19f);
        public Color TableColor = new Color(0.20f, 0.22f, 0.24f);
        public Color MachineColor = new Color(0.12f, 0.28f, 0.42f);
        public Color AccentColor = new Color(0.95f, 0.62f, 0.12f);

        Transform _root;
        Transform _machineCableStart;
        Material _floorMat, _wallMat, _tableMat, _machineMat, _accentMat, _darkMat;
        LineRenderer _torchCable;
        LineRenderer _groundCable;

        void Start()
        {
            if (Workpieces == null) Workpieces = FindObjectOfType<TrainingWorkpieceFactoryV5>();
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            if (BuildOnStart) Build();
        }

        public void Build()
        {
            ClearGenerated();
            BuildMaterials();

            Vector3 c = Workpieces != null ? Workpieces.WorkCenter : transform.position;
            float topY = Workpieces != null ? Workpieces.WorkTopY : c.y;

            GameObject rootGo = new GameObject("WorkshopSetV5");
            _root = rootGo.transform;
            _root.SetParent(transform, true);

            MakeDecorBox("Floor", new Vector3(c.x, topY - 0.82f, c.z), new Vector3(4.2f, 0.06f, 3.4f), _floorMat);
            MakeDecorBox("BackWall", new Vector3(c.x, topY + 0.45f, c.z + 1.35f), new Vector3(4.2f, 2.45f, 0.06f), _wallMat);

            // Mesa: superficie justo debajo de la antigua chapa.
            float tableTopY = topY - 0.035f;
            MakeDecorBox("WeldingTableTop", new Vector3(c.x, tableTopY - 0.025f, c.z), new Vector3(0.92f, 0.05f, 0.68f), _tableMat);
            float legY = topY - 0.42f;
            MakeDecorBox("Leg_FL", new Vector3(c.x - 0.39f, legY, c.z - 0.27f), new Vector3(0.055f, 0.72f, 0.055f), _tableMat);
            MakeDecorBox("Leg_FR", new Vector3(c.x + 0.39f, legY, c.z - 0.27f), new Vector3(0.055f, 0.72f, 0.055f), _tableMat);
            MakeDecorBox("Leg_BL", new Vector3(c.x - 0.39f, legY, c.z + 0.27f), new Vector3(0.055f, 0.72f, 0.055f), _tableMat);
            MakeDecorBox("Leg_BR", new Vector3(c.x + 0.39f, legY, c.z + 0.27f), new Vector3(0.055f, 0.72f, 0.055f), _tableMat);

            // Soldadora a un costado.
            Vector3 mp = new Vector3(c.x - 0.80f, topY - 0.18f, c.z + 0.20f);
            MakeDecorBox("WelderBody", mp, new Vector3(0.34f, 0.46f, 0.30f), _machineMat);
            MakeDecorBox("WelderFront", mp + new Vector3(0f, 0.02f, -0.158f), new Vector3(0.29f, 0.34f, 0.015f), _darkMat);
            MakeDecorBox("WelderAccent", mp + new Vector3(0f, 0.14f, -0.168f), new Vector3(0.21f, 0.035f, 0.008f), _accentMat);
            MakeDecorBox("WelderHandle", mp + new Vector3(0f, 0.27f, 0f), new Vector3(0.18f, 0.025f, 0.08f), _darkMat);

            GameObject cableStart = new GameObject("MachineCableStartV5");
            cableStart.transform.SetParent(_root, true);
            cableStart.transform.position = mp + new Vector3(0.10f, -0.06f, -0.17f);
            _machineCableStart = cableStart.transform;

            // Pinza de masa decorativa sobre el borde de mesa.
            Vector3 clamp = new Vector3(c.x + 0.34f, topY + 0.015f, c.z + 0.22f);
            MakeDecorBox("GroundClamp", clamp, new Vector3(0.10f, 0.025f, 0.035f), _accentMat);

            _torchCable = MakeCable("TorchCableV5", new Color(0.035f, 0.035f, 0.04f), 0.012f);
            _groundCable = MakeCable("GroundCableV5", new Color(0.035f, 0.035f, 0.04f), 0.010f);
            UpdateCables(c, clamp);

            MakeSafetySign(new Vector3(c.x + 0.55f, topY + 0.55f, c.z + 1.30f));
        }

        void BuildMaterials()
        {
            if (_floorMat != null) return;
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Diffuse");
            if (sh == null) return;
            _floorMat = Mat(sh, FloorColor, 0.05f, 0.15f);
            _wallMat = Mat(sh, WallColor, 0.02f, 0.12f);
            _tableMat = Mat(sh, TableColor, 0.65f, 0.22f);
            _machineMat = Mat(sh, MachineColor, 0.15f, 0.25f);
            _accentMat = Mat(sh, AccentColor, 0.10f, 0.28f);
            _darkMat = Mat(sh, new Color(0.035f, 0.04f, 0.045f), 0.0f, 0.10f);
        }

        Material Mat(Shader sh, Color c, float metal, float smooth)
        {
            Material m = new Material(sh); m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }

        GameObject MakeDecorBox(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        LineRenderer MakeCable(string name, Color c, float width)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 7;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = width;
            lr.numCapVertices = 4;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                Material m = new Material(sh); m.color = c;
                lr.material = m;
            }
            return lr;
        }

        void UpdateCables(Vector3 center, Vector3 clamp)
        {
            if (_groundCable != null && _machineCableStart != null)
                SetSagCable(_groundCable, _machineCableStart.position, clamp, 0.20f);

            if (_torchCable != null && _machineCableStart != null && Torch != null)
                SetSagCable(_torchCable, _machineCableStart.position, Torch.transform.position, 0.28f);
        }

        static void SetSagCable(LineRenderer lr, Vector3 a, Vector3 b, float sag)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                lr.SetPosition(i, p);
            }
        }

        void MakeSafetySign(Vector3 pos)
        {
            GameObject bg = MakeDecorBox("SafetySign", pos, new Vector3(0.44f, 0.24f, 0.015f), _darkMat);
            GameObject tg = new GameObject("SafetyText");
            tg.transform.SetParent(bg.transform, false);
            tg.transform.localPosition = new Vector3(0f, 0f, -0.53f);
            TextMesh tm = tg.AddComponent<TextMesh>();
            tm.text = "AREA DE SOLDADURA\nPROTECCION OCULAR OBLIGATORIA";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.characterSize = 0.045f;
            tm.color = new Color(1f, 0.78f, 0.18f);
        }

        void LateUpdate()
        {
            if (_root == null) return;
            Vector3 c = Workpieces != null ? Workpieces.WorkCenter : transform.position;
            Vector3 clamp = new Vector3(c.x + 0.34f, Workpieces != null ? Workpieces.WorkTopY + 0.015f : c.y, c.z + 0.22f);
            UpdateCables(c, clamp);
        }

        public void ClearGenerated()
        {
            if (_root == null) return;
            if (Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject);
            _root = null;
        }

        void OnDestroy()
        {
            Material[] mats = { _floorMat, _wallMat, _tableMat, _machineMat, _accentMat, _darkMat };
            foreach (Material m in mats) if (m != null) Destroy(m);
        }
    }
}
