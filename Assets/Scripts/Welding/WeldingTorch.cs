using UnityEngine;

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
    /// 'Tip' es la punta del electrodo = TCP al que apunta el IK.
    ///
    /// Modela el comportamiento real del electrodo revestido:
    ///  - hay que CEBAR el arco (no enciende instantaneo)
    ///  - si se apoya con arco nulo, el electrodo SE PEGA y hay que despegarlo
    ///  - el electrodo SE CONSUME: la varilla se acorta y hay que reponerla
    public class WeldingTorch : MonoBehaviour
    {
        [Header("Referencias")]
        public Transform      Tip;                 // punta del electrodo (TCP)
        public WeldingMachine Machine;
        public Transform      ElectrodeVisual;     // cilindro que se acorta al consumirse
        public LayerMask      WorkpieceMask = ~0;

        [Header("Geometria")]
        [Tooltip("Eje local del soporte a lo largo del cual apunta el electrodo hacia la pieza.")]
        public Vector3 ElectrodeAxisLocal = Vector3.down;
        public float   MaxArcRayMm = 40f;
        [Tooltip("Longitud minima util de la varilla: por debajo hay que cambiarla.")]
        public float   StubLengthMm = 50f;

        [Header("Cebado / pegado")]
        public float StrikeTimeSec  = 0.25f; // demora en establecer el arco
        public float StickArcLenMm  = 0.4f;  // por debajo de esto se pega
        public float UnstickPullMm  = 4f;    // separacion necesaria para despegar

        [Header("Estado (solo lectura)")]
        public ArcState State = ArcState.Off;
        public float    ArcLenMm;
        public float    RemainingMm = 350f;
        public float    ArcQuality;    // 0..1 estabilidad instantanea del arco
        public bool     SparkRequested;

        float _strikeTimer;
        float _initialLenMm = 350f;
        Vector3 _electrodeBaseScale = Vector3.one;
        bool _scaleCaptured;

        public bool Arcing => State == ArcState.Stable;
        public bool Stuck  => State == ArcState.Stuck;
        public float RemainingFrac => _initialLenMm > 0f ? Mathf.Clamp01(RemainingMm / _initialLenMm) : 0f;

        void Awake() { CaptureElectrodeScale(); }

        void Start() { LoadNewElectrode(); }

        /// Guarda la escala original del cilindro del electrodo. Sin esto, al acortar la
        /// varilla se pisaba la escala con la fraccion (0..1) y el cilindro pasaba de
        /// 9 cm a 2 m (la escala 1 de un Cylinder de Unity son 2 metros de alto).
        void CaptureElectrodeScale()
        {
            if (_scaleCaptured || ElectrodeVisual == null) return;
            _electrodeBaseScale = ElectrodeVisual.localScale;
            _scaleCaptured = true;
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
        public Vector3 ElectrodeDir
        {
            get
            {
                if (Tip == null) return Vector3.up;
                return -Tip.TransformDirection(ElectrodeAxisLocal.normalized);
            }
        }

        /// Direccion en la que apunta la punta hacia la pieza.
        public Vector3 PointingDir
        {
            get
            {
                if (Tip == null) return Vector3.down;
                return Tip.TransformDirection(ElectrodeAxisLocal.normalized);
            }
        }

        /// Longitud de arco medida por raycast desde la punta hacia la pieza.
        public float MeasureArcLenMm()
        {
            if (Tip == null) { ArcLenMm = MaxArcRayMm; return ArcLenMm; }
            if (Physics.Raycast(Tip.position, PointingDir, out RaycastHit hit, MaxArcRayMm * 0.001f, WorkpieceMask))
                ArcLenMm = hit.distance * 1000f;
            else
                ArcLenMm = MaxArcRayMm;
            return ArcLenMm;
        }

        /// Maquina de estados del arco. Llamar cada FixedUpdate.
        public void Tick(float dt)
        {
            MeasureArcLenMm();
            Electrode e = Machine != null ? Machine.Current : null;
            bool consumed = RemainingMm <= StubLengthMm;
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
                    if (ArcLenMm >= MaxArcRayMm) break;            // demasiado lejos, no ceba
                    if (ArcLenMm < StickArcLenMm) { State = ArcState.Stuck; break; }
                    _strikeTimer = 0f;
                    State = ArcState.Striking;
                    break;

                case ArcState.Striking:
                    if (!SparkRequested) { State = ArcState.Off; break; }
                    if (ArcLenMm < StickArcLenMm) { State = ArcState.Stuck; break; }
                    if (ArcLenMm >= MaxArcRayMm)  { State = ArcState.Off;   break; }
                    _strikeTimer += dt;
                    if (_strikeTimer >= StrikeTimeSec) State = ArcState.Stable;
                    break;

                case ArcState.Stable:
                    if (!SparkRequested || consumed || tripped) { State = (consumed || tripped) ? ArcState.Blocked : ArcState.Off; break; }
                    if (ArcLenMm < StickArcLenMm) { State = ArcState.Stuck; break; }
                    if (ArcLenMm >= MaxArcRayMm)  { State = ArcState.Off;   break; } // se corta el arco
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

        /// Potencia depositada ahora (0 si no hay arco estable).
        public float CurrentPowerWatts()
        {
            if (!Arcing || Machine == null || Machine.Current == null) return 0f;
            return WeldingHeatModel.PowerWatts(Machine.Amperage, ArcLenMm, Machine.Current);
        }

        void UpdateElectrodeVisual()
        {
            if (ElectrodeVisual == null || _initialLenMm <= 0f) return;
            CaptureElectrodeScale();
            float f = Mathf.Clamp01(RemainingMm / _initialLenMm);
            // Se escala SOBRE la escala original, no se reemplaza.
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
            if (Tip == null) return;
            Gizmos.color = Arcing ? Color.yellow : Color.gray;
            Gizmos.DrawRay(Tip.position, PointingDir * (MaxArcRayMm * 0.001f));
        }
    }
}
