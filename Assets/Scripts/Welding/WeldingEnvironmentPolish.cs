using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

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
        [Header("Presentacion")]
        public bool PolishWorkshopMaterials = true;
        public bool AddFloorMarkings = true;
        public bool IndustrialDetails = true;
        [Tooltip("Nave con linea robotizada, cinta, AGV y puente grua alrededor de la celda.")]
        public bool FactoryLine = true;
        readonly List<Material> _presentationMaterials = new List<Material>();
        readonly List<Texture2D> _surfaceTextures = new List<Texture2D>();
        Transform _industrialRoot;
        Transform _workerHead;
        Texture2D _concrete, _brushedSteel;

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
        bool _guidanceVisible = true;

        public void SetGuidanceVisible(bool visible) => _guidanceVisible = visible;

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
            if (PolishWorkshopMaterials) BuildSurfaceTextures();
            if (PolishWorkshopMaterials) ApplyWorkshopMaterials();
            if (AddFloorMarkings) BuildFloorMarkings();
            if (IndustrialDetails) BuildIndustrialDetails();
            if (IndustrialDetails) BuildWorker();
            if (IndustrialDetails && FactoryLine) _industrialRoot.gameObject.AddComponent<FactoryLineScenery>();
        }

        void ApplyWorkshopMaterials()
        {
            foreach (Renderer renderer in FindObjectsByType<Renderer>())
            {
                string name = renderer.gameObject.name;
                Color color;
                float metallic = 0f, smoothness = 0.25f;
                if (name == "Piso") color = new Color(0.34f, 0.36f, 0.37f);
                else if (name.StartsWith("Pared")) color = new Color(0.48f, 0.51f, 0.52f);
                else if (name == "Biombo") color = new Color(0.31f, 0.13f, 0.09f);
                else if (name == "MaquinaSoldar") { color = new Color(0.10f, 0.27f, 0.33f); metallic = 0.4f; }
                else if (name.StartsWith("Pata")) { color = new Color(0.12f, 0.16f, 0.18f); metallic = 0.6f; }
                else if (name == "MesaTapa" || name == "MesaHerramientas" || name.StartsWith("Balda") || name.StartsWith("Montante"))
                { color = new Color(0.29f, 0.32f, 0.35f); metallic = 0.65f; smoothness = 0.32f; }
                else if (name.StartsWith("Cajon")) color = new Color(0.10f, 0.26f, 0.32f);
                else if (name.StartsWith("Tambor")) { color = new Color(0.17f, 0.24f, 0.27f); metallic = 0.35f; }
                else continue;
                Material[] source = renderer.sharedMaterials;
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null) continue;
                    Material material = new Material(source[i]);
                    material.name = "FrankaVR_Presentacion_" + name;
                    if (material.HasProperty("_Color")) material.color = color;
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
                    if (material.HasProperty("_MainTex"))
                    {
                        if (name == "Piso") { material.mainTexture = _concrete; material.mainTextureScale = Vector2.one * 8f; }
                        else if (metallic > 0.5f) { material.mainTexture = _brushedSteel; material.mainTextureScale = Vector2.one * 3f; }
                    }
                    source[i] = material;
                    _presentationMaterials.Add(material);
                }
                renderer.sharedMaterials = source;
            }
        }

        void BuildSurfaceTextures()
        {
            _concrete = SurfaceTexture("Concrete", false);
            _brushedSteel = SurfaceTexture("BrushedSteel", true);
        }

        Texture2D SurfaceTexture(string name, bool metal)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            texture.name = "FrankaVR_" + name;
            texture.wrapMode = TextureWrapMode.Repeat;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.47f + 13f, y * 0.47f + 7f);
                    float cloud = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);
                    float value = metal ? 0.82f + 0.10f * grain + 0.06f * Mathf.Sin(y * 2.7f) :
                        0.62f + 0.22f * cloud + 0.12f * grain;
                    if (!metal && (x < 1 || y < 1)) value *= 0.76f;
                    pixels[y * size + x] = new Color(value, value, value);
                }
            texture.SetPixels(pixels); texture.Apply(true, true);
            _surfaceTextures.Add(texture);
            return texture;
        }

        Material IndustrialMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.25f)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = "FrankaVR_" + name;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            _presentationMaterials.Add(material);
            return material;
        }

        GameObject Detail(string name, Vector3 position, Vector3 scale, Material material, PrimitiveType shape = PrimitiveType.Cube)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(_industrialRoot, true);
            go.transform.position = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        void Sign(string text, Vector3 position, Vector2 size, Material background, Color color)
        {
            Detail("IndustrialSign", position, new Vector3(size.x, size.y, 0.012f), background);
            GameObject label = new GameObject("IndustrialSignText");
            label.transform.SetParent(_industrialRoot, true);
            label.transform.position = position - Vector3.forward * 0.008f;
            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = text; mesh.fontSize = 72; mesh.characterSize = 0.035f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = color;
            FactoryLineScenery.UseDepthTestedText(mesh);
            Bounds bounds = label.GetComponent<Renderer>().localBounds;
            label.transform.localScale = Vector3.one * Mathf.Min(size.x * 0.88f / Mathf.Max(0.001f, bounds.size.x),
                size.y * 0.80f / Mathf.Max(0.001f, bounds.size.y));
        }

        void BuildIndustrialDetails()
        {
            if (_industrialRoot != null) return;
            GameObject root = new GameObject("IndustrialWorkshopDetails");
            root.transform.SetParent(transform, true); _industrialRoot = root.transform;
            Material dark = IndustrialMaterial("Structure", new Color(0.12f, 0.16f, 0.18f), 0.6f);
            Material grey = IndustrialMaterial("Galvanized", new Color(0.43f, 0.47f, 0.49f), 0.65f, 0.4f);
            Material yellow = IndustrialMaterial("SafetyYellow", new Color(0.95f, 0.65f, 0.12f));
            Material blue = IndustrialMaterial("MachineBlue", new Color(0.08f, 0.25f, 0.32f), 0.35f);
            Material white = IndustrialMaterial("Luminaire", new Color(0.85f, 0.93f, 1f));
            white.EnableKeyword("_EMISSION"); white.SetColor("_EmissionColor", new Color(1.2f, 1.3f, 1.4f));
            Renderer back = GameObject.Find("ParedFondo")?.GetComponent<Renderer>();
            if (back != null)
            {
                Bounds b = back.bounds;
                float front = b.min.z - 0.045f, roof = b.max.y;
                float left = Mathf.Max(b.min.x, -3.85f), right = Mathf.Min(b.max.x, 3.85f);
                // Chapa de nave y zocalo protector. Todo queda junto a las paredes.
                Detail("WallLowerBand", new Vector3(b.center.x, b.min.y + 0.48f, front), new Vector3(right-left, 0.95f, 0.025f), blue);
                Detail("WallSafetyBand", new Vector3(b.center.x, b.min.y + 0.98f, front - 0.02f), new Vector3(right-left, 0.035f, 0.01f), yellow);
                for (float x = left; x <= right; x += 0.65f)
                    Detail("WallRib", new Vector3(x, b.center.y + 0.42f, front), new Vector3(0.018f, b.size.y - 0.95f, 0.045f), grey);
                Detail("RoofDeck", new Vector3((left+right)*0.5f, roof+0.025f, b.center.z-4f), new Vector3(right-left+0.40f, 0.06f, 8.1f), dark);
                for (int i = 0; i < 4; i++)
                {
                    float z = b.center.z - 0.6f - i * 2f;
                    Detail("RoofBeam", new Vector3((left+right)*0.5f, roof-0.03f,z), new Vector3(right-left,0.16f,0.10f), grey);
                    Detail("LightHousing", new Vector3(0f,roof-0.17f,z), new Vector3(2.2f,0.08f,0.20f), dark);
                    Detail("LightDiffuser", new Vector3(0f,roof-0.215f,z), new Vector3(2.05f,0.015f,0.14f), white);
                }
                Sign("FRANKA VR  /  SOLDADURA\nCELDA 03", new Vector3(0f,2.35f,front-0.05f), new Vector2(2.4f,0.5f), dark, Color.white);
                Sign("ZONA DE TRABAJO\nPROTECCION OCULAR", new Vector3(-2.65f,1.55f,front-0.05f), new Vector2(1.15f,0.4f), yellow, new Color(0.1f,0.12f,0.13f));
                Detail("CableTray", new Vector3(0f,roof-0.35f,front-0.12f), new Vector3(right-left,0.10f,0.14f), dark);
            }
            Renderer table = GameObject.Find("MesaTapa")?.GetComponent<Renderer>();
            if (table != null)
            {
                Bounds b = table.bounds;
                Detail("BenchApron", new Vector3(b.center.x,b.min.y-0.065f,b.min.z+0.01f), new Vector3(b.size.x,0.12f,0.025f), dark);
                for (int i = 0; i < 9; i++)
                {
                    GameObject stripe = Detail("BenchHazardStripe", new Vector3(b.min.x+0.05f+i*(b.size.x-0.1f)/8f,b.min.y-0.065f,b.min.z-0.006f), new Vector3(0.025f,0.10f,0.005f), yellow);
                    stripe.transform.rotation = Quaternion.Euler(0f,0f,-25f);
                }
                // Tornillos laterales fuera de la superficie de soldadura.
                for (int i = 0; i < 4; i++)
                    Detail("BenchBolt", new Vector3(b.min.x+0.08f+i*(b.size.x-0.16f)/3f,b.center.y,b.min.z-0.006f), Vector3.one*0.012f, grey, PrimitiveType.Sphere);
            }
            foreach (string name in new[] { "ParedIzq", "ParedDer" })
            {
                Renderer wall = GameObject.Find(name)?.GetComponent<Renderer>();
                // Con la nave, la pared izquierda se reemplaza por una valla.
                if (wall == null || (FactoryLine && name == "ParedIzq")) continue;
                Bounds b = wall.bounds;
                float x = name == "ParedIzq" ? b.max.x + 0.015f : b.min.x - 0.015f;
                Detail("SideWallBand", new Vector3(x,b.min.y+0.48f,b.center.z), new Vector3(0.025f,0.95f,b.size.z), blue);
                Detail("SideWallSafetyBand", new Vector3(x,b.min.y+0.98f,b.center.z), new Vector3(0.03f,0.035f,b.size.z), yellow);
            }
            Renderer machine = GameObject.Find("MaquinaSoldar")?.GetComponent<Renderer>();
            if (machine != null)
            {
                Bounds b = machine.bounds;
                for (int i = 0; i < 7; i++)
                    Detail("MachineVent", new Vector3(b.center.x,b.min.y+0.12f+i*0.025f,b.min.z-0.009f), new Vector3(b.size.x*0.6f,0.009f,0.01f), dark);
                Detail("MachineHandle", new Vector3(b.center.x,b.max.y+0.045f,b.center.z), new Vector3(b.size.x*0.55f,0.025f,0.03f), dark);
                for (int i = -1; i <= 1; i += 2)
                    Detail("MachineHandleMount", new Vector3(b.center.x+i*b.size.x*0.24f,b.max.y+0.02f,b.center.z), new Vector3(0.018f,0.04f,0.03f), dark);
                Sign("SMAW / DC", new Vector3(b.center.x,b.max.y-0.10f,b.min.z-0.015f), new Vector2(b.size.x*0.65f,0.075f), dark, Color.white);
            }
            foreach (Renderer shelf in FindObjectsByType<Renderer>())
            {
                if (!shelf.name.StartsWith("Balda")) continue;
                Bounds b = shelf.bounds;
                for (int i = 0; i < 3; i++)
                    Detail("StockSteel", new Vector3(b.center.x,b.max.y+0.014f+i*0.028f,b.center.z+0.035f*i), new Vector3(b.size.x*0.70f,0.022f,0.055f), grey);
            }
            Renderer screen = GameObject.Find("Biombo")?.GetComponent<Renderer>();
            if (screen != null)
            {
                Bounds b = screen.bounds;
                for (int i = 0; i <= 12; i++)
                    Detail("CurtainFold", new Vector3(b.min.x+i*b.size.x/12f,b.center.y,b.min.z-0.014f), new Vector3(0.015f,b.size.y,0.028f),
                        _presentationMaterials.Find(m => m.name == "FrankaVR_Presentacion_Biombo") ?? dark);
                Detail("CurtainRail", new Vector3(b.center.x,b.max.y+0.025f,b.center.z), new Vector3(b.size.x+0.08f,0.035f,0.05f), grey);
            }
        }

        GameObject WorkerPart(Transform root, string name, Vector3 localPosition, Vector3 scale, Material material,
            PrimitiveType shape = PrimitiveType.Cube)
        {
            GameObject go = Detail(name, Vector3.zero, scale, material, shape);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        void WorkerLimb(Transform root, string name, Vector3 a, Vector3 b, float radius, Material material)
        {
            GameObject go = WorkerPart(root, name, (a+b)*0.5f,
                new Vector3(radius*2f, Vector3.Distance(a,b)*0.5f+radius, radius*2f), material, PrimitiveType.Capsule);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b-a);
        }

        void BuildWorker()
        {
            // Operario decorativo, separado de la celda y sin colisiones ni input.
            Material cloth = IndustrialMaterial("WorkerUniform", new Color(0.08f,0.15f,0.21f));
            Material vest = IndustrialMaterial("WorkerVest", new Color(0.90f,0.68f,0.08f));
            Material reflective = IndustrialMaterial("WorkerReflective", new Color(0.80f,0.84f,0.86f),0.2f);
            Material skin = IndustrialMaterial("WorkerSkin", new Color(0.62f,0.39f,0.26f));
            Material rubber = IndustrialMaterial("WorkerBoots", new Color(0.055f,0.065f,0.075f));
            Material helmet = IndustrialMaterial("WorkerHelmet", new Color(0.95f,0.78f,0.20f),0.05f,0.4f);
            Material visor = IndustrialMaterial("WorkerVisor", new Color(0.07f,0.17f,0.20f),0.5f,0.65f);
            Transform worker = new GameObject("WorkshopOperator").transform;
            worker.SetParent(_industrialRoot, false);
            Renderer floor = GameObject.Find("Piso")?.GetComponent<Renderer>();
            float ground = floor != null ? floor.bounds.max.y : 0f;
            worker.position = new Vector3(2.25f,ground,2.65f);
            worker.rotation = Quaternion.Euler(0f,225f,0f);
            WorkerPart(worker,"OperatorBootL",new Vector3(-0.12f,0.07f,0.045f),new Vector3(0.14f,0.14f,0.27f),rubber);
            WorkerPart(worker,"OperatorBootR",new Vector3(0.12f,0.07f,0.045f),new Vector3(0.14f,0.14f,0.27f),rubber);
            WorkerLimb(worker,"OperatorLegL",new Vector3(-0.12f,0.18f,0f),new Vector3(-0.11f,0.85f,0f),0.075f,cloth);
            WorkerLimb(worker,"OperatorLegR",new Vector3(0.12f,0.18f,0f),new Vector3(0.11f,0.85f,0f),0.075f,cloth);
            WorkerPart(worker,"OperatorTorso",new Vector3(0f,1.11f,0f),new Vector3(0.43f,0.57f,0.25f),cloth,PrimitiveType.Capsule);
            WorkerPart(worker,"OperatorVest",new Vector3(0f,1.14f,0.014f),new Vector3(0.38f,0.40f,0.25f),vest);
            WorkerPart(worker,"OperatorBelt",new Vector3(0f,0.92f,0f),new Vector3(0.38f,0.035f,0.25f),rubber);
            WorkerPart(worker,"OperatorVestBand",new Vector3(0f,1.04f,0.148f),new Vector3(0.36f,0.034f,0.008f),reflective);
            foreach (float x in new[] { -0.12f,0.12f })
                WorkerPart(worker,"OperatorVestReflector",new Vector3(x,1.21f,0.148f),new Vector3(0.028f,0.25f,0.008f),reflective);
            WorkerLimb(worker,"OperatorArmL",new Vector3(-0.25f,1.32f,0f),new Vector3(-0.32f,1.04f,0.08f),0.063f,cloth);
            WorkerLimb(worker,"OperatorForearmL",new Vector3(-0.32f,1.04f,0.08f),new Vector3(-0.13f,0.99f,0.27f),0.05f,cloth);
            WorkerLimb(worker,"OperatorArmR",new Vector3(0.25f,1.32f,0f),new Vector3(0.29f,1.06f,0.05f),0.063f,cloth);
            WorkerLimb(worker,"OperatorForearmR",new Vector3(0.29f,1.06f,0.05f),new Vector3(0.08f,1.03f,0.28f),0.05f,cloth);
            WorkerPart(worker,"OperatorClipboard",new Vector3(0f,1.01f,0.31f),new Vector3(0.28f,0.34f,0.025f),rubber).transform.localRotation=Quaternion.Euler(25f,0f,0f);
            WorkerPart(worker,"OperatorGloveL",new Vector3(-0.13f,0.99f,0.27f),Vector3.one*0.095f,skin,PrimitiveType.Sphere);
            WorkerPart(worker,"OperatorGloveR",new Vector3(0.08f,1.03f,0.28f),Vector3.one*0.095f,skin,PrimitiveType.Sphere);
            WorkerPart(worker,"OperatorNeck",new Vector3(0f,1.44f,0f),new Vector3(0.095f,0.07f,0.095f),skin,PrimitiveType.Cylinder);
            _workerHead = new GameObject("OperatorHeadRoot").transform;
            _workerHead.SetParent(worker,false); _workerHead.localPosition=new Vector3(0f,1.57f,0f);
            WorkerPart(_workerHead,"OperatorHead",Vector3.zero,new Vector3(0.20f,0.25f,0.19f),skin,PrimitiveType.Sphere);
            WorkerPart(_workerHead,"OperatorNose",new Vector3(0f,-0.012f,0.092f),new Vector3(0.035f,0.05f,0.038f),skin,PrimitiveType.Sphere);
            WorkerPart(_workerHead,"OperatorHelmet",new Vector3(0f,0.10f,-0.012f),new Vector3(0.24f,0.16f,0.23f),helmet,PrimitiveType.Sphere);
            WorkerPart(_workerHead,"OperatorHelmetBrim",new Vector3(0f,0.053f,0.02f),new Vector3(0.27f,0.015f,0.28f),helmet);
            WorkerPart(_workerHead,"OperatorSafetyGlasses",new Vector3(0f,0.015f,0.093f),new Vector3(0.175f,0.045f,0.023f),visor);
            foreach (float x in new[] { -0.105f,0.105f })
                WorkerPart(_workerHead,"OperatorEarProtection",new Vector3(x,0.01f,0f),new Vector3(0.04f,0.095f,0.06f),rubber,PrimitiveType.Sphere);
        }

        void BuildFloorMarkings()
        {
            GameObject floor = GameObject.Find("Piso");
            Renderer renderer = floor != null ? floor.GetComponent<Renderer>() : null;
            if (renderer == null || Workpiece == null) return;
            Vector3 c = Workpiece.position;
            float y = renderer.bounds.max.y + 0.003f;
            LineRenderer border = CreateLine("WorkshopFloorMarkings", 4, 0.025f, new Color(0.95f, 0.66f, 0.13f), true);
            border.SetPositions(new[] {
                new Vector3(c.x - 0.70f,y,c.z - 0.65f), new Vector3(c.x + 0.70f,y,c.z - 0.65f),
                new Vector3(c.x + 0.70f,y,c.z + 0.65f), new Vector3(c.x - 0.70f,y,c.z + 0.65f) });
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
            if (_workerHead != null) _workerHead.localRotation = Quaternion.Euler(2f*Mathf.Sin(Time.time*0.55f),6f*Mathf.Sin(Time.time*0.30f),0f);
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
                    _seamPath.loop = Seam.Shape == SeamShape.Circle;
                    _seamPath.enabled = _guidanceVisible && (!Torch || !Torch.Arcing);
                }
            }

            // Si la costura se construye/cambia luego del Awake, crear o recolocar marcadores.
            if (ShowSeamEndpoints && Seam != null && Seam.Count >= 2)
            {
                if (_startMarker == null || _endMarker == null) BuildSeamMarkers();
                if (_startMarker != null) _startMarker.transform.position = Seam.PointAt(0) + Vector3.up * 0.004f;
                if (_endMarker != null) _endMarker.transform.position = Seam.PointAt(Seam.Count - 1) + Vector3.up * 0.004f;
                if (_startMarker != null) _startMarker.SetActive(_guidanceVisible);
                if (_endMarker != null) _endMarker.SetActive(_guidanceVisible && Seam.Shape != SeamShape.Circle);
            }
            if (_outline != null) _outline.enabled = _guidanceVisible;
        }

        void UpdateGuide()
        {
            if (_guide == null || !_guidanceVisible || !ShowArcGuide || Torch == null)
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
            foreach (Material material in _presentationMaterials) if (material != null) Destroy(material);
            foreach (Texture2D texture in _surfaceTextures) if (texture != null) Destroy(texture);
            if (_industrialRoot != null) Destroy(_industrialRoot.gameObject);
        }
    }
}
