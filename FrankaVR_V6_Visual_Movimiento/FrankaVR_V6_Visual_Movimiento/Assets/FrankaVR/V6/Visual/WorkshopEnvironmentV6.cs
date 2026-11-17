using UnityEngine;
using FrankaVR.V5;
using FrankaVR.Welding;

namespace FrankaVR.V6
{
    /// <summary>
    /// Escenografia mas cuidada y legible, con panel tecnico y acentos de taller.
    /// </summary>
    public class WorkshopEnvironmentV6 : MonoBehaviour
    {
        public TrainingWorkpieceFactoryV5 Workpieces;
        public WeldingTorch Torch;
        public bool BuildOnStart = true;

        [Header("Aspecto")]
        public Color FloorColor = new Color(0.10f, 0.11f, 0.12f);
        public Color WallColor = new Color(0.14f, 0.15f, 0.17f);
        public Color TableColor = new Color(0.23f, 0.24f, 0.26f);
        public Color MachineColor = new Color(0.09f, 0.24f, 0.38f);
        public Color AccentColor = new Color(0.95f, 0.58f, 0.12f);
        public Color PanelColor = new Color(0.02f, 0.03f, 0.05f);

        Transform _root;
        Material _floorMat, _wallMat, _tableMat, _machineMat, _accentMat, _darkMat, _panelMat;
        LineRenderer _torchCable, _groundCable;
        Transform _machineCableStart;

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

            GameObject rootGo = new GameObject("WorkshopSetV6");
            _root = rootGo.transform;
            _root.SetParent(transform, true);

            MakeBox("Floor", new Vector3(c.x, topY - 0.83f, c.z), new Vector3(5.0f, 0.06f, 4.2f), _floorMat);
            MakeBox("BackWall", new Vector3(c.x, topY + 0.52f, c.z + 1.55f), new Vector3(5.0f, 2.55f, 0.06f), _wallMat);
            MakeBox("LeftWallStrip", new Vector3(c.x - 2.0f, topY + 0.52f, c.z + 0.2f), new Vector3(0.06f, 2.55f, 2.8f), _wallMat);

            float tableTopY = topY - 0.035f;
            MakeBox("TableTop", new Vector3(c.x, tableTopY - 0.025f, c.z), new Vector3(1.08f, 0.05f, 0.74f), _tableMat);
            MakeBox("TableShelf", new Vector3(c.x, topY - 0.47f, c.z), new Vector3(0.86f, 0.03f, 0.48f), _darkMat);
            float legY = topY - 0.42f;
            MakeBox("Leg_FL", new Vector3(c.x - 0.46f, legY, c.z - 0.30f), new Vector3(0.05f, 0.72f, 0.05f), _tableMat);
            MakeBox("Leg_FR", new Vector3(c.x + 0.46f, legY, c.z - 0.30f), new Vector3(0.05f, 0.72f, 0.05f), _tableMat);
            MakeBox("Leg_BL", new Vector3(c.x - 0.46f, legY, c.z + 0.30f), new Vector3(0.05f, 0.72f, 0.05f), _tableMat);
            MakeBox("Leg_BR", new Vector3(c.x + 0.46f, legY, c.z + 0.30f), new Vector3(0.05f, 0.72f, 0.05f), _tableMat);

            Vector3 mp = new Vector3(c.x - 0.90f, topY - 0.16f, c.z + 0.22f);
            MakeBox("WelderBody", mp, new Vector3(0.36f, 0.44f, 0.32f), _machineMat);
            MakeBox("WelderFront", mp + new Vector3(0f, 0.01f, -0.17f), new Vector3(0.30f, 0.32f, 0.015f), _darkMat);
            MakeBox("WelderDisplay", mp + new Vector3(0f, 0.10f, -0.178f), new Vector3(0.15f, 0.05f, 0.008f), _accentMat);
            MakeBox("WelderHandle", mp + new Vector3(0f, 0.25f, 0f), new Vector3(0.18f, 0.02f, 0.08f), _darkMat);

            GameObject cs = new GameObject("MachineCableStartV6");
            cs.transform.SetParent(_root, true);
            cs.transform.position = mp + new Vector3(0.12f, -0.05f, -0.17f);
            _machineCableStart = cs.transform;

