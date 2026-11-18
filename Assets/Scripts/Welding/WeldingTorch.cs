using UnityEngine;
using UnityEngine.Serialization;

namespace FrankaVR.Welding
{
    public enum ArcState
    {
        Off,      // sin pedido de arco
        Striking, // intentando cebar
        Stable,   // arco encendido
        Stuck,    // electrode pegado a la pieza
        Blocked   // maquina en proteccion termica o electrodo consumido
    }

    /// Efector final soldador. Se monta bajo la brida del Panda.
    /// 'Tip' es la punta fisica del electrodo desde donde se mide el arco.
    /// Puede ser un hijo desplazado del frame que usa PandaIK como endEffector.
    ///
    /// Modela el comportamiento real del electrodo revestido:
    ///  - hay que CEBAR el arco (no enciende instantaneo)
    ///  - si se apoya con arco nulo, el electrodo SE PEGA y hay que despegarlo
    ///  - el electrodo SE CONSUME: la varilla se acorta y hay que reponerla
    public class WeldingTorch : MonoBehaviour
    {
        [Header("Referencias")]
        public Transform      Tip;                 // frame logico/TCP de la herramienta
        [Tooltip("Referencia manual OPCIONAL de la punta fisica.")]
        public Transform      ArcTip;
        [Tooltip("Si esta OFF (recomendado), ArcTip se ignora y la punta fisica se calcula SIEMPRE con RemainingMm. Esto garantiza que el consumo visual siga al consumo logico.")]
        public bool           UseArcTipOverride = false;
        public WeldingMachine Machine;
        public Transform      ElectrodeVisual;     // cilindro que se acorta al consumirse
        [Tooltip("Punto cercano al mango del electrodo. Si se asigna, la direccion del rayo " +
                 "se deduce geometricamente como ElectrodeBase -> Tip, evitando depender de ejes locales.")]
        public Transform      ElectrodeBase;
        [Tooltip("Raiz SOLO del robot para ignorar sus colliders. Si queda vacio se intenta " +
                 "deducir a partir de la jerarquia de ArticulationBody. Nunca se usa transform.root, " +
                 "porque podria incluir tambien la pieza de trabajo.")]
        public Transform      IgnoreRoot;
        [Tooltip("Filtrar por capa como PREFERENCIA. Si la mascara no encuentra la pieza, el codigo hace " +
                 "fallback automatico a la primera superficie que no pertenezca al robot.")]
        public bool           UseLayerFilter = false;
        public LayerMask      WorkpieceMask = ~0;

        [Header("Geometria")]
        [Tooltip("Eje local usado solo como fallback si ElectrodeBase no esta asignado.")]
        public Vector3 ElectrodeAxisLocal = Vector3.down;

        [Tooltip("Fallback visual usado solo si falta Tip. Normalmente no interviene en la medicion.")]
        public bool UseElectrodeVisualTip = true;

        [Tooltip("Distancia maxima que el sensor virtual intenta MEDIR. No es el limite del arco.")]
        public float MeasureRangeMm = 2000f;

        [Tooltip("Distancia maxima a la que se permite cebar o mantener el arco.")]
        [FormerlySerializedAs("MaxArcRayMm")]
        public float MaxArcLenMm = 40f;

        [Tooltip("Longitud minima util de la varilla: por debajo hay que cambiarla.")]
        public float StubLengthMm = 50f;

