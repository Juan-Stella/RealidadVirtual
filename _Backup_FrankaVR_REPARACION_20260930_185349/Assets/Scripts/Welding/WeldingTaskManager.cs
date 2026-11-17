using UnityEngine;

namespace FrankaVR.Welding
{
    public enum SessionState { Idle, Briefing, Running, Finished }

    /// Orquesta el ejercicio: carga la orden de trabajo, integra el aporte termico,
    /// mide las metricas de desempeño y calcula la nota final.
    public class WeldingTaskManager : MonoBehaviour
    {
        [Header("Referencias")]
        public WeldSeam                Seam;
        public WeldingTorch            Torch;
        public WeldingMachine          Machine;
        public WeldingTeleopController Teleop;
        public Transform               TipStart;    // pose inicial del TCP al reiniciar

        [Header("Orden de trabajo")]
        public string WorkOrderId = "N1";
        public WorkOrder Order { get; private set; }

        [Header("Estado")]
        public SessionState State = SessionState.Idle;
        public float Elapsed;

        [Header("Resultado (solo lectura)")]
        public int GoodPoints, ColdPoints, BurnPoints, UndercutPoints, PorosityPoints, UntouchedPoints;
        public float FinalScore;

        [Header("Metricas de proceso")]
        public float ArcOnTime;        // segundos con arco estable
        public float MeanArcLenMm;     // longitud de arco promedio con arco
        public float ArcLenStdMm;      // desvio: mide la CONSTANCIA del operador
        public int   StickCount;       // veces que pego el electrodo
        public int   ArcBreaks;        // veces que se corto el arco
        public int   ElectrodesUsed = 1;

        float _arcLenSum, _arcLenSqSum;
        int   _arcSamples;
        bool  _prevArcing;
        bool  _prevStuck;

        void Awake() { LoadWorkOrder(WorkOrderId); }

        public void LoadWorkOrder(string id)
        {
            Order = WorkOrder.ById(id);
            WorkOrderId = Order.Id;
            if (Seam != null) Seam.ApplyWorkOrder(Order);
            if (Machine != null)
            {
                Machine.SelectElectrodeByName(Order.RecommendedElectrode);
                Machine.Amperage = Order.RecommendedAmps;
                Machine.ResetThermal();
            }
            if (Torch != null) Torch.LoadNewElectrode();
            State = SessionState.Briefing;
        }

        public void StartTask()
        {
            ResetTask();
            State = SessionState.Running;
        }

        /// Reset limpio: no debe quedar estado residual de la ronda anterior.
        public void ResetTask()
        {
            State = SessionState.Idle;
            Elapsed = 0f;
            GoodPoints = ColdPoints = BurnPoints = UndercutPoints = PorosityPoints = UntouchedPoints = 0;
            FinalScore = 0f;
            ArcOnTime = 0f; MeanArcLenMm = 0f; ArcLenStdMm = 0f;
            StickCount = 0; ArcBreaks = 0; ElectrodesUsed = 1;
            _arcLenSum = _arcLenSqSum = 0f; _arcSamples = 0;
            _prevArcing = false; _prevStuck = false;

            if (Seam)    Seam.ResetSeam();
            if (Torch)   { Torch.SetSparkRequested(false); Torch.LoadNewElectrode(); }
            if (Machine) Machine.ResetThermal();
            if (Teleop != null && TipStart != null) Teleop.ResetTo(TipStart.position);
        }