            Vector3 clamp = new Vector3(c.x + 0.39f, topY + 0.018f, c.z + 0.24f);
            MakeBox("GroundClamp", clamp, new Vector3(0.11f, 0.024f, 0.038f), _accentMat);

            _torchCable = MakeCable("TorchCableV6", new Color(0.03f, 0.03f, 0.04f), 0.012f);
            _groundCable = MakeCable("GroundCableV6", new Color(0.03f, 0.03f, 0.04f), 0.010f);
            UpdateCables(clamp);

            MakeSafetySign(new Vector3(c.x + 0.75f, topY + 0.62f, c.z + 1.48f));
            MakeInfoStand(new Vector3(c.x + 1.05f, topY - 0.02f, c.z - 0.28f));
        }

        void BuildMaterials()
        {
            if (_floorMat != null) return;
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Diffuse");
            _floorMat = Mat(sh, FloorColor, 0.04f, 0.15f);
            _wallMat = Mat(sh, WallColor, 0.02f, 0.12f);
            _tableMat = Mat(sh, TableColor, 0.70f, 0.22f);
            _machineMat = Mat(sh, MachineColor, 0.18f, 0.25f);
            _accentMat = Mat(sh, AccentColor, 0.12f, 0.32f);
            _darkMat = Mat(sh, new Color(0.04f, 0.045f, 0.05f), 0f, 0.08f);
            _panelMat = Mat(sh, PanelColor, 0f, 0.04f);
        }

        Material Mat(Shader sh, Color c, float metal, float smooth)
        {
            Material m = new Material(sh); m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }

        GameObject MakeBox(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            return go;
        }

        LineRenderer MakeCable(string name, Color color, float width)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 8;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = width;
            lr.numCapVertices = 4;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                var m = new Material(sh); m.color = color;
                lr.material = m;
            }
            return lr;
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

        void UpdateCables(Vector3 clamp)
        {
            if (_machineCableStart == null) return;
            if (_groundCable != null) SetSagCable(_groundCable, _machineCableStart.position, clamp, 0.20f);
            if (_torchCable != null && Torch != null) SetSagCable(_torchCable, _machineCableStart.position, Torch.transform.position, 0.28f);
        }

        void MakeSafetySign(Vector3 pos)
        {
            GameObject bg = MakeBox("SafetySignV6", pos, new Vector3(0.52f, 0.30f, 0.015f), _darkMat);
            var t = new GameObject("SafetyText");
            t.transform.SetParent(bg.transform, false);
            t.transform.localPosition = new Vector3(0f, 0f, -0.53f);
            var tm = t.AddComponent<TextMesh>();
            tm.text = "AREA DE SOLDADURA\nUSO DE CARETA OBLIGATORIO";
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.fontSize = 50; tm.characterSize = 0.040f;
            tm.color = new Color(1f, 0.82f, 0.18f);
        }

        void MakeInfoStand(Vector3 pos)
        {
            MakeBox("InfoStandPole", pos + new Vector3(0f, -0.16f, 0f), new Vector3(0.05f, 0.38f, 0.05f), _darkMat);
            GameObject board = MakeBox("InfoStandBoard", pos + new Vector3(0f, 0.10f, 0f), new Vector3(0.52f, 0.30f, 0.02f), _panelMat);
            var textGo = new GameObject("InfoStandText");
            textGo.transform.SetParent(board.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = "SIMULADOR DE SOLDADURA\n\nEjercicios N1 a N5\nModo entrenamiento y examen\nControl por teclado, Cardboard y guante IMU";
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.fontSize = 42; tm.characterSize = 0.030f;
            tm.color = new Color(0.92f, 0.96f, 1f);
        }

        void LateUpdate()
        {
            if (_root == null) return;
            Vector3 c = Workpieces != null ? Workpieces.WorkCenter : transform.position;
            Vector3 clamp = new Vector3(c.x + 0.39f, Workpieces != null ? Workpieces.WorkTopY + 0.018f : c.y, c.z + 0.24f);
            UpdateCables(clamp);
        }

        public void ClearGenerated()
        {
            if (_root == null) return;
            if (Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject);
            _root = null;
        }

        void OnDestroy()
        {
            var mats = new[] { _floorMat, _wallMat, _tableMat, _machineMat, _accentMat, _darkMat, _panelMat };
            foreach (var m in mats) if (m != null) Destroy(m);
        }
    }
}