        [Header("Visual de varilla - procedural")]
        [Tooltip("Busca automaticamente la malla original de la varilla si ElectrodeVisual falta o parece apuntar a otro objeto.")]
        public bool AutoFindElectrodeVisual = true;
        [Tooltip("Genera una varilla visual nueva cuya longitud coincide exactamente con RemainingMm. Recomendado: ON.")]
        public bool UseProceduralElectrodeVisual = true;
        [Tooltip("Oculta el Renderer de ElectrodeVisual original para que no se vea la varilla vieja a longitud fija.")]
        public bool HideOriginalElectrodeVisual = true;
        [Tooltip("Desactiva colliders del ElectrodeVisual original. Evita que una varilla invisible pero larga siga chocando con la chapa.")]
        public bool DisableOriginalElectrodeColliders = true;
        [Tooltip("Diametro visual si no hay electrodo cargado en la maquina.")]
        public float FallbackDiameterMm = 3.25f;
        [Tooltip("Multiplicador SOLO visual del diametro para que la varilla sea legible en VR/mobile.")]
        [Range(0.8f, 2.0f)] public float VisualDiameterScale = 1.20f;
        public Color ElectrodeColdColor = new Color(0.46f, 0.47f, 0.49f, 1f);
        public Color ElectrodeHotTipColor = new Color(1.0f, 0.45f, 0.10f, 1f);
        [Tooltip("Tamano del punto incandescente en la punta mientras hay arco.")]
        public float HotTipScale = 0.008f;

        [Header("Cebado / pegado")]
        public float StrikeTimeSec  = 0.25f; // demora en establecer el arco
        public float StickArcLenMm  = 0.4f;  // por debajo de esto se pega
        public float UnstickPullMm  = 4f;    // separacion necesaria para despegar

        [Header("Diagnostico")]
        [Tooltip("Dibuja el rayo de medicion en la vista Scene durante el Play. " +
                 "VERDE = pego en la pieza. ROJO = no pego en nada. " +
                 "Es la forma directa de ver si apunta donde corresponde.")]
        public bool DebugRay = true;
        [Tooltip("Largo del rayo de diagnostico, solo para verlo. No cambia la medicion.")]
        public float DebugRayLengthM = 0.30f;
        [Tooltip("Imprime en Consola el resultado del raycast, con y SIN filtro de capa. " +
                 "Comparar ambos dice si el problema es la mascara o la geometria.")]
        public bool LogRaycast = false;

        [Header("Estado (solo lectura)")]
        public ArcState State = ArcState.Off;
        public float    ArcLenMm;
        public float    RemainingMm = 350f;
        public float    ArcQuality;    // 0..1 estabilidad instantanea del arco
        public bool     SparkRequested;
        public bool     HasArcSurface;  // true si el raycast encontro una superficie valida

        float _strikeTimer;
        float _initialLenMm = 350f;
        Vector3 _electrodeBaseScale = Vector3.one;
        bool _scaleCaptured;

        GameObject _runtimeRod;
        LineRenderer _runtimeRodLine;
        Material _runtimeRodMaterial;
        GameObject _runtimeHotTip;
        Renderer _runtimeHotTipRenderer;
        Material _runtimeHotTipMaterial;
        Renderer[] _originalRodRenderers;
        bool[] _originalRendererStates;
        Collider[] _originalRodColliders;
        bool[] _originalColliderStates;

        public bool Arcing => State == ArcState.Stable;
        public bool Stuck  => State == ArcState.Stuck;
        public float RemainingFrac => _initialLenMm > 0f ? Mathf.Clamp01(RemainingMm / _initialLenMm) : 0f;

        readonly RaycastHit[] _hitBuf = new RaycastHit[12];

        void Awake()
        {
            if (AutoFindElectrodeVisual) AutoResolveElectrodeVisual();
            CaptureElectrodeScale();
            SetupElectrodeVisuals();

            // IMPORTANTE: no usar transform.root. En muchas escenas la raiz es la celda completa
            // y tambien contiene la chapa; eso haria que el raycast descarte justamente la pieza.
            if (IgnoreRoot == null) IgnoreRoot = FindRobotArticulationRoot();

            if (IgnoreRoot == null)
                Debug.LogWarning("[WeldingTorch] IgnoreRoot no asignado y no pude deducir la raiz del robot. " +
                                 "Asigna explicitamente el GameObject raiz del Panda en el Inspector.");
        }

