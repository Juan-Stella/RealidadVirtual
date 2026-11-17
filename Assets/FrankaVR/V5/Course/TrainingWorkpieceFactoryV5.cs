using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V5
{
    /// <summary>
    /// Genera las piezas de practica N1..N5 con geometria simple y barata para mobile.
    /// Mantiene la superficie de trabajo cerca de la altura de la Chapa original para no
    /// cambiar el alcance del Panda que ya fue calibrado.
    /// </summary>
    public class TrainingWorkpieceFactoryV5 : MonoBehaviour
    {
        [Header("Referencias")]
        public WeldSeam Seam;
        public Transform LegacyChapa;
        public Transform CustomOrigin;

        [Header("Comportamiento")]
        public bool HideLegacyChapa = true;
        public bool BuildOnStart = false;
        public string InitialOrderId = "N1";

        [Header("Dimensiones visuales")]
        public float PlateLength = 0.32f;
        [Tooltip("Costuras rectas de afuera hacia la base del robot. Con arrastre el mango se inclina " +
                 "hacia el avance: asi el flange queda cerca y el codo flexionado con el electrodo de 350 mm.")]
        public bool WeldTowardRobot = true;
        public float PlateWidth = 0.18f;
        public float ButtGap = 0.004f;
        public float PipeSectionLength = 0.18f;
        public float PipeGap = 0.005f;
        public float PipeInnerRatio = 0.82f;
        public int LineSamples = 60;
        public int CircleSamples = 84;

        [Header("Materiales")]
        public Color SteelColor = new Color(0.30f, 0.32f, 0.35f, 1f);
        public Color EdgeColor = new Color(0.10f, 0.11f, 0.12f, 1f);
        public Color SupportColor = new Color(0.16f, 0.18f, 0.20f, 1f);

        Transform _root;
        Transform _start;
        Transform _end;
        Transform _circleCenter;
        Material _steelMat, _edgeMat, _supportMat;
        int _workpieceLayer;
        float _referenceTopY;
        Vector3 _referenceCenter;

        public Transform CurrentRoot => _root;
        public Vector3 WorkCenter => _referenceCenter;
        public float WorkTopY => _referenceTopY;
        public float PipeRotationDegrees { get; private set; }

        public bool IndexPipe(float degrees)
        {
            if (_root == null || _circleCenter == null || Seam == null || Seam.Shape != SeamShape.Circle) return false;
            Vector3 center = _circleCenter.position;
            foreach (Transform part in _root)
            {
                if (part.name.StartsWith("Pipe_", System.StringComparison.Ordinal) ||
                    part.name.StartsWith("PipeCut_", System.StringComparison.Ordinal))
                    part.RotateAround(center, Seam.CircleAxis.normalized, degrees);
            }
            Seam.RotateCircleSamples(degrees);
            PipeRotationDegrees = Mathf.Repeat(PipeRotationDegrees + degrees, 360f);
            Physics.SyncTransforms();
            return true;
        }

        void Awake()
        {
            ResolveReferences();
            ResolveReferenceSurface();
        }

        void Start()
        {
            if (BuildOnStart) BuildById(InitialOrderId);
        }

        void ResolveReferences()
        {
            if (Seam == null) Seam = FindObjectOfType<WeldSeam>();
            if (LegacyChapa == null)
            {
                GameObject go = GameObject.Find("Chapa");
                if (go != null) LegacyChapa = go.transform;
            }
        }

        void ResolveReferenceSurface()
        {
            ResolveReferences();
            _workpieceLayer = LegacyChapa != null ? LegacyChapa.gameObject.layer : gameObject.layer;

            if (CustomOrigin != null)
            {
                _referenceCenter = CustomOrigin.position;
                _referenceTopY = CustomOrigin.position.y;
                return;
            }

            if (LegacyChapa != null)
            {
                Renderer r = LegacyChapa.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    _referenceCenter = r.bounds.center;
                    _referenceTopY = r.bounds.max.y;
                    return;
                }
                _referenceCenter = LegacyChapa.position;
                _referenceTopY = LegacyChapa.position.y;
                return;
            }

            _referenceCenter = transform.position;
            _referenceTopY = transform.position.y;
        }

        public void BuildById(string id)
        {
            BuildFor(WorkOrder.ById(id));
        }

        public void BuildFor(WorkOrder order)
        {
            if (order == null) return;
            PipeRotationDegrees = 0f;
            ResolveReferenceSurface();
            ClearGenerated();
            BuildMaterials();

            GameObject rootGo = new GameObject("TrainingWorkpieceV5_" + order.Id);
            _root = rootGo.transform;
            _root.SetParent(transform, true);
            _root.position = Vector3.zero;
            _root.rotation = Quaternion.identity;

            if (HideLegacyChapa && LegacyChapa != null)
                LegacyChapa.gameObject.SetActive(false);

            switch (order.Joint)
            {
                case JointType.BeadOnPlate: BuildBeadOnPlate(order); break;
                case JointType.ButtJoint:   BuildButtJoint(order); break;
                case JointType.FilletT:     BuildTJoint(order); break;
                case JointType.LapJoint:    BuildLapJoint(order); break;
                case JointType.PipeButt:    BuildPipe(order); break;
                default:                    BuildBeadOnPlate(order); break;
            }

            ConfigureSeam(order);
        }

        void BuildBeadOnPlate(WorkOrder o)
        {
            float t = Mathf.Max(0.002f, o.ThicknessMm * 0.001f);
            Vector3 c = new Vector3(_referenceCenter.x, _referenceTopY - t * 0.5f, _referenceCenter.z);
            MakeBox("Plate_N1", c, new Vector3(PlateLength, t, PlateWidth), _steelMat, true);
            MakeLineMarkers(o.SeamLengthM, new Vector3(_referenceCenter.x, _referenceTopY + 0.0015f, _referenceCenter.z));
        }

        void BuildButtJoint(WorkOrder o)
        {
            float t = Mathf.Max(0.002f, o.ThicknessMm * 0.001f);
            float halfW = (PlateWidth - ButtGap) * 0.25f;
            float zOff = halfW + ButtGap * 0.5f;
            float y = _referenceTopY - t * 0.5f;

            MakeBox("ButtPlate_A", new Vector3(_referenceCenter.x, y, _referenceCenter.z - zOff),
                new Vector3(PlateLength, t, halfW * 2f), _steelMat, true);
            MakeBox("ButtPlate_B", new Vector3(_referenceCenter.x, y, _referenceCenter.z + zOff),
                new Vector3(PlateLength, t, halfW * 2f), _steelMat, true);
            // Raiz didactica de la junta, ligeramente hundida. Da una superficie
            // soldable al centro del hueco, igual que la raiz simplificada del caño.
            MakeBox("Butt_Root", new Vector3(_referenceCenter.x, _referenceTopY - 0.002f, _referenceCenter.z),
                new Vector3(PlateLength, 0.002f, ButtGap), _edgeMat, true);
            MakeLineMarkers(o.SeamLengthM, new Vector3(_referenceCenter.x, _referenceTopY + 0.0015f, _referenceCenter.z));
        }

        void BuildTJoint(WorkOrder o)
        {
            float t = Mathf.Max(0.004f, o.ThicknessMm * 0.001f);
            float baseY = _referenceTopY - t * 0.5f;
            MakeBox("T_Base", new Vector3(_referenceCenter.x, baseY, _referenceCenter.z),
                new Vector3(PlateLength, t, PlateWidth), _steelMat, true);

            float verticalHeight = 0.085f;
            MakeBox("T_Vertical", new Vector3(_referenceCenter.x, _referenceTopY + verticalHeight * 0.5f, _referenceCenter.z),
                new Vector3(PlateLength, verticalHeight, t), _steelMat, true);

            // Con avance +X, el angulo de trabajo +45 grados apunta el mango
            // hacia -Z. Soldar ese lado evita atravesar la chapa vertical.
            float seamZ = _referenceCenter.z - t * 0.55f;
            MakeLineMarkers(o.SeamLengthM, new Vector3(_referenceCenter.x, _referenceTopY + 0.002f, seamZ));
        }

        void BuildLapJoint(WorkOrder o)
        {
            float t = Mathf.Max(0.0015f, o.ThicknessMm * 0.001f);
            float baseY = _referenceTopY - t * 0.5f;
            MakeBox("Lap_Bottom", new Vector3(_referenceCenter.x, baseY, _referenceCenter.z),
                new Vector3(PlateLength, t, PlateWidth), _steelMat, true);

            float upperW = PlateWidth * 0.58f;
            float upperZ = _referenceCenter.z + (PlateWidth - upperW) * 0.25f;
            MakeBox("Lap_Upper", new Vector3(_referenceCenter.x, _referenceTopY + t * 0.5f, upperZ),
                new Vector3(PlateLength * 0.92f, t, upperW), _steelMat, true);

            float seamZ = upperZ - upperW * 0.5f;
            MakeLineMarkers(o.SeamLengthM, new Vector3(_referenceCenter.x, _referenceTopY + t + 0.0015f, seamZ));
        }

        void BuildPipe(WorkOrder o)
        {
            float r = Mathf.Max(0.025f, o.PipeRadiusM);
            // Apoyado sobre la antigua superficie para mantenerlo dentro del volumen util del Panda.
            Vector3 center = new Vector3(_referenceCenter.x, _referenceTopY + r + 0.012f, _referenceCenter.z);
            float xOff = PipeGap * 0.5f + PipeSectionLength * 0.5f;

            MakePipeSection("Pipe_Left", center + Vector3.left * xOff, r, PipeSectionLength);
            MakePipeSection("Pipe_Right", center + Vector3.right * xOff, r, PipeSectionLength);
            // Raiz simplificada de la junta: sin ella el raycast radial cae en el
            // hueco entre tramos, aunque la costura este a la distancia correcta.
            GameObject joint = MakePipeSection("Pipe_JointRoot", center, r, PipeGap);
            joint.GetComponent<Renderer>().sharedMaterial = _edgeMat;
            MakePipeCut("PipeCut_Left", center + Vector3.left * (PipeGap * 0.5f + 0.0007f), r * PipeInnerRatio);
            MakePipeCut("PipeCut_Right", center + Vector3.right * (PipeGap * 0.5f + 0.0007f), r * PipeInnerRatio);
            MakePipeSupport("PipeSupport_Left", center + Vector3.left * (PipeSectionLength * 0.58f), r);
            MakePipeSupport("PipeSupport_Right", center + Vector3.right * (PipeSectionLength * 0.58f), r);

            GameObject cg = new GameObject("SeamCircleCenterV5");
            cg.transform.SetParent(_root, true);
            cg.transform.position = center;
            _circleCenter = cg.transform;
        }

        void MakeLineMarkers(float seamLength, Vector3 center)
        {
            float len = Mathf.Clamp(seamLength > 0f ? seamLength : 0.20f, 0.08f, PlateLength * 0.88f);
            Vector3 half = (WeldTowardRobot ? Vector3.right : Vector3.left) * len * 0.5f;
            _start = MakeMarker("SeamStartV5", center + half);
            _end   = MakeMarker("SeamEndV5", center - half);
        }

        Transform MakeMarker(string name, Vector3 p)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, true);
            go.transform.position = p;
            go.transform.rotation = Quaternion.identity;
            return go.transform;
        }

        void ConfigureSeam(WorkOrder o)
        {
            if (Seam == null) return;

            Seam.Shape = o.Shape;
            Seam.ThicknessMm = o.ThicknessMm;
            Seam.IdealTravelAngle = o.IdealTravelAngle;
            // Invertir el avance invierte el lado (n x t): mismo lado fisico de inclinacion.
            Seam.ReverseWorkSide = o.Shape == SeamShape.Line && WeldTowardRobot;
            Seam.IdealWorkAngle = Seam.ReverseWorkSide ? -o.IdealWorkAngle : o.IdealWorkAngle;
            Seam.AngleToleranceDeg = o.AngleToleranceDeg;

            if (o.Shape == SeamShape.Circle)
            {
                Seam.CircleCenter = _circleCenter;
                Seam.CircleAxis = Vector3.right;
                Seam.CircleRadius = o.PipeRadiusM;
                Seam.Samples = Mathf.Max(2, CircleSamples);
            }
            else
            {
                Seam.Start = _start;
                Seam.End = _end;
                Seam.Samples = Mathf.Max(2, LineSamples);
            }
            Seam.Build();
        }

        void BuildMaterials()
        {
            if (_steelMat != null) return;
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Diffuse");
            if (sh == null) return;

            _steelMat = new Material(sh); _steelMat.name = "V5_Steel"; _steelMat.color = SteelColor;
            _edgeMat = new Material(sh); _edgeMat.name = "V5_DarkEdge"; _edgeMat.color = EdgeColor;
            _supportMat = new Material(sh); _supportMat.name = "V5_Support"; _supportMat.color = SupportColor;

            if (_steelMat.HasProperty("_Metallic")) _steelMat.SetFloat("_Metallic", 0.70f);
            if (_steelMat.HasProperty("_Glossiness")) _steelMat.SetFloat("_Glossiness", 0.30f);
            if (_supportMat.HasProperty("_Metallic")) _supportMat.SetFloat("_Metallic", 0.55f);
            if (_supportMat.HasProperty("_Glossiness")) _supportMat.SetFloat("_Glossiness", 0.20f);
        }

        GameObject MakeBox(string name, Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = _workpieceLayer;
            go.transform.SetParent(_root, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) SafeDestroy(c);
            }
            return go;
        }

        GameObject MakePipeSection(string name, Vector3 pos, float radius, float length)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = _workpieceLayer;
            go.transform.SetParent(_root, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            Renderer rr = go.GetComponent<Renderer>();
            if (rr != null && _steelMat != null) rr.sharedMaterial = _steelMat;
            // Cylinder trae CapsuleCollider: su silueta no coincide con los extremos.
            Collider old = go.GetComponent<Collider>();
            if (old != null) { old.enabled = false; SafeDestroy(old); }
            MeshCollider surface = go.AddComponent<MeshCollider>();
            surface.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            surface.convex = false;
            return go;
        }

        void MakePipeCut(string name, Vector3 pos, float radius)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = _workpieceLayer;
            go.transform.SetParent(_root, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            go.transform.localScale = new Vector3(radius * 2f, 0.0005f, radius * 2f);
            Renderer rr = go.GetComponent<Renderer>();
            if (rr != null && _edgeMat != null) rr.sharedMaterial = _edgeMat;
            Collider c = go.GetComponent<Collider>();
            if (c != null) SafeDestroy(c);
        }

        void MakePipeSupport(string name, Vector3 pipeCenter, float radius)
        {
            Vector3 pos = new Vector3(pipeCenter.x, pipeCenter.y - radius - 0.04f, pipeCenter.z);
            MakeBox(name, pos, new Vector3(0.07f, 0.08f, 0.15f), _supportMat, false);
        }

        public void ClearGenerated()
        {
            if (_root == null) return;
            _root.gameObject.SetActive(false);
            SafeDestroy(_root.gameObject);
            _root = null;
            _start = _end = _circleCenter = null;
        }

        void OnDestroy()
        {
            if (_steelMat != null) SafeDestroy(_steelMat);
            if (_edgeMat != null) SafeDestroy(_edgeMat);
            if (_supportMat != null) SafeDestroy(_supportMat);
        }

        static void SafeDestroy(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
