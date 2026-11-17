using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Paneles informativos en el mundo 3D.
    /// Primera mejora de entorno: informacion mas clara para operar la celda sin abrir la consola.
    /// </summary>
    public class WeldingHUD : MonoBehaviour
    {
        [Header("Referencias")]
        public WeldingTaskManager Task;
        public WeldingMachine     Machine;
        public WeldingTorch       Torch;

        [Header("Paneles")]
        public TextMesh MachinePanel;
        public TextMesh BriefPanel;
        public TextMesh ResultPanel;

        [Header("Colores")]
        public Color OkColor   = new Color(0.55f, 0.95f, 0.55f);
        public Color WarnColor = new Color(1f, 0.75f, 0.25f);
        public Color BadColor  = new Color(1f, 0.4f, 0.35f);

        [Header("Opciones")]
        public bool ShowControlsHint = true;

        float _refresh;

        void Update()
        {
            _refresh += Time.deltaTime;
            if (_refresh < 0.1f) return;
            _refresh = 0f;

            UpdateMachinePanel();
            UpdateBriefPanel();
            UpdateResultPanel();
        }

        void UpdateMachinePanel()
        {
            if (MachinePanel == null || Machine == null) return;
            Electrode e = Machine.Current;

            string s = "MAQUINA DE SOLDAR\n";
            s += "Electrodo:  " + e.Name + "\n";
            s += "Rango:      " + e.AmpMin.ToString("0") + " - " + e.AmpMax.ToString("0") + " A\n";
            s += "Corriente:  " + Machine.Amperage.ToString("0") + " A\n";
            s += "Termico:    " + (Machine.ThermalLoad * 100f).ToString("0") + " %\n";

            if (Torch != null)
            {
                s += "\nARCO\n";
                s += "Estado:     " + Torch.StateLabel() + "\n";
                string arcText = Torch.HasArcSurface
                    ? Torch.ArcLenMm.ToString("0.0") + " mm"
                    : "> " + Torch.MeasureRangeMm.ToString("0") + " mm";
                s += "Long. arco: " + arcText + "   (ideal " + e.ArcLenIdealMm.ToString("0.0") + ")\n";
                s += "Varilla:    " + Torch.RemainingMm.ToString("0") + " mm  (" + (Torch.RemainingFrac * 100f).ToString("0") + " %)\n";
                s += "Limite arco:" + Torch.MaxArcLenMm.ToString("0") + " mm\n";
            }

            s += "\n" + Machine.RegulationHint();
            if (ShowControlsHint)
            {
                s += "\n\nControles:\n";
                s += "Q/E bajar-subir | Space arco | R reponer | Tab cambiar electrodo\n";
                s += "[ / ] corriente | H careta | V enderezar";
            }

            MachinePanel.text = s;

            if (Machine.ThermalTripped || (Torch != null && Torch.Stuck)) MachinePanel.color = BadColor;
            else if (!Machine.AmpsInRange())                              MachinePanel.color = WarnColor;
            else                                                          MachinePanel.color = OkColor;
        }

        void UpdateBriefPanel()
        {
            if (BriefPanel == null || Task == null || Task.Order == null) return;
            WorkOrder o = Task.Order;

            string s = "ORDEN " + o.Id + "\n" + o.Title + "\n\n";
            s += o.Brief + "\n\n";
            s += "NORMA\n";
            s += "Espesor:           " + o.ThicknessMm.ToString("0.0") + " mm\n";
            s += "Angulo de avance:  " + o.IdealTravelAngle.ToString("0") + " deg\n";
            s += "Angulo de trabajo: " + o.IdealWorkAngle.ToString("0") + " deg\n";
            s += "Tolerancia:        +/- " + o.AngleToleranceDeg.ToString("0") + " deg\n";
            s += "Recomendado:       " + o.RecommendedElectrode + " a " + o.RecommendedAmps.ToString("0") + " A\n";
            s += "Aprueba con:       " + o.MinScoreToPass.ToString("0") + " / 100\n";

            if (Task.State == SessionState.Running)
            {
                float left = Mathf.Max(0f, o.TimeLimitSec - Task.Elapsed);
                s += "\nTiempo restante: " + left.ToString("0") + " s";
            }
            BriefPanel.text = s;
        }

        void UpdateResultPanel()
        {
            if (ResultPanel == null || Task == null) return;
            if (Task.State != SessionState.Finished)
            {
                ResultPanel.text = "";
                return;
            }
            ResultPanel.text = Task.ReportText();
            ResultPanel.color = Task.Passed() ? OkColor : BadColor;
        }
    }
}
