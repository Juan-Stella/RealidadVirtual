using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Cordon SMAW visualmente mas legible: seccion eliptica (ancho > altura), ondulacion
    /// tipo "stack of dimes" y presencia visible desde los primeros joules depositados.
    /// No modifica la evaluacion de calidad ni la fisica del proceso: solo el render.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class WeldBeadRenderer : MonoBehaviour
    {
        [Header("Referencias")]
        public WeldSeam Seam;

        [Header("Forma del cordon")]
        [Range(6, 16)] public int RingSegments = 10;
        [Tooltip("Semiancho minimo visible del cordon.")]
        public float MinHalfWidth = 0.0045f;   // 9 mm total
        [Tooltip("Semiancho maximo visual.")]
        public float MaxHalfWidth = 0.0090f;   // 18 mm total
        [Tooltip("Altura minima sobre la chapa.")]
        public float MinHeight = 0.0014f;
        [Tooltip("Altura maxima sobre la chapa.")]
        public float MaxHeight = 0.0032f;
        [Tooltip("Eleva levemente la seccion para evitar z-fighting con la chapa.")]
        public float Lift = 0.0008f;
        [Tooltip("Energia minima para que empiece a verse material depositado.")]
        public float VisibleEnergyJ = 1.0f;

        [Header("Textura geometrica")]
        [Range(0f, 0.40f)] public float RippleStrength = 0.18f;
        [Tooltip("Ondas visuales a lo largo del cordon.")]
        public float RippleFrequency = 0.85f;
        [Range(0f, 0.30f)] public float SideIrregularity = 0.08f;
        [Range(0f, 0.25f)] public float HeightIrregularity = 0.07f;

        [Header("Color")]
        public Color HotColor = new Color(1f, 0.42f, 0.05f, 1f);
        public Color WhiteHot = new Color(1f, 0.96f, 0.80f, 1f);
        public Color ColdMetalColor = new Color(0.24f, 0.25f, 0.27f, 1f);
        [Range(0f, 1f)] public float QualityTintStrength = 0.38f;

        Mesh _mesh;
        MeshFilter _mf;
        Vector3[] _verts;
        Color[] _cols;
        int[] _tris;
        int _cachedPoints = -1, _cachedSegs = -1;

        void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mesh = new Mesh { name = "WeldBead" };
            _mesh.MarkDynamic();
            if (_mf != null) _mf.mesh = _mesh;
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
            int tcount = (points - 1) * segs * 6;
            _verts = new Vector3[vcount];
            _cols = new Color[vcount];
            _tris = new int[tcount];

            int k = 0;
            for (int i = 0; i < points - 1; i++)
            {
                int a = i * segs, b = (i + 1) * segs;
                for (int s = 0; s < segs; s++)
                {
                    int s2 = (s + 1) % segs;
                    _tris[k++] = a + s;  _tris[k++] = b + s;  _tris[k++] = a + s2;
                    _tris[k++] = a + s2; _tris[k++] = b + s;  _tris[k++] = b + s2;
                }
            }

            _cachedPoints = points;
            _cachedSegs = segs;
            if (_mesh != null) _mesh.Clear();
        }

        void Rebuild()
        {
            int n = Seam.Count;
            int segs = Mathf.Max(6, RingSegments);
            EnsureBuffers(n, segs);

            for (int i = 0; i < n; i++)
            {
                SeamPoint sp = Seam.Get(i);
                WeldQuality q = Seam.QualityAt(i);
                bool hasMetal = sp.Energy >= VisibleEnergyJ;

                // sqrt hace que el cordon gane presencia visual temprano sin modificar la energia real.
                float w = hasMetal ? Mathf.Sqrt(Mathf.Clamp01(Seam.BeadWidth01(i))) : 0f;
                float halfWidth = hasMetal ? Mathf.Lerp(MinHalfWidth, MaxHalfWidth, w) : 0f;
                float height = hasMetal ? Mathf.Lerp(MinHeight, MaxHeight, w) : 0f;

                if (q == WeldQuality.BurnThrough)
                {
                    halfWidth = Mathf.Max(MinHalfWidth * 0.65f, halfWidth * 0.72f);
                    height = Mathf.Max(0.0005f, height * 0.35f);
                }

                float phase = i * RippleFrequency * Mathf.PI * 2f;
                float ripple = 1f + RippleStrength * Mathf.Sin(phase);
                halfWidth *= Mathf.Max(0.62f, ripple);
                height *= Mathf.Max(0.72f, 1f + HeightIrregularity * Mathf.Sin(phase + 1.1f));

                Vector3 n0 = sp.Normal.normalized;
                Vector3 t0 = sp.Tangent.normalized;
                Vector3 b0 = Vector3.Cross(n0, t0).normalized;
                if (b0.sqrMagnitude < 1e-8f) b0 = Vector3.right;

                float side = hasMetal ? SideIrregularity * halfWidth * Mathf.Sin(i * 1.73f) : 0f;
                Vector3 center = sp.Pos
                    + n0 * (q == WeldQuality.BurnThrough ? -Lift : Lift)
                    + b0 * side;

                Color quality = WeldingHeatModel.QualityColor(q);
                Color baseCol = Color.Lerp(ColdMetalColor, quality, QualityTintStrength);
                float temp = sp.Temp;
                Color hot = Color.Lerp(HotColor, WhiteHot, Mathf.Clamp01((temp - 0.50f) / 0.50f));
                Color col = Color.Lerp(baseCol, hot, temp);
                if (!hasMetal) col.a = 0f;

                int off = i * segs;
                for (int s = 0; s < segs; s++)
                {
                    float a = (s / (float)segs) * 2f * Mathf.PI;
                    // Seccion eliptica: ancha sobre la chapa y baja en la normal.
                    Vector3 radial = b0 * (Mathf.Cos(a) * halfWidth)
                                   + n0 * (Mathf.Sin(a) * height);
                    _verts[off + s] = transform.InverseTransformPoint(center + radial);
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
