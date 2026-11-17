using UnityEngine;

namespace FrankaVR.Welding
{
    /// Estado de un punto de la costura.
    public struct SeamPoint
    {
        public Vector3 Pos;
        public Vector3 Normal;   // normal de la junta (hacia donde debe apuntar el electrodo)
        public Vector3 Tangent;  // direccion de avance
        public float   Energy;   // J acumulados
        public float   AngleAcc; // suma de calidad angular ponderada por energia
        public float   ArcAcc;   // suma de calidad de arco ponderada por energia
        public float   Temp;     // 0..1 temperatura visual (se enfria con el tiempo)
        public float   LastHeat; // tiempo de la ultima deposicion

        public float AngleQ => Energy > 1f ? Mathf.Clamp01(AngleAcc / Energy) : 0f;
        public float ArcQ   => Energy > 1f ? Mathf.Clamp01(ArcAcc   / Energy) : 0f;
    }

    /// La costura a soldar: una polilinea (recta o circunferencia) de puntos que acumulan
    /// energia y calidad. Es el modelo de datos que consume el renderer del cordon.
    public class WeldSeam : MonoBehaviour
    {
        [Header("Geometria")]
        public SeamShape Shape = SeamShape.Line;
        public Transform Start;              // extremo inicial (modo Line)
        public Transform End;                // extremo final   (modo Line)
        public Transform CircleCenter;       // centro (modo Circle)
        public float     CircleRadius = 0.06f;
        public Vector3   CircleAxis = Vector3.right; // eje del caño
        public int       Samples = 60;

        [Header("Proceso")]
        public float ThicknessMm  = 6f;
        public float DepositRadius = 0.008f; // m: radio de influencia del arco
        public float IdealTravelAngle = 12f;
        public float IdealWorkAngle   = 0f;
        [Tooltip("La costura avanza al reves que la orden original: el lado (n x t) se invierte " +
                 "y el angulo de trabajo cambia de signo para conservar la misma inclinacion fisica.")]
        public bool ReverseWorkSide = false;
        public float AngleToleranceDeg = 12f;

        [Header("Enfriamiento")]
        public float CoolSecs = 6f; // tiempo que tarda el metal en dejar de verse caliente

        SeamPoint[] _pts;
        public int Count => _pts != null ? _pts.Length : 0;
        public bool Dirty { get; set; } // avisa al renderer que hay que rehacer la malla

        void Awake() { Build(); }

        /// Construye los puntos de muestreo segun la forma configurada.
        public void Build()
        {
            int n = Mathf.Max(2, Samples);
            _pts = new SeamPoint[n];

            if (Shape == SeamShape.Circle)
            {
                Vector3 c = CircleCenter ? CircleCenter.position : transform.position;
                Vector3 ax = CircleAxis.normalized;
                // base ortonormal perpendicular al eje del caño
                Vector3 u = Vector3.Cross(ax, Vector3.up);
                if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(ax, Vector3.forward);
                u = u.normalized;
                Vector3 v = Vector3.Cross(ax, u).normalized;

                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * 2f * Mathf.PI; // circunferencia cerrada
                    Vector3 radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    _pts[i].Pos     = c + radial * CircleRadius;
                    _pts[i].Normal  = radial;                        // sale del caño
                    _pts[i].Tangent = Vector3.Cross(ax, radial).normalized;
                }
            }
            else
            {
                Vector3 a0 = Start ? Start.position : transform.position;
                Vector3 b0 = End   ? End.position   : transform.position + transform.right * 0.2f;
                Vector3 dir = (b0 - a0).normalized;
                Vector3 nrm = (Start ? Start.up : transform.up).normalized;
                // ortogonalizar la normal respecto a la direccion de avance
                nrm = Vector3.ProjectOnPlane(nrm, dir).normalized;
                if (nrm.sqrMagnitude < 1e-4f) nrm = Vector3.up;

                for (int i = 0; i < n; i++)
                {
                    _pts[i].Pos     = Vector3.Lerp(a0, b0, i / (float)(n - 1));
                    _pts[i].Normal  = nrm;
                    _pts[i].Tangent = dir;
                }
            }
            Dirty = true;
        }

        public void ResetSeam()
        {
            if (_pts == null) { Build(); return; }
            for (int i = 0; i < _pts.Length; i++)
            {
                _pts[i].Energy = 0f; _pts[i].AngleAcc = 0f;
                _pts[i].ArcAcc = 0f; _pts[i].Temp = 0f; _pts[i].LastHeat = 0f;
            }
            Dirty = true;
        }

        public SeamPoint Get(int i) => _pts[i];

