using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V6
{
    /// <summary>
    /// Version visual mejorada del arco: nucleo brillante, halo, charco mas plano y luz oscilante.
    /// No reemplaza WeldingFX; suma una capa visual adicional.
    /// </summary>
    public class WeldArcVisualEnhancerV6 : MonoBehaviour
    {
        public WeldingTorch Torch;
        public float CoreWidth = 0.0024f;
        public float HaloWidth = 0.0065f;
        public float PoolSize = 0.018f;
        public Color CoreColor = new Color(0.88f, 0.96f, 1f, 1f);
        public Color HaloColor = new Color(0.42f, 0.76f, 1f, 0.65f);
        public Color PoolColor = new Color(1f, 0.34f, 0.06f, 1f);
        public float LightIntensity = 4.0f;

        LineRenderer _core, _halo;
        Transform _pool;
        Renderer _poolRenderer;
        Material _coreMat, _haloMat, _poolMat;
        Light _light;

        void Start()
        {
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            Build();
        }

        void Build()
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) return;

            _core = MakeLine("ArcCoreV6", CoreWidth, CoreColor, out _coreMat);
            _halo = MakeLine("ArcHaloV6", HaloWidth, HaloColor, out _haloMat);

            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pool.name = "MoltenPoolV6";
            pool.transform.SetParent(transform, false);
            Destroy(pool.GetComponent<Collider>());
            _pool = pool.transform;
            _pool.localScale = new Vector3(PoolSize * 2.0f, PoolSize * 0.16f, PoolSize * 1.18f);
            _poolRenderer = pool.GetComponent<Renderer>();
            _poolMat = new Material(sh); _poolMat.color = PoolColor;
            _poolRenderer.sharedMaterial = _poolMat;

            var lg = new GameObject("ArcLightV6");
            lg.transform.SetParent(transform, false);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.78f, 0.90f, 1f);
            _light.range = 1.30f;
            _light.intensity = LightIntensity;
            _light.shadows = LightShadows.None;

            SetVisible(false);
        }

        LineRenderer MakeLine(string name, float width, Color color, out Material mat)
        {
            Shader sh = Shader.Find("Sprites/Default");
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = width;
            lr.numCapVertices = 4;
            mat = sh != null ? new Material(sh) : null;
            if (mat != null)
            {
                mat.color = color;
                lr.material = mat;
            }
            return lr;
        }

        void Update()
        {
            if (Torch == null || _core == null || _halo == null || _pool == null) return;
            bool active = (Torch.Arcing || Torch.State == ArcState.Striking) && Torch.HasArcSurface;
            if (!active)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);

            Vector3 a = Torch.ArcOriginPosition;
            Vector3 b = a + Torch.PointingDir * (Torch.ArcLenMm * 0.001f);
            _core.SetPosition(0, a); _core.SetPosition(1, b);
            _halo.SetPosition(0, a); _halo.SetPosition(1, b);

            float q = Mathf.Clamp01(Torch.ArcQuality);
            float flicker = 0.85f + 0.20f * Mathf.Sin(Time.time * (24f + 10f * (1f - q)));
            _pool.position = b + Vector3.up * 0.0012f;
            _pool.localScale = new Vector3(PoolSize * 2.0f, PoolSize * 0.16f, PoolSize * 1.18f) * flicker;
            _light.transform.position = b + Vector3.up * 0.03f;
            _light.intensity = LightIntensity * flicker;
        }

        void SetVisible(bool on)
        {
            if (_core != null) _core.enabled = on;
            if (_halo != null) _halo.enabled = on;
            if (_poolRenderer != null) _poolRenderer.enabled = on;
            if (_light != null) _light.enabled = on;
        }

        void OnDestroy()
        {
            if (_coreMat != null) Destroy(_coreMat);
            if (_haloMat != null) Destroy(_haloMat);
            if (_poolMat != null) Destroy(_poolMat);
        }
    }
}
