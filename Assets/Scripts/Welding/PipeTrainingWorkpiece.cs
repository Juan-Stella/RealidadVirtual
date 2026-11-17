using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Pieza de practica procedural y liviana para mobile: dos tramos de caño a tope,
    /// soportes y una junta circunferencial compatible con la orden N5 existente.
    /// </summary>
    public class PipeTrainingWorkpiece : MonoBehaviour
    {
        public WeldSeam Seam;
        public WeldingTaskManager Task;
        [Tooltip("Centro de la junta. Si queda vacio se usa la posicion de este objeto.")]
        public Transform JointCenter;

        [Header("Geometria")]
        public float PipeRadius = 0.06f;
        public float SectionLength = 0.22f;
        public float Gap = 0.006f;
        public float InnerRadiusRatio = 0.72f;
        public bool ActivateN5OnStart = true;
        public bool BuildOnStart = true;
        [Tooltip("Copia la layer de 'Chapa' a los dos caños generados. Util si WeldingTorch usa WorkpieceMask.")]
        public bool CopyLayerFromChapa = true;

        [Header("Aspecto")]
        public Color SteelColor = new Color(0.30f, 0.33f, 0.36f, 1f);
        public Color CutColor = new Color(0.055f, 0.06f, 0.065f, 1f);
        public Color SupportColor = new Color(0.14f, 0.17f, 0.20f, 1f);

        Transform _root;
        Transform _center;
        Material _steelMat, _cutMat, _supportMat;
        int _workpieceLayer;

        void Start()
        {
            if (BuildOnStart) Build();
        }

        [ContextMenu("Build Pipe Training Workpiece")]
        public void Build()
        {
            if (_root != null) { _root.gameObject.SetActive(false); SafeDestroy(_root.gameObject); }
            if (Seam == null) Seam = FindObjectOfType<WeldSeam>();
            if (Task == null) Task = FindObjectOfType<WeldingTaskManager>();

            _workpieceLayer = gameObject.layer;
            if (CopyLayerFromChapa)
            {
                GameObject chapa = GameObject.Find("Chapa");
                if (chapa != null) _workpieceLayer = chapa.layer;
            }

            if (_steelMat == null) BuildMaterials();

            GameObject rootGo = new GameObject("PipeButtTrainingWorkpiece");
            _root = rootGo.transform;
            _root.SetParent(transform, false);
            _root.localPosition = Vector3.zero;
            _root.localRotation = Quaternion.identity;

            Vector3 centerWorld = JointCenter != null ? JointCenter.position : transform.position;
            GameObject centerGo = new GameObject("PipeJointCenter");
            _center = centerGo.transform;
            _center.SetParent(_root, true);
            _center.position = centerWorld;
            _center.rotation = Quaternion.identity;

            float xOff = Gap * 0.5f + SectionLength * 0.5f;
            MakePipeSection("Pipe_Left", centerWorld + Vector3.left * xOff, SectionLength);
            MakePipeSection("Pipe_Right", centerWorld + Vector3.right * xOff, SectionLength);
            MakePipeSection("Pipe_JointRoot", centerWorld, Gap);

            MakeDarkCutFace("Cut_Left", centerWorld + Vector3.left * (Gap * 0.5f + 0.001f));
            MakeDarkCutFace("Cut_Right", centerWorld + Vector3.right * (Gap * 0.5f + 0.001f));

            MakeSupport("Support_Left", centerWorld + Vector3.left * (SectionLength * 0.55f));
            MakeSupport("Support_Right", centerWorld + Vector3.right * (SectionLength * 0.55f));

            if (Seam != null)
            {
                Seam.Shape = SeamShape.Circle;
                Seam.CircleCenter = _center;
                Seam.CircleAxis = Vector3.right;
                Seam.CircleRadius = PipeRadius;
                Seam.Samples = Mathf.Max(72, Seam.Samples);
                Seam.Build();
            }

            if (ActivateN5OnStart && Task != null)
                Task.LoadWorkOrder("N5");
        }

        void BuildMaterials()
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Diffuse");

            _steelMat = new Material(sh); _steelMat.color = SteelColor;
            _cutMat = new Material(sh); _cutMat.color = CutColor;
            _supportMat = new Material(sh); _supportMat.color = SupportColor;

            if (_steelMat.HasProperty("_Metallic")) _steelMat.SetFloat("_Metallic", 0.72f);
            if (_steelMat.HasProperty("_Glossiness")) _steelMat.SetFloat("_Glossiness", 0.32f);
        }

        void MakePipeSection(string name, Vector3 pos, float length)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = _workpieceLayer;
            go.transform.SetParent(_root, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // eje del cilindro -> X
            go.transform.localScale = new Vector3(PipeRadius * 2f, length * 0.5f, PipeRadius * 2f);
            go.GetComponent<Renderer>().material = _steelMat;

            Collider old = go.GetComponent<Collider>();
            if (old != null) { old.enabled = false; SafeDestroy(old); }
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = false;
        }

        void MakeDarkCutFace(string name, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = _workpieceLayer;
            go.transform.SetParent(_root, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            float r = PipeRadius * InnerRadiusRatio;
            go.transform.localScale = new Vector3(r * 2f, 0.001f, r * 2f);
            go.GetComponent<Renderer>().material = _cutMat;
            Collider c = go.GetComponent<Collider>();
            if (c != null) SafeDestroy(c);
        }

        void MakeSupport(string name, Vector3 pipePos)
        {
            float topY = pipePos.y - PipeRadius - 0.015f;
            GameObject baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = name;
            baseGo.transform.SetParent(_root, true);
            baseGo.transform.position = new Vector3(pipePos.x, topY - 0.055f, pipePos.z);
            baseGo.transform.localScale = new Vector3(0.08f, 0.11f, 0.16f);
            baseGo.GetComponent<Renderer>().material = _supportMat;
        }

        void OnDestroy()
        {
            if (_steelMat != null) SafeDestroy(_steelMat);
            if (_cutMat != null) SafeDestroy(_cutMat);
            if (_supportMat != null) SafeDestroy(_supportMat);
        }

        static void SafeDestroy(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
