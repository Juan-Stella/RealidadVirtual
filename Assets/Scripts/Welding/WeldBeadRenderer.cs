using UnityEngine;

namespace FrankaVR.Welding
{
    /// Genera la malla del cordon de soldadura en tiempo real a partir del estado de la
    /// costura. Cada punto con energia produce un anillo de vertices; los anillos
    /// consecutivos se unen formando un tubo. El color por vertice codifica el
    /// resultado (calidad) mezclado con el brillo del metal caliente.
    ///
    /// Usar con el shader FrankaVR/WeldBead (unlit + vertex color) para que se vea el
    /// naranja incandescente sin depender de iluminacion.
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class WeldBeadRenderer : MonoBehaviour
    {
        [Header("Referencias")]
        public WeldSeam Seam;

        [Header("Forma del cordon")]
        [Tooltip("Vertices por anillo. 6 alcanza y es barato para mobile.")]
        public int RingSegments = 6;
        public float MinRadius = 0.0015f;
        public float MaxRadius = 0.005f;
        [Tooltip("Cuanto sobresale el cordon de la superficie.")]
        public float Lift = 0.0012f;

        [Header("Color")]
        public Color HotColor = new Color(1f, 0.55f, 0.12f, 1f);
        public Color WhiteHot = new Color(1f, 0.92f, 0.7f, 1f);

        Mesh _mesh;
        MeshFilter _mf;

        Vector3[] _verts;
        Color[]   _cols;
        int[]     _tris;
        int _cachedPoints = -1, _cachedSegs = -1;

        void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mesh = new Mesh();
            _mesh.name = "WeldBead";
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

        /// Reserva los buffers una sola vez mientras no cambie la cantidad de puntos.
        void EnsureBuffers(int points, int segs)
        {
            if (_cachedPoints == points && _cachedSegs == segs) return;

            int vcount = points * segs;
            int tcount = (points - 1) * segs * 6;
            _verts = new Vector3[vcount];
            _cols  = new Color[vcount];
            _tris  = new int[tcount];

            // La topologia es fija: solo se recalculan posiciones y colores por frame.
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
            int segs = Mathf.Max(3, RingSegments);
            EnsureBuffers(n, segs);

            for (int i = 0; i < n; i++)
            {
                SeamPoint sp = Seam.Get(i);
                WeldQuality q = Seam.QualityAt(i);

                float w = Seam.BeadWidth01(i);
                float radius = q == WeldQuality.None ? 0f : Mathf.Lerp(MinRadius, MaxRadius, w);
                // La perforacion no engorda el cordon: lo hunde.
                if (q == WeldQuality.BurnThrough) radius = MinRadius * 0.6f;

                Vector3 n0 = sp.Normal.normalized;
                Vector3 t0 = sp.Tangent.normalized;
                Vector3 b0 = Vector3.Cross(n0, t0).normalized;
                Vector3 center = sp.Pos + n0 * (q == WeldQuality.BurnThrough ? -Lift : Lift);

                // Color: calidad de base, mezclada hacia incandescente segun temperatura.
                Color baseCol = WeldingHeatModel.QualityColor(q);
                float temp = sp.Temp;
                Color hot = Color.Lerp(HotColor, WhiteHot, Mathf.Clamp01((temp - 0.6f) / 0.4f));
                Color col = Color.Lerp(baseCol, hot, temp);

                int off = i * segs;
                for (int s = 0; s < segs; s++)
                {
                    float a = (s / (float)segs) * 2f * Mathf.PI;
                    Vector3 radial = b0 * Mathf.Cos(a) + n0 * Mathf.Sin(a);
                    _verts[off + s] = transform.InverseTransformPoint(center + radial * radius);
                    _cols[off + s]  = col;
                }
            }

            _mesh.Clear();
            _mesh.vertices  = _verts;
            _mesh.colors    = _cols;
            _mesh.triangles = _tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }
}