        void FixedUpdate()
        {
            if (Torch == null) return;
            float dt = Time.fixedDeltaTime;

            Torch.Tick(dt); // maquina de estados del arco + consumo + termico

            if (State != SessionState.Running) return;
            Elapsed += dt;

            bool arcing = Torch.Arcing;

            // Metricas de constancia: media y desvio de la longitud de arco.
            if (arcing)
            {
                ArcOnTime += dt;
                _arcLenSum   += Torch.ArcLenMm;
                _arcLenSqSum += Torch.ArcLenMm * Torch.ArcLenMm;
                _arcSamples++;
            }
            if (_prevArcing && !arcing && !Torch.Stuck) ArcBreaks++;
            if (Torch.Stuck && !_prevStuck) StickCount++;
            _prevArcing = arcing;
            _prevStuck  = Torch.Stuck;

            // Deposicion en la costura.
            if (arcing && Seam != null)
            {
                float watts = Torch.CurrentPowerWatts();
                if (watts > 0f)
                    Seam.Deposit(Torch.ArcOriginPosition, Torch.ElectrodeDir, watts, Torch.ArcQuality, dt);
            }

            if (Order != null && Order.TimeLimitSec > 0f && Elapsed >= Order.TimeLimitSec)
                FinishTask();
        }

        /// Reponer varilla cuando se consumio.
        public void ReplaceElectrode()
        {
            if (Torch == null) return;
            Torch.LoadNewElectrode();
            ElectrodesUsed++;
        }

        public void FinishTask()
        {
            State = SessionState.Finished;
            if (Torch) Torch.SetSparkRequested(false);

            if (_arcSamples > 0)
            {
                MeanArcLenMm = _arcLenSum / _arcSamples;
                float var = _arcLenSqSum / _arcSamples - MeanArcLenMm * MeanArcLenMm;
                ArcLenStdMm = Mathf.Sqrt(Mathf.Max(0f, var));
            }

            if (Seam) Seam.Tally(out GoodPoints, out ColdPoints, out BurnPoints,
                                 out UndercutPoints, out PorosityPoints, out UntouchedPoints);
            FinalScore = Score();
        }

        /// Nota 0..100. Premia cobertura correcta y penaliza cada defecto con peso distinto,
        /// mas una penalizacion por falta de constancia en la longitud de arco.
        public float Score()
        {
            int total = Seam ? Seam.Count : 0;
            if (total == 0) return 0f;

            float good = GoodPoints / (float)total;
            float pen  = (BurnPoints     * 3.0f
                        + UndercutPoints * 1.5f
                        + PorosityPoints * 1.2f
                        + ColdPoints     * 1.0f
                        + UntouchedPoints* 1.0f) / total;

            float raw = Mathf.Clamp01(good - pen * 0.5f);

            // Constancia: desvio de arco por encima de ~1.5 mm empieza a descontar.
            float steady = Mathf.Clamp01(1f - Mathf.Max(0f, ArcLenStdMm - 1.5f) / 3f);
            float stickPen = Mathf.Clamp01(StickCount * 0.04f);

            return Mathf.Clamp01(raw * (0.75f + 0.25f * steady) - stickPen) * 100f;
        }

        public bool Passed() => Order != null && FinalScore >= Order.MinScoreToPass;

        /// Informe de texto para el panel de resultados.
        public string ReportText()
        {
            if (Seam == null || Order == null) return "";
            int total = Seam.Count;
            string s = "ORDEN " + Order.Id + " - " + Order.Title + "\n";
            s += "Nota: " + FinalScore.ToString("0") + " / 100   (" + (Passed() ? "APROBADO" : "NO APROBADO") + ")\n";
            s += "Tiempo: " + Elapsed.ToString("0.0") + " s   Arco encendido: " + ArcOnTime.ToString("0.0") + " s\n";
            s += "Cordon correcto: " + GoodPoints + "/" + total + "\n";
            s += "Falta de fusion: " + ColdPoints + "   Perforaciones: " + BurnPoints + "\n";
            s += "Socavado: " + UndercutPoints + "   Porosidad: " + PorosityPoints + "\n";
            s += "Sin soldar: " + UntouchedPoints + "\n";
            s += "Long. de arco media: " + MeanArcLenMm.ToString("0.0") + " mm (desvio "
               + ArcLenStdMm.ToString("0.0") + " mm)\n";
            s += "Pegadas: " + StickCount + "   Cortes de arco: " + ArcBreaks
               + "   Electrodos: " + ElectrodesUsed;
            return s;
        }
    }
}
