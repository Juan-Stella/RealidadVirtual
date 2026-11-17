using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V6
{
    /// <summary>
    /// Cordon mas creible para entrenamiento: seccion eliptica apoyada sobre la superficie,
    /// ondulacion sutil y ancho/altura desacoplados.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class WeldBeadRendererV6 : MonoBehaviour
    {
        public WeldSeam Seam;

        [Header("Forma")]
        [Range(5, 18)] public int CrossSegments = 9;
        public float MinWidth = 0.006f;
        public float MaxWidth = 0.018f;
        public float MinHeight = 0.0010f;
        public float MaxHeight = 0.0048f;
        public float Lift = 0.0004f;
        public float RippleAmplitude = 0.00055f;
        public float RippleFrequency = 1.4f;
        public bool MakeEndsRounder = true;

        [Header("Color")]
        public Color HotColor = new Color(1f, 0.58f, 0.16f, 1f);
        public Color WhiteHot = new Color(1f, 0.94f, 0.74f, 1f);

        Mesh _mesh;
        MeshFilter _mf;
        Vector3[] _verts;
        Color[] _cols;
        int[] _tris;
        int _cachedPoints = -1, _cachedSegs = -1;

        void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mesh = new Mesh();
            _mesh.name = "WeldBeadV6";
            _mesh.MarkDynamic();
            _mf.mesh = _mesh;
        }

        void LateUpdate()
        {
            if (Seam == null || Seam.Count < 2) return;
            Seam.UpdateCooling();
            if (!Seam.Dirty) return;
            Rebuild();
            Seam.Dirty = false;
        }

        void EnsureBuffers(int points, int segs)
        {
            if (_cachedPoints == points && _cachedSegs == segs) return;
            int vcount = points * segs;
            int tcount = (points - 1) * (segs - 1) * 6;
            _verts = new Vector3[vcount];
            _cols = new Color[vcount];
            _tris = new int[tcount];

            int k = 0;
            for (int i = 0; i < points - 1; i++)
            {
                int a = i * segs, b = (i + 1) * segs;
                for (int s = 0; s < segs - 1; s++)
                {
                    _tris[k++] = a + s;
                    _tris[k++] = b + s;
                    _tris[k++] = a + s + 1;
                    _tris[k++] = a + s + 1;
                    _tris[k++] = b + s;
                    _tris[k++] = b + s + 1;
                }
            }
            _cachedPoints = points; _cachedSegs = segs;
            _mesh.Clear();
        }

        void Rebuild()
        {
            int n = Seam.Count;
            int segs = Mathf.Max(5, CrossSegments);
            EnsureBuffers(n, segs);

            for (int i = 0; i < n; i++)
            {
                SeamPoint sp = Seam.Get(i);
                WeldQuality q = Seam.QualityAt(i);
                float energy = Mathf.Clamp01(Seam.BeadWidth01(i));
                float endMask = 1f;
                if (MakeEndsRounder)
                {
                    float t = i / (float)(n - 1);
                    endMask = Mathf.SmoothStep(0.25f, 1f, Mathf.Min(t * 3f, (1f - t) * 3f));
                }

                float width = q == WeldQuality.None ? 0f : Mathf.Lerp(MinWidth, MaxWidth, energy) * endMask;
                float height = q == WeldQuality.None ? 0f : Mathf.Lerp(MinHeight, MaxHeight, energy) * endMask;
                if (q == WeldQuality.BurnThrough)
                {
                    width *= 0.75f;
                    height *= 0.25f;
                }

                Vector3 n0 = sp.Normal.normalized;
                Vector3 t0 = sp.Tangent.normalized;
                Vector3 b0 = Vector3.Cross(n0, t0).normalized;
                Vector3 center = sp.Pos + n0 * (q == WeldQuality.BurnThrough ? -Lift : Lift);

                Color baseCol = WeldingHeatModel.QualityColor(q);
                float temp = sp.Temp;
                Color hot = Color.Lerp(HotColor, WhiteHot, Mathf.Clamp01((temp - 0.55f) / 0.45f));
                Color col = Color.Lerp(baseCol, hot, temp);

                int off = i * segs;
                for (int s = 0; s < segs; s++)
                {
                    float u = s / (float)(segs - 1); // 0..1 across bead
                    float x = (u - 0.5f) * width;
                    float arch = Mathf.Sin(u * Mathf.PI); // 0..1..0
                    float ripple = Mathf.Sin((i * RippleFrequency + u * 0.75f) * Mathf.PI * 2f) * RippleAmplitude * energy;
                    float y = arch * height + ripple;
                    if (q == WeldQuality.BurnThrough) y -= height * 0.6f;

                    Vector3 p = center + b0 * x + n0 * y;
                    _verts[off + s] = transform.InverseTransformPoint(p);
                    _cols[off + s] = col;
                }
            }

            _mesh.Clear();
            _mesh.vertices = _verts;
            _mesh.colors = _cols;
            _mesh.triangles = _tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }
}
