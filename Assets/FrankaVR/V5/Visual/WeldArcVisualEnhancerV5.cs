using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V5
{
    /// <summary>
    /// Agrega nucleo visible de arco y charco luminoso sin sustituir WeldingFX.
    /// </summary>
    public class WeldArcVisualEnhancerV5 : MonoBehaviour
    {
        public WeldingTorch Torch;
        public float CoreWidth = 0.0022f;
        public float PoolSize = 0.016f;
        public Color CoreColor = new Color(0.82f, 0.94f, 1f, 1f);
        public Color PoolColor = new Color(1f, 0.28f, 0.03f, 1f);
        public bool AdditionalLight = false;
        public float LightIntensity = 3.5f;

        LineRenderer _core;
        Transform _pool;
        Renderer _poolRenderer;
        Material _coreMat, _poolMat;
        Light _light;

        void Start()
        {
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            Build();
        }

        void Build()
        {
            Shader unlit = Shader.Find("Sprites/Default");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");

            GameObject core = new GameObject("ArcCoreV5");
            core.transform.SetParent(transform, false);
            _core = core.AddComponent<LineRenderer>();
            _core.positionCount = 2;
            _core.useWorldSpace = true;
            _core.startWidth = _core.endWidth = CoreWidth;
            _core.numCapVertices = 4;
            if (unlit != null)
            {
                _coreMat = new Material(unlit); _coreMat.color = CoreColor;
                _core.material = _coreMat;
            }

            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pool.name = "MoltenPoolV5";
            pool.transform.SetParent(transform, false);
            Collider c = pool.GetComponent<Collider>(); if (c != null) Destroy(c);
            _pool = pool.transform;
            _pool.localScale = new Vector3(PoolSize * 1.8f, PoolSize * 0.22f, PoolSize);
            _poolRenderer = pool.GetComponent<Renderer>();
            if (unlit != null)
            {
                _poolMat = new Material(unlit); _poolMat.color = PoolColor;
                _poolRenderer.material = _poolMat;
            }

            if (AdditionalLight)
            {
                GameObject lg = new GameObject("ArcLightV5");
                lg.transform.SetParent(transform, false);
                _light = lg.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(0.72f, 0.88f, 1f);
                _light.range = 1.15f;
                _light.intensity = LightIntensity;
                _light.shadows = LightShadows.None;
            }

            SetVisible(false);
        }

        void Update()
        {
            if (Torch == null || _core == null || _pool == null) return;
            bool active = Torch.Arcing || Torch.State == ArcState.Striking;
            if (!active || !Torch.HasArcSurface)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            Vector3 a = Torch.ArcOriginPosition;
            Vector3 b = a + Torch.PointingDir * (Torch.ArcLenMm * 0.001f);
            _core.SetPosition(0, a);
            _core.SetPosition(1, b);
            _pool.position = b + Vector3.up * 0.001f;

            float q = Mathf.Clamp01(Torch.ArcQuality);
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.time * (28f + 12f * (1f - q)));
            _pool.localScale = new Vector3(PoolSize * 1.8f, PoolSize * 0.22f, PoolSize) * pulse;
            if (_light != null)
            {
                _light.transform.position = b + Vector3.up * 0.02f;
                _light.intensity = LightIntensity * (0.75f + 0.35f * pulse);
            }
        }

        void SetVisible(bool on)
        {
            if (_core != null) _core.enabled = on;
            if (_poolRenderer != null) _poolRenderer.enabled = on;
            if (_light != null) _light.enabled = on;
        }

        void OnDestroy()
        {
            if (_coreMat != null) Destroy(_coreMat);
            if (_poolMat != null) Destroy(_poolMat);
        }
    }
}
