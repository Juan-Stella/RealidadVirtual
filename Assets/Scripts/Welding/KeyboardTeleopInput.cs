using UnityEngine;

namespace FrankaVR.Teleop
{
    /// Fuente de teleoperacion por teclado, para probar sin guante.
    /// Rotacion: I/K = pitch, J/L = yaw, U/O = roll.
    /// Traslacion: W/S = adelante/atras, A/D = izq/der, Q/E = abajo/arriba.
    /// Chispa: Space (mantener). Amperaje: [ y ]. Electrodo en maquina: Tab.
    /// Reponer varilla: R. Careta arriba/abajo: H.
    public class KeyboardTeleopInput : MonoBehaviour, ITeleopInput
    {
        [Header("Sensibilidad")]
        public float RotSpeedDeg = 60f; // grados/seg
        public float TransRate   = 1f;  // magnitud de velocidad (0..1)

        Quaternion _accum = Quaternion.identity;
        bool _initialized;
        bool _prevCycle;

        public TeleopCommand Poll(Quaternion currentToolOrientation)
        {
            if (!_initialized) { _accum = currentToolOrientation; _initialized = true; }

            float dt = Time.deltaTime;
            float pitch = (Key(KeyCode.I) - Key(KeyCode.K)) * RotSpeedDeg * dt;
            float yaw   = (Key(KeyCode.L) - Key(KeyCode.J)) * RotSpeedDeg * dt;
            float roll  = (Key(KeyCode.O) - Key(KeyCode.U)) * RotSpeedDeg * dt;
            _accum = _accum * Quaternion.Euler(pitch, yaw, roll);

            Vector3 rate = new Vector3(
                Key(KeyCode.D) - Key(KeyCode.A),
                Key(KeyCode.E) - Key(KeyCode.Q),
                Key(KeyCode.W) - Key(KeyCode.S)) * TransRate;

            bool cycle = Input.GetKey(KeyCode.Tab);
            bool cycleEdge = cycle && !_prevCycle;
            _prevCycle = cycle;

            int amp = (Input.GetKeyDown(KeyCode.RightBracket) ? 1 : 0)
                    + (Input.GetKeyDown(KeyCode.LeftBracket)  ? -1 : 0);

            return new TeleopCommand
            {
                Orientation     = _accum,
                TranslationRate = rate,
                SparkHeld       = Input.GetKey(KeyCode.Space),
                AmperageStep    = amp,
                CycleElectrode   = cycleEdge,
                ReplaceElectrode = Input.GetKeyDown(KeyCode.R),
                ToggleHelmet     = Input.GetKeyDown(KeyCode.H),
                MoveClutch       = true // teclado: clutch siempre activo
            };
        }

        static float Key(KeyCode k) => Input.GetKey(k) ? 1f : 0f;
    }
}
