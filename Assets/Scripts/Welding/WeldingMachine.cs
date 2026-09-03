using UnityEngine;

namespace FrankaVR.Welding
{
    /// La maquina de soldar: regulacion de corriente, seleccion de electrodo y ciclo de
    /// trabajo termico. Modela el punto que planteo la catedra: "potencia de la soldadora
    /// aplicada segun regulacion dada".
    ///
    /// Ciclo de trabajo: una maquina al 60% puede soldar 6 de cada 10 minutos a corriente
    /// nominal. Aca se escala: soldar por encima del nominal calienta mas rapido, y si se
    /// llega al limite la maquina corta por termico hasta enfriarse.
    public class WeldingMachine : MonoBehaviour
    {
        [Header("Regulacion")]
        public float Amperage = 110f;
        public float AmpStep  = 5f;
        public float AmpMin   = 20f;
        public float AmpMax   = 220f;

        [Header("Ciclo de trabajo")]
        [Tooltip("Corriente a la que el ciclo de trabajo nominal es valido.")]
        public float RatedAmps = 140f;
        [Tooltip("Ciclo de trabajo nominal (0.6 = 60%).")]
        public float DutyCycle = 0.6f;
        [Tooltip("Duracion del ciclo de referencia, en segundos (norma: 10 min).")]
        public float CyclePeriodSec = 600f;
        [Tooltip("Cuanto mas rapido se enfria que se calienta.")]
        public float CoolRate = 0.35f;

        [Header("Estado (solo lectura)")]
        public float ThermalLoad;      // 0..1
        public bool  ThermalTripped;   // true = protegida, no da arco

        Electrode[] _lib;
        int _idx = 1;

        public Electrode Current => _lib[_idx];
        public int ElectrodeIndex => _idx;
        public int ElectrodeCount => _lib.Length;

        void Awake()
        {
            _lib = ElectrodeLibrary.Defaults;
            ClampAmp();
        }

        void EnsureLib() { if (_lib == null) _lib = ElectrodeLibrary.Defaults; }

        public void SelectElectrode(int index)
        {
            EnsureLib();
            _idx = Mathf.Clamp(index, 0, _lib.Length - 1);
        }

        public void SelectElectrodeByName(string name)
        {
            EnsureLib();
            for (int i = 0; i < _lib.Length; i++)
                if (_lib[i].Name == name) { _idx = i; return; }
        }

        public void CycleElectrode()
        {
            EnsureLib();
            _idx = (_idx + 1) % _lib.Length;
        }

        public void StepAmperage(int dir)
        {
            Amperage += dir * AmpStep;
            ClampAmp();
        }

        void ClampAmp() => Amperage = Mathf.Clamp(Amperage, AmpMin, AmpMax);

        /// True si la corriente actual esta dentro del rango del electrodo elegido.
        public bool AmpsInRange()
        {
            EnsureLib();
            return Amperage >= Current.AmpMin && Amperage <= Current.AmpMax;
        }

        /// Texto de diagnostico para el panel didactico.
        public string RegulationHint()
        {
            EnsureLib();
            if (ThermalTripped) return "PROTECCION TERMICA - esperar enfriamiento";
            if (Amperage < Current.AmpMin) return "Corriente baja para " + Current.Name + ": el electrodo se pega y no funde";
            if (Amperage > Current.AmpMax) return "Corriente alta para " + Current.Name + ": riesgo de perforar y de socavado";
            return "Regulacion correcta (" + Current.AmpMin.ToString("0") + "-" + Current.AmpMax.ToString("0") + " A)";
        }

        /// Llamar cada FixedUpdate. arcing = si esta saliendo arco ahora.
        public void UpdateThermal(bool arcing, float dt)
        {
            if (arcing && !ThermalTripped)
            {
                // Cuanto mas corriente respecto a la nominal, mas rapido carga.
                float loadRate = (Amperage / Mathf.Max(1f, RatedAmps)) / Mathf.Max(1f, DutyCycle * CyclePeriodSec);
                ThermalLoad += loadRate * dt;
            }
            else
            {
                ThermalLoad -= (CoolRate / Mathf.Max(1f, CyclePeriodSec)) * dt * 10f;
            }
            ThermalLoad = Mathf.Clamp01(ThermalLoad);

            if (ThermalLoad >= 1f) ThermalTripped = true;
            if (ThermalTripped && ThermalLoad <= 0.4f) ThermalTripped = false; // histeresis
        }

        public void ResetThermal() { ThermalLoad = 0f; ThermalTripped = false; }
    }
}