        void AutoResolveElectrodeVisual()
        {
            if (Tip == null) return;

            bool currentLooksLikeRod = ElectrodeVisual != null && LooksLikeRod(ElectrodeVisual);
            if (currentLooksLikeRod) return;

            Transform searchRoot = Tip.parent != null ? Tip.parent : transform;
            Renderer[] rs = searchRoot.GetComponentsInChildren<Renderer>(true);
            Renderer best = null;
            float bestScore = float.NegativeInfinity;
            Vector3 dir = PointingDir.sqrMagnitude > 1e-8f ? PointingDir.normalized : Vector3.down;

            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || r.transform == transform) continue;
                Bounds b = r.bounds;
                Vector3 sz = b.size;
                float max = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
                float min = Mathf.Max(0.0001f, Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)));
                float mid = sz.x + sz.y + sz.z - max - min;
                float aspect = max / Mathf.Max(0.0001f, mid);

                if (max < 0.08f || max > 0.60f) continue;
                if (aspect < 4.0f) continue;

                Vector3 rel = b.center - Tip.position;
                float proj = Vector3.Dot(rel, dir);
                Vector3 perp = rel - dir * proj;
                if (proj < -0.08f || proj > 0.55f) continue;
                if (perp.magnitude > 0.10f) continue;

                float score = aspect * 2f
                            - Mathf.Abs(max - 0.35f) * 18f
                            - perp.magnitude * 30f
                            - Mathf.Abs(proj - 0.17f) * 7f;
                string n = r.name.ToLowerInvariant();
                if (n.Contains("elect") || n.Contains("rod") || n.Contains("varilla")) score += 10f;

                if (score > bestScore) { bestScore = score; best = r; }
            }

            if (best != null)
            {
                ElectrodeVisual = best.transform;
                Debug.Log("[WeldingTorch] ElectrodeVisual autodetectado: '" + best.name + "'. La varilla original se ocultara y se usara RuntimeElectrodeRod.");
            }
            else if (ElectrodeVisual == null)
            {
                Debug.LogWarning("[WeldingTorch] No pude autodetectar la malla original de la varilla. RuntimeElectrodeRod funcionara igual, pero la varilla vieja puede seguir visible si no asignas ElectrodeVisual.");
            }
        }

        bool LooksLikeRod(Transform tr)
        {
            if (tr == null) return false;
            Renderer r = tr.GetComponentInChildren<Renderer>(true);
            if (r == null) return false;
            Vector3 sz = r.bounds.size;
            float max = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            float min = Mathf.Max(0.0001f, Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)));
            float mid = sz.x + sz.y + sz.z - max - min;
            float aspect = max / Mathf.Max(0.0001f, mid);
            return max >= 0.08f && max <= 0.60f && aspect >= 4f;
        }

        Transform FindRobotArticulationRoot()
        {
            Transform candidate = null;
            Transform t = transform;
            while (t != null)
            {
                if (t.GetComponent<ArticulationBody>() != null) candidate = t;
                t = t.parent;
            }
            return candidate;
        }

        void Start()
        {
            LoadNewElectrode();
            UpdateProceduralElectrodeVisual();
        }

        void LateUpdate()
        {
            UpdateProceduralElectrodeVisual();
        }

        /// Guarda la escala original del cilindro del electrodo. Sin esto, al acortar la
        /// varilla se pisaba la escala con la fraccion (0..1) y el cilindro pasaba de
        /// 9 cm a 2 m (la escala 1 de un Cylinder de Unity son 2 metros de alto).
        void CaptureElectrodeScale()
        {
            if (_scaleCaptured || ElectrodeVisual == null) return;
            _electrodeBaseScale = ElectrodeVisual.localScale;
            _scaleCaptured = true;
        }

        void SetupElectrodeVisuals()
        {
            if (!UseProceduralElectrodeVisual) return;

            if (ElectrodeVisual != null)
            {
                _originalRodRenderers = ElectrodeVisual.GetComponentsInChildren<Renderer>(true);
                _originalRendererStates = new bool[_originalRodRenderers.Length];
                for (int i = 0; i < _originalRodRenderers.Length; i++)
                {
                    _originalRendererStates[i] = _originalRodRenderers[i] != null && _originalRodRenderers[i].enabled;
                    if (HideOriginalElectrodeVisual && _originalRodRenderers[i] != null)
                        _originalRodRenderers[i].enabled = false;
                }

                _originalRodColliders = ElectrodeVisual.GetComponentsInChildren<Collider>(true);
                _originalColliderStates = new bool[_originalRodColliders.Length];
                for (int i = 0; i < _originalRodColliders.Length; i++)
                {
                    _originalColliderStates[i] = _originalRodColliders[i] != null && _originalRodColliders[i].enabled;
                    if (DisableOriginalElectrodeColliders && _originalRodColliders[i] != null)
                        _originalRodColliders[i].enabled = false;
                }
            }

            BuildRuntimeRod();
            BuildRuntimeHotTip();
        }

        Material MakeRuntimeMaterial(Color color, Material template = null)
        {
            Material m = null;
            if (template != null) m = new Material(template);
            else
            {
                Shader sh = Shader.Find("Standard");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null) m = new Material(sh);
            }

            if (m != null)
            {
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                else m.color = color;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.55f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.45f);
            }
            return m;
        }

        void BuildRuntimeRod()
        {
            if (_runtimeRod != null) return;

            // LineRenderer: la varilla queda definida literalmente por dos puntos
            // (sujecion y punta fisica). No depende del pivot/eje/escala de una malla.
            _runtimeRod = new GameObject("RuntimeElectrodeRod");
            _runtimeRodLine = _runtimeRod.AddComponent<LineRenderer>();
            _runtimeRodLine.useWorldSpace = true;
            _runtimeRodLine.positionCount = 2;
            _runtimeRodLine.numCapVertices = 6;
            _runtimeRodLine.numCornerVertices = 2;
            _runtimeRodLine.textureMode = LineTextureMode.Stretch;
            _runtimeRodLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _runtimeRodLine.receiveShadows = false;

            Material template = null;
            if (ElectrodeVisual != null)
            {
                Renderer r = ElectrodeVisual.GetComponentInChildren<Renderer>(true);
                if (r != null) template = r.sharedMaterial;
            }
            _runtimeRodMaterial = MakeRuntimeMaterial(ElectrodeColdColor, template);
            if (_runtimeRodMaterial != null) _runtimeRodLine.material = _runtimeRodMaterial;
        }

        void BuildRuntimeHotTip()
        {
            if (_runtimeHotTip != null) return;

            _runtimeHotTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _runtimeHotTip.name = "RuntimeElectrodeHotTip";
            Collider c = _runtimeHotTip.GetComponent<Collider>();
            if (c != null) Destroy(c);

            _runtimeHotTipRenderer = _runtimeHotTip.GetComponent<Renderer>();
            _runtimeHotTipMaterial = MakeRuntimeMaterial(ElectrodeHotTipColor);
            if (_runtimeHotTipMaterial != null)
            {
                if (_runtimeHotTipMaterial.HasProperty("_EmissionColor"))
                {
                    _runtimeHotTipMaterial.EnableKeyword("_EMISSION");
                    _runtimeHotTipMaterial.SetColor("_EmissionColor", ElectrodeHotTipColor * 2.5f);
                }
            }
            if (_runtimeHotTipRenderer != null && _runtimeHotTipMaterial != null)
                _runtimeHotTipRenderer.material = _runtimeHotTipMaterial;
            _runtimeHotTip.SetActive(false);
        }

        void UpdateProceduralElectrodeVisual()
        {
            if (!UseProceduralElectrodeVisual) return;
            if (_runtimeRod == null) BuildRuntimeRod();
            if (_runtimeHotTip == null) BuildRuntimeHotTip();
            if (_runtimeRod == null || _runtimeRodLine == null || Tip == null) return;

            Vector3 start = Tip.position;
            Vector3 end = ArcOriginPosition;
            float len = Vector3.Distance(start, end);
            if (len < 1e-5f)
            {
                _runtimeRod.SetActive(false);
                if (_runtimeHotTip != null) _runtimeHotTip.SetActive(false);
                return;
            }

            float diameterMm = (Machine != null && Machine.Current != null)
                ? Machine.Current.DiameterMm : FallbackDiameterMm;
            float diameterM = Mathf.Max(0.0005f, diameterMm * 0.001f * VisualDiameterScale);

            _runtimeRod.SetActive(true);
            _runtimeRodLine.SetPosition(0, start);
            _runtimeRodLine.SetPosition(1, end);
            _runtimeRodLine.startWidth = diameterM;
            _runtimeRodLine.endWidth = diameterM * 0.94f;

            if (_runtimeHotTip != null)
            {
                bool hot = Arcing || State == ArcState.Striking;
                _runtimeHotTip.SetActive(hot);
                if (hot)
                {
                    _runtimeHotTip.transform.position = end;
                    float hotSize = Mathf.Max(diameterM * 1.8f, HotTipScale);
                    _runtimeHotTip.transform.localScale = Vector3.one * hotSize;
                }
            }
        }

        void RestoreOriginalElectrodeVisual()
        {
            if (_originalRodRenderers != null && _originalRendererStates != null)
                for (int i = 0; i < _originalRodRenderers.Length && i < _originalRendererStates.Length; i++)
                    if (_originalRodRenderers[i] != null) _originalRodRenderers[i].enabled = _originalRendererStates[i];

            if (_originalRodColliders != null && _originalColliderStates != null)
                for (int i = 0; i < _originalRodColliders.Length && i < _originalColliderStates.Length; i++)
                    if (_originalRodColliders[i] != null) _originalRodColliders[i].enabled = _originalColliderStates[i];
        }

        void OnDestroy()
        {
            RestoreOriginalElectrodeVisual();
            if (_runtimeRod != null) Destroy(_runtimeRod);
            if (_runtimeHotTip != null) Destroy(_runtimeHotTip);
            if (_runtimeRodMaterial != null) Destroy(_runtimeRodMaterial);
            if (_runtimeHotTipMaterial != null) Destroy(_runtimeHotTipMaterial);
        }

        /// Repone la varilla completa del electrodo seleccionado en la maquina.
        public void LoadNewElectrode()
        {
            float len = (Machine != null && Machine.Current != null) ? Machine.Current.LengthMm : 350f;
            _initialLenMm = len;
            RemainingMm = len;
            State = ArcState.Off;
            _strikeTimer = 0f;
            UpdateElectrodeVisual();
        }

        public void SetSparkRequested(bool on) => SparkRequested = on;

        /// Direccion del electrodo: desde la pieza HACIA el mango (para calcular angulos).
        public Vector3 ElectrodeDir => -PointingDir;

        /// Direccion en la que apunta la punta hacia la pieza.
        /// Si ElectrodeBase esta asignado se deduce por geometria; asi la medicion no depende
        /// de como quedaron orientados los ejes locales al importar el URDF.
        public Vector3 PointingDir
        {
            get
            {
                Transform physicalTip = (UseArcTipOverride && ArcTip != null) ? ArcTip : Tip;
                if (physicalTip == null) return Vector3.down;

                if (ElectrodeBase != null)
                {
                    Vector3 d = physicalTip.position - ElectrodeBase.position;
                    if (d.sqrMagnitude > 1e-8f) return d.normalized;
                }

                return physicalTip.TransformDirection(ElectrodeAxisLocal.normalized).normalized;
            }
        }

        /// <summary>
        /// Posicion FISICA desde donde nace el arco. Si existe ElectrodeVisual se toma el
        /// extremo de su malla mas avanzado en PointingDir. Esto evita medir desde el frame
        /// del porta-electrodo cuando la varilla visible mide ~350 mm.
        /// </summary>
        public Vector3 ArcOriginPosition
        {
            get
            {
                // Override manual opcional: si alguna vez existe un ArcTip real, usarlo.
                if (UseArcTipOverride && ArcTip != null) return ArcTip.position;

                // Caso normal de este proyecto: Tip es el punto de sujecion / TCP logico y
                // la varilla se extiende desde ahi en PointingDir. La punta fisica queda a
                // RemainingMm milimetros del Tip. Esto evita depender del pivot, bounds o
                // escala de la malla visual, que en el prefab no representan el largo real.
                if (Tip != null)
                    return Tip.position + PointingDir * (RemainingMm * 0.001f);

                // Fallback puramente visual si falta Tip.
                if (UseElectrodeVisualTip && TryGetElectrodeVisualEnd(out Vector3 p)) return p;

                return transform.position;
            }
        }

        public float LogicalToPhysicalTipOffsetMm =>
            Tip != null ? Vector3.Distance(Tip.position, ArcOriginPosition) * 1000f : 0f;

        bool TryGetElectrodeVisualEnd(out Vector3 worldEnd)
        {
            worldEnd = Vector3.zero;
            if (ElectrodeVisual == null) return false;

            Vector3 dir = PointingDir;
            if (dir.sqrMagnitude < 1e-8f) return false;
            dir.Normalize();

            MeshFilter mf = ElectrodeVisual.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;
                float best = float.NegativeInfinity;

                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 local = c + Vector3.Scale(e, new Vector3(x, y, z));
                    Vector3 world = ElectrodeVisual.TransformPoint(local);
                    float s = Vector3.Dot(world, dir);
                    if (s > best) { best = s; worldEnd = world; }
                }
                return true;
            }

            Renderer r = ElectrodeVisual.GetComponent<Renderer>();
            if (r != null)
            {
                Bounds b = r.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;
                float best = float.NegativeInfinity;

                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 world = c + Vector3.Scale(e, new Vector3(x, y, z));
                    float s = Vector3.Dot(world, dir);
                    if (s > best) { best = s; worldEnd = world; }
                }
                return true;
            }

            return false;
        }

        /// Longitud de arco medida por raycast desde la punta FISICA hacia la pieza.
        public float MeasureArcLenMm()
        {
            if (Tip == null && (!UseArcTipOverride || ArcTip == null) && ElectrodeVisual == null)
            {
                HasArcSurface = false;
                ArcLenMm = MeasureRangeMm;
                return ArcLenMm;
            }

            Vector3 origin = ArcOriginPosition;

            // El sensor busca lejos para poder diagnosticar aun cuando el robot arranca
            // bastante separado de la pieza. MaxArcLenMm sigue siendo el limite REAL del arco.
            float range = Mathf.Max(2.0f, Mathf.Max(1f, MeasureRangeMm) * 0.001f);

            // IMPORTANTE: siempre se lanza contra TODAS las capas. El filtro de capa se usa
            // solo como PREFERENCIA, no como requisito. Asi una WorkpieceMask mal configurada
            // no deja la distancia clavada para siempre.
            // Los controles UI usan triggers; no son superficies de soldadura.
            int n = Physics.RaycastNonAlloc(origin, PointingDir, _hitBuf, range, ~0, QueryTriggerInteraction.Ignore);
            bool gotPreferred = false;
            bool gotFallback = false;
            RaycastHit preferred = default(RaycastHit);
            RaycastHit fallback = default(RaycastHit);
            float bestPreferred = float.MaxValue;
            float bestFallback = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                Collider col = _hitBuf[i].collider;
                Transform t = col != null ? col.transform : null;
                if (t == null) continue;

                // Nunca medir contra el propio Panda/soldador.
                if (IgnoreRoot != null && (t == IgnoreRoot || t.IsChildOf(IgnoreRoot))) continue;

                float dist = _hitBuf[i].distance;
                if (dist < bestFallback)
                {
                    bestFallback = dist;
                    fallback = _hitBuf[i];
                    gotFallback = true;
                }

                if (UseLayerFilter)
                {
                    int bit = 1 << col.gameObject.layer;
                    bool allowed = (((int)WorkpieceMask) & bit) != 0;
                    if (allowed && dist < bestPreferred)
                    {
                        bestPreferred = dist;
                        preferred = _hitBuf[i];
                        gotPreferred = true;
                    }
                }
            }

            // Si la mascara esta bien, se usa. Si no encontro nada en esa capa, se hace
            // fallback automatico a la primera superficie NO perteneciente al robot.
            bool got = UseLayerFilter ? (gotPreferred || gotFallback) : gotFallback;
            RaycastHit hit = gotPreferred ? preferred : fallback;

            HasArcSurface = got;
            ArcLenMm = got ? hit.distance * 1000f : range * 1000f;

            if (LogRaycast) LogRaycastDetail(range, got, hit);

            if (DebugRay)
            {
                // Rojo = no pega (o esta fuera de alcance). Verde = pego en la pieza.
                // Se dibuja mas largo que el alcance real solo para poder verlo.
                Debug.DrawRay(origin, PointingDir * DebugRayLengthM,
                              got ? Color.green : Color.red);
                // Tramo corto en amarillo: el alcance REAL de medicion.
                Debug.DrawRay(origin, PointingDir * range, Color.yellow);
            }
            return ArcLenMm;
        }

        float _lastExternalTick = -999f;

        /// Red de seguridad: si nadie llama a Tick() desde afuera (tipicamente porque el
        /// WeldingTaskManager quedo sin la referencia al soldador, o esta desactivado), el
        /// soldador se actualiza solo. Sin esto el arco nunca ceba y NADA cambia: ni la
        /// longitud de arco, ni el estado, ni el consumo de varilla.
        void FixedUpdate()
        {
            if (Time.time - _lastExternalTick > 0.1f) TickInternal(Time.fixedDeltaTime);
        }

        /// Maquina de estados del arco. Llamar cada FixedUpdate.
        public void Tick(float dt)
        {
            _lastExternalTick = Time.time;
            TickInternal(dt);
        }

        void TickInternal(float dt)
        {
            MeasureArcLenMm();
            Electrode e = Machine != null ? Machine.Current : null;
            // Con alambre (MIG) el alimentador repone el stick-out: nunca se agota.
            bool consumed = e != null && !e.WireFeed && RemainingMm <= StubLengthMm;
            bool tripped  = Machine != null && Machine.ThermalTripped;

            ArcQuality = (e != null) ? WeldingHeatModel.ArcLengthFactor(ArcLenMm, e) : 0f;

            switch (State)
            {
                case ArcState.Stuck:
                    // Solo se despega separando fisicamente el electrodo.
                    if (ArcLenMm > UnstickPullMm) State = ArcState.Off;
                    break;

                case ArcState.Off:
                    if (!SparkRequested || consumed || tripped) { State = consumed || tripped ? ArcState.Blocked : ArcState.Off; break; }
                    if (!HasArcSurface || ArcLenMm >= MaxArcLenMm) break; // demasiado lejos, no ceba
                    if (ArcLenMm < StickArcLenMm) { State = ArcState.Stuck; break; }
                    _strikeTimer = 0f;
                    State = ArcState.Striking;
                    break;

                case ArcState.Striking:
                    if (!SparkRequested) { State = ArcState.Off; break; }
                    if (ArcLenMm < StickArcLenMm) { State = ArcState.Stuck; break; }
                    if (!HasArcSurface || ArcLenMm >= MaxArcLenMm) { State = ArcState.Off; break; }
                    _strikeTimer += dt;
                    if (_strikeTimer >= StrikeTimeSec) State = ArcState.Stable;
                    break;

                case ArcState.Stable:
                    if (!SparkRequested || consumed || tripped) { State = (consumed || tripped) ? ArcState.Blocked : ArcState.Off; break; }
                    if (ArcLenMm < StickArcLenMm) { State = ArcState.Stuck; break; }
                    if (!HasArcSurface || ArcLenMm >= MaxArcLenMm) { State = ArcState.Off; break; } // se corta el arco
                    // Consumo de varilla
                    if (e != null && Machine != null)
                    {
                        RemainingMm -= WeldingHeatModel.BurnOffMmPerSec(Machine.Amperage, e) * dt;
                        RemainingMm = Mathf.Max(0f, RemainingMm);
                        UpdateElectrodeVisual();
                    }
                    break;

                case ArcState.Blocked:
                    if (!consumed && !tripped) State = ArcState.Off;
                    break;
            }

            if (Machine != null) Machine.UpdateThermal(Arcing, dt);
        }

        float _rayLogTimer;

        /// Diagnostico decisivo: dispara el mismo rayo SIN filtro de capa y compara.
        ///  - filtrado NO pega y sin filtro SI pega en la chapa -> problema de MASCARA/CAPA.
        ///  - ninguno de los dos pega -> problema de DIRECCION o de ALCANCE.
        ///  - sin filtro pega en otra cosa -> hay algo tapando el paso.
        void LogRaycastDetail(float range, bool got, RaycastHit hit)
        {
            _rayLogTimer += Time.fixedDeltaTime;
            if (_rayLogTimer < 1.0f) return;
            _rayLogTimer = 0f;

            string conFiltro = got
                ? "PEGO en '" + (hit.collider != null ? hit.collider.name : "?") + "' a " + (hit.distance * 1000f).ToString("0.0") + "mm"
                : "no pego";

            Vector3 origin = ArcOriginPosition;

            // Mismo rayo, sin mascara y con mucho mas alcance.
            string sinFiltro;
            if (Physics.Raycast(origin, PointingDir, out RaycastHit free, 5f))
            {
                int lay = free.collider != null ? free.collider.gameObject.layer : -1;
                sinFiltro = "PEGO en '" + (free.collider != null ? free.collider.name : "?") +
                            "' capa=" + lay + " (" + LayerMask.LayerToName(lay) + ") a " +
                            (free.distance * 1000f).ToString("0.0") + "mm";
            }
            else sinFiltro = "no pego en NADA en 5 m";

            Debug.Log(string.Format(
                "[Torch] frameTip={0}  arcTip={1}  origenArco={2}  offset={3:0.0}mm\n" +
                "        dir={4}  alcanceMedicion={5:0.0}mm  maxArco={6:0.0}mm  mascara={7}\n" +
                "        IgnoreRoot={8}\n        MEDICION: {9}\n        SIN filtro: {10}",
                Tip != null ? Tip.position.ToString() : "NULL",
                (UseArcTipOverride && ArcTip != null) ? ArcTip.position.ToString() : "IGNORADO",
                origin, LogicalToPhysicalTipOffsetMm,
                PointingDir, range * 1000f, MaxArcLenMm, WorkpieceMask.value,
                IgnoreRoot != null ? IgnoreRoot.name : "NULL", conFiltro, sinFiltro));
        }

        /// Potencia depositada ahora (0 si no hay arco estable).
        public float CurrentPowerWatts()
        {
            if (!Arcing || Machine == null || Machine.Current == null) return 0f;
            return WeldingHeatModel.PowerWatts(Machine.Amperage, ArcLenMm, Machine.Current);
        }

        void UpdateElectrodeVisual()
        {
            if (UseProceduralElectrodeVisual)
            {
                UpdateProceduralElectrodeVisual();
                return;
            }

            // Fallback para escenas antiguas: conserva el comportamiento previo basado en escala.
            if (ElectrodeVisual == null || _initialLenMm <= 0f) return;
            CaptureElectrodeScale();
            float f = Mathf.Clamp01(RemainingMm / _initialLenMm);
            ElectrodeVisual.localScale = new Vector3(
                _electrodeBaseScale.x,
                _electrodeBaseScale.y * Mathf.Max(0.05f, f),
                _electrodeBaseScale.z);
        }

        public string StateLabel()
        {
            switch (State)
            {
                case ArcState.Stable:   return "Arco estable";
                case ArcState.Striking: return "Cebando...";
                case ArcState.Stuck:    return "ELECTRODO PEGADO - separar";
                case ArcState.Blocked:  return RemainingMm <= StubLengthMm ? "Electrodo consumido - reponer" : "Maquina bloqueada";
                default:                return "Sin arco";
            }
        }

        void OnDrawGizmos()
        {
            if (Tip == null && (!UseArcTipOverride || ArcTip == null) && ElectrodeVisual == null) return;
            Gizmos.color = Arcing ? Color.yellow : Color.gray;
            Vector3 origin = ArcOriginPosition;
            Gizmos.DrawSphere(origin, 0.004f);
            Gizmos.DrawRay(origin, PointingDir * (MeasureRangeMm * 0.001f));
        }
    }
}