        /// Giro de la pieza N5 sin reconstruir ni perder energia/calidad depositada.
        public void RotateCircleSamples(float degrees)
        {
            if (Shape != SeamShape.Circle || _pts == null) return;
            Vector3 center = CircleCenter ? CircleCenter.position : transform.position;
            Quaternion rotation = Quaternion.AngleAxis(degrees, CircleAxis.normalized);
            for (int i = 0; i < _pts.Length; i++)
            {
                _pts[i].Pos = center + rotation * (_pts[i].Pos - center);
                _pts[i].Normal = rotation * _pts[i].Normal;
                _pts[i].Tangent = rotation * _pts[i].Tangent;
            }
            Dirty = true;
        }
        public Vector3 PointAt(int i) => _pts[i].Pos;
        public Vector3 NormalAt(int i) => _pts[i].Normal;
        public Vector3 TangentAt(int i) => _pts[i].Tangent;

        public WeldQuality QualityAt(int i)
            => WeldingHeatModel.Classify(_pts[i].Energy, ThicknessMm, _pts[i].AngleQ, _pts[i].ArcQ);

        /// Ancho relativo del cordon en el punto i (0..1+), para el renderer.
        public float BeadWidth01(int i)
        {
            WeldingHeatModel.Thresholds(ThicknessMm, out float coldMax, out float burnMin);
            return Mathf.Clamp01(_pts[i].Energy / burnMin);
        }

        /// Deposita energia desde el arco. Devuelve true si toco al menos un punto.
        /// electrodeDir: direccion desde la pieza hacia el mango (eje del electrodo).
        public bool Deposit(Vector3 tipPos, Vector3 electrodeDir, float watts, float arcQuality, float dt)
        {
            if (_pts == null) Build();
            bool touched = false;
            float r2 = DepositRadius * DepositRadius;

            for (int i = 0; i < _pts.Length; i++)
            {
                Vector3 d = tipPos - _pts[i].Pos;
                if (d.sqrMagnitude > r2) continue;

                float falloff = 1f - d.magnitude / DepositRadius;
                float e = watts * dt * falloff;
                if (e <= 0f) continue;

                WeldingHeatModel.ToolAngles(electrodeDir, _pts[i].Tangent, _pts[i].Normal,
                                            out float travel, out float work);
                float aq = WeldingHeatModel.AngleQuality(travel, work,
                                                         IdealTravelAngle, IdealWorkAngle, AngleToleranceDeg);

                _pts[i].Energy   += e;
                _pts[i].AngleAcc += aq * e;          // promedio ponderado por energia
                _pts[i].ArcAcc   += arcQuality * e;
                _pts[i].Temp      = 1f;
                _pts[i].LastHeat  = Time.time;
                touched = true;
            }
            if (touched) Dirty = true;
            return touched;
        }

        /// Enfriamiento visual del metal ya depositado.
        public void UpdateCooling()
        {
            if (_pts == null) return;
            bool any = false;
            for (int i = 0; i < _pts.Length; i++)
            {
                if (_pts[i].Temp <= 0f) continue;
                float t = (Time.time - _pts[i].LastHeat) / Mathf.Max(0.1f, CoolSecs);
                float nt = Mathf.Clamp01(1f - t);
                if (!Mathf.Approximately(nt, _pts[i].Temp)) { _pts[i].Temp = nt; any = true; }
            }
            if (any) Dirty = true;
        }

        public float TempAt(int i) => _pts[i].Temp;

        /// Conteo por categoria de defecto.
        public void Tally(out int good, out int cold, out int burn, out int undercut, out int porosity, out int untouched)
        {
            good = cold = burn = undercut = porosity = untouched = 0;
            if (_pts == null) return;
            for (int i = 0; i < _pts.Length; i++)
            {
                switch (QualityAt(i))
                {
                    case WeldQuality.Good:        good++;      break;
                    case WeldQuality.Cold:        cold++;      break;
                    case WeldQuality.BurnThrough: burn++;      break;
                    case WeldQuality.Undercut:    undercut++;  break;
                    case WeldQuality.Porosity:    porosity++;  break;
                    default:                      untouched++; break;
                }
            }
        }

        /// Aplica la configuracion de una orden de trabajo a esta costura.
        public void ApplyWorkOrder(WorkOrder wo)
        {
            Shape              = wo.Shape;
            ThicknessMm        = wo.ThicknessMm;
            IdealTravelAngle   = wo.IdealTravelAngle;
            IdealWorkAngle     = ReverseWorkSide ? -wo.IdealWorkAngle : wo.IdealWorkAngle;
            AngleToleranceDeg  = wo.AngleToleranceDeg;
            if (wo.Shape == SeamShape.Circle) CircleRadius = wo.PipeRadiusM;
            Build();
        }

        void OnDrawGizmos()
        {
            if (_pts == null) return;
            for (int i = 0; i < _pts.Length; i++)
            {
                Gizmos.color = WeldingHeatModel.QualityColor(QualityAt(i));
                Gizmos.DrawSphere(_pts[i].Pos, 0.003f);
            }
        }
    }
}
