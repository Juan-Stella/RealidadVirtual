using UnityEngine;
using UnityEngine.Rendering;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Pulido visual no invasivo de la celda de soldadura.
    /// No toca IK, fisica ni logica de tarea: solo iluminacion y ayudas visuales.
    /// Se puede agregar a un Empty llamado, por ejemplo, "VisualPolish".
    /// </summary>
    public class WeldingEnvironmentPolish : MonoBehaviour
    {
        [Header("Referencias opcionales")]
        public WeldingTorch Torch;
        public WeldSeam Seam;
        public Transform Workpiece;
        public Camera ReferenceCamera;

        [Header("Iluminacion de taller")]
        public bool ImproveAmbientLighting = true;
        public Color AmbientColor = new Color(0.34f, 0.37f, 0.42f, 1f);
        [Range(0f, 2f)] public float AmbientIntensity = 0.75f;
        public bool AddFillLight = true;
        public Color FillColor = new Color(0.72f, 0.82f, 1f, 1f);
        public float FillIntensity = 0.65f;
        public float FillRange = 2.6f;

        [Header("Guia de arco")]
        public bool ShowArcGuide = true;
        [Tooltip("Ocultar la guia una vez que el arco esta estable da una apariencia mas realista.")]
        public bool HideGuideWhileArcing = true;
        public float GuideMaxDistanceMm = 180f;
        public float GuideWidth = 0.0018f;
        public Color GuideGood = new Color(0.25f, 1f, 0.35f, 0.9f);
        public Color GuideNear = new Color(1f, 0.72f, 0.12f, 0.9f);
        public Color GuideFar = new Color(1f, 0.20f, 0.12f, 0.9f);

        [Header("Zona de trabajo")]
        public bool OutlineWorkpiece = true;
        public Color WorkpieceOutlineColor = new Color(0.18f, 0.65f, 1f, 0.55f);
        public float OutlineWidth = 0.002f;
        public bool ShowSeamEndpoints = true;
        public float EndpointSize = 0.012f;
        [Tooltip("Muestra una guia fina sobre la trayectoria a soldar.")]
        public bool ShowSeamPath = true;
        public float SeamPathWidth = 0.0025f;
        public Color SeamPathColor = new Color(0.10f, 0.75f, 1f, 0.65f);

        Material _lineMat;
        LineRenderer _guide;
        LineRenderer _outline;
        LineRenderer _seamPath;
        GameObject _startMarker, _endMarker;
        GameObject _fillLightGo;
        Light _fillLight;

        AmbientMode _oldAmbientMode;
        Color _oldAmbientLight;
        float _oldAmbientIntensity;

        void Awake()
        {
            AutoFindReferences();
            BuildMaterials();
            ApplyAmbient();
            BuildGuide();
            BuildWorkpieceOutline();
            BuildSeamMarkers();
            BuildSeamPath();
            BuildFillLight();
        }

        void AutoFindReferences()
        {
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            if (Seam == null) Seam = FindObjectOfType<WeldSeam>();
            if (ReferenceCamera == null) ReferenceCamera = Camera.main;
            if (Workpiece == null)
            {
                GameObject chapa = GameObject.Find("Chapa");
                if (chapa != null) Workpiece = chapa.transform;
            }
        }

        void BuildMaterials()
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh != null) _lineMat = new Material(sh);
        }

        void ApplyAmbient()
        {
            if (!ImproveAmbientLighting) return;
            _oldAmbientMode = RenderSettings.ambientMode;
            _oldAmbientLight = RenderSettings.ambientLight;
            _oldAmbientIntensity = RenderSettings.ambientIntensity;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;
            RenderSettings.ambientIntensity = AmbientIntensity;
        }

        LineRenderer CreateLine(string name, int positions, float width, Color color, bool loop = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = positions;
            lr.loop = loop;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            if (_lineMat != null) lr.material = _lineMat;
            lr.startColor = color;
            lr.endColor = color;
            return lr;
        }

        void BuildGuide()
        {
            _guide = CreateLine("ArcDistanceGuide", 2, GuideWidth, GuideFar, false);
            _guide.enabled = false;
        }

        void BuildWorkpieceOutline()
        {
            if (!OutlineWorkpiece || Workpiece == null) return;
            Renderer r = Workpiece.GetComponentInChildren<Renderer>();
            if (r == null) return;

            Bounds b = r.bounds;
            float y = b.max.y + 0.0015f;
            _outline = CreateLine("WorkpieceOutline", 4, OutlineWidth, WorkpieceOutlineColor, true);
            _outline.SetPosition(0, new Vector3(b.min.x, y, b.min.z));
            _outline.SetPosition(1, new Vector3(b.max.x, y, b.min.z));
            _outline.SetPosition(2, new Vector3(b.max.x, y, b.max.z));
            _outline.SetPosition(3, new Vector3(b.min.x, y, b.max.z));
        }

        Material MakeMarkerMaterial(Color color)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            Material m = new Material(sh);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            else m.color = color;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.5f);
            }
            return m;
        }

        GameObject MakeMarker(string name, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.localScale = Vector3.one * EndpointSize;
            Renderer r = go.GetComponent<Renderer>();
            Material m = MakeMarkerMaterial(color);
            if (r != null && m != null) r.material = m;
            return go;
        }

        void BuildSeamMarkers()
        {
            if (!ShowSeamEndpoints || Seam == null || Seam.Count < 2) return;
            _startMarker = MakeMarker("SeamStart", new Color(0.20f, 1f, 0.35f, 1f));
            _endMarker = MakeMarker("SeamEnd", new Color(0.15f, 0.55f, 1f, 1f));
            _startMarker.transform.position = Seam.PointAt(0) + Vector3.up * 0.004f;
            _endMarker.transform.position = Seam.PointAt(Seam.Count - 1) + Vector3.up * 0.004f;
        }


        void BuildSeamPath()
        {
            if (!ShowSeamPath || Seam == null || Seam.Count < 2) return;
            _seamPath = CreateLine("SeamPathGuide", Seam.Count, SeamPathWidth, SeamPathColor, false);
            for (int i = 0; i < Seam.Count; i++)
                _seamPath.SetPosition(i, Seam.PointAt(i) + Seam.NormalAt(i).normalized * 0.0015f);
        }

        void BuildFillLight()
        {
            if (!AddFillLight) return;

            Vector3 center = Workpiece != null ? Workpiece.position : transform.position;
            Renderer r = Workpiece != null ? Workpiece.GetComponentInChildren<Renderer>() : null;
            if (r != null) center = r.bounds.center;

            _fillLightGo = new GameObject("WorkshopFillLight");
            _fillLightGo.transform.SetParent(transform, false);
            _fillLightGo.transform.position = center + new Vector3(-0.55f, 0.85f, -0.35f);
            _fillLight = _fillLightGo.AddComponent<Light>();
            _fillLight.type = LightType.Point;
            _fillLight.color = FillColor;
            _fillLight.intensity = FillIntensity;
            _fillLight.range = FillRange;
            _fillLight.shadows = LightShadows.None;
        }

        void LateUpdate()
        {
            UpdateGuide();

            // Algunas piezas (por ejemplo PipeTrainingWorkpiece) generan su mesh en Start.
            // Reintentar evita depender del orden de Awake/Start entre componentes.
            if (OutlineWorkpiece && _outline == null && Workpiece != null &&
                Workpiece.GetComponentInChildren<Renderer>() != null)
                BuildWorkpieceOutline();

            if (ShowSeamPath && Seam != null && Seam.Count >= 2)
            {
                if (_seamPath == null || _seamPath.positionCount != Seam.Count)
                {
                    if (_seamPath != null) Destroy(_seamPath.gameObject);
                    _seamPath = null;
                    BuildSeamPath();
                }

                if (_seamPath != null)
                {
                    for (int i = 0; i < Seam.Count; i++)
                        _seamPath.SetPosition(i, Seam.PointAt(i) + Seam.NormalAt(i).normalized * 0.0015f);
                    _seamPath.enabled = !Torch || !Torch.Arcing;
                }
            }

            // Si la costura se construye/cambia luego del Awake, crear o recolocar marcadores.
            if (ShowSeamEndpoints && Seam != null && Seam.Count >= 2)
            {
                if (_startMarker == null || _endMarker == null) BuildSeamMarkers();
                if (_startMarker != null) _startMarker.transform.position = Seam.PointAt(0) + Vector3.up * 0.004f;
                if (_endMarker != null) _endMarker.transform.position = Seam.PointAt(Seam.Count - 1) + Vector3.up * 0.004f;
            }
        }

        void UpdateGuide()
        {
            if (_guide == null || !ShowArcGuide || Torch == null)
            {
                if (_guide != null) _guide.enabled = false;
                return;
            }

            if (HideGuideWhileArcing && Torch.Arcing)
            {
                _guide.enabled = false;
                return;
            }

            if (!Torch.HasArcSurface || Torch.ArcLenMm > GuideMaxDistanceMm)
            {
                _guide.enabled = false;
                return;
            }

            Vector3 a = Torch.ArcOriginPosition;
            Vector3 b = a + Torch.PointingDir * Torch.ArcLenMm * 0.001f;
            _guide.SetPosition(0, a);
            _guide.SetPosition(1, b);

            float ideal = (Torch.Machine != null && Torch.Machine.Current != null)
                ? Torch.Machine.Current.ArcLenIdealMm : 3.25f;
            Color c;
            if (Torch.ArcLenMm <= Mathf.Max(ideal * 2.2f, 8f)) c = GuideGood;
            else if (Torch.ArcLenMm < Torch.MaxArcLenMm) c = GuideNear;
            else c = GuideFar;

            _guide.startColor = c;
            _guide.endColor = c;
            _guide.enabled = true;
        }

        void OnDestroy()
        {
            if (ImproveAmbientLighting)
            {
                RenderSettings.ambientMode = _oldAmbientMode;
                RenderSettings.ambientLight = _oldAmbientLight;
                RenderSettings.ambientIntensity = _oldAmbientIntensity;
            }

            if (_lineMat != null) Destroy(_lineMat);
            if (_startMarker != null) Destroy(_startMarker);
            if (_endMarker != null) Destroy(_endMarker);
            if (_fillLightGo != null) Destroy(_fillLightGo);
        }
    }
}
