using UnityEngine;

namespace FrankaVR.Teleop
{
    /// Fuente de teleoperacion por teclado, para probar sin guante.
    /// Rotacion: I/K = pitch, J/L = yaw, U/O = roll.
    /// Traslacion: W/S = adelante/atras, A/D = izq/der, Q/E = abajo/arriba.
    /// Chispa: Space (mantener). Amperaje: [ y ]. Electrodo en maquina: Tab.
    /// Reponer varilla: R. Careta arriba/abajo: H. Enderezar electrodo: V.
    ///
    /// La orientacion se acumula en MARCO DE MUNDO y arranca en identidad, de modo que
    /// Orientation * Vector3.down da exactamente "hacia abajo" en el primer frame y las
    /// teclas inclinan sobre ejes fijos del mundo. Antes arrancaba copiando la rotacion
    /// de la punta y acumulaba en marco local: el controlador terminaba conjugando por esa
    /// rotacion inicial (R0 * D * R0^-1), asi que los ejes de control quedaban mezclados
    /// segun la pose en la que hubiera cargado el brazo. Por eso costaba tanto orientar.
    public class KeyboardTeleopInput : MonoBehaviour, ITeleopInput
    {
        [Header("Sensibilidad")]
        public float RotSpeedDeg = 60f; // grados/seg
        public float TransRate   = 1f;  // magnitud de velocidad (0..1)
        public float FineTranslationMultiplier = 0.033f; // ~10 mm/s con MoveSpeed 0.3
        public float FineRotationMultiplier = 0.15f;
        [Header("Aceleracion al mantener la tecla")]
        public float BaseTranslation = 0.2f;    // ~60 mm/s al empezar
        public float CruiseTranslation = 0.5f;  // ~150 mm/s sostenido o con Shift
        public float RampDelay = 0.35f, RampTime = 0.8f;
        public float RotationMultiplier = 0.5f; // ~30 grados/s

        /// Ejes de inclinacion en mundo. Los fija el controlador: camara o costura.
        [HideInInspector] public Vector3 TiltRight = Vector3.right, TiltForward = Vector3.forward;
        public bool Fine { get; private set; }
        public bool Fast { get; private set; }

        Quaternion _accum = Quaternion.identity;
        bool _prevCycle;
        float _held;
        public void SetOrientation(Quaternion orientation) => _accum = orientation;

        public TeleopCommand Poll(Quaternion currentToolOrientation)
        {
            bool fine = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Fine = fine; Fast = fast;
            Vector3 rate = new Vector3(
                Key(KeyCode.D) - Key(KeyCode.A),
                Key(KeyCode.E) - Key(KeyCode.Q),
                Key(KeyCode.W) - Key(KeyCode.S));
            // Arranca a ~60 mm/s para ubicar con precision y acelera hasta ~150 mm/s
            // si se mantiene la tecla. Ctrl ~10 mm/s. Con arco sobre la costura el
            // controlador usa velocidades de soldadura propias.
            _held = rate.sqrMagnitude > 0f ? _held + Time.deltaTime : 0f;
            float ramp = Mathf.Clamp01((_held - RampDelay) / RampTime);
            float precision = fine ? FineTranslationMultiplier : Input.GetKey(KeyCode.Space) ? 0.02f :
                fast ? CruiseTranslation : Mathf.Lerp(BaseTranslation, CruiseTranslation, ramp * ramp);

            // Inclinacion sobre ejes de la vista (o de la costura con arco):
            // I/K mango adelante/atras, J/L mango izquierda/derecha, U/O girar sobre la vertical.
            float step = Mathf.Min(Time.deltaTime, 0.05f) * RotSpeedDeg * (fine ? FineRotationMultiplier : RotationMultiplier);
            float tiltForward = (Key(KeyCode.I) - Key(KeyCode.K)) * step;
            float tiltSide    = (Key(KeyCode.J) - Key(KeyCode.L)) * step;
            float swivel      = (Key(KeyCode.O) - Key(KeyCode.U)) * step;
            // PRE-multiplicar = rotar sobre ejes del MUNDO (post-multiplicar seria local).
            _accum = Quaternion.AngleAxis(tiltForward, TiltRight) * Quaternion.AngleAxis(tiltSide, TiltForward) *
                     Quaternion.AngleAxis(swivel, Vector3.up) * _accum;
            if (Input.GetKeyDown(KeyCode.V)) _accum = Quaternion.identity; // enderezar

            rate = Vector3.ClampMagnitude(rate, 1f) * (TransRate * precision);

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
                ResetOrientation = Input.GetKeyDown(KeyCode.V),
                MoveClutch       = true // teclado: clutch siempre activo
            };
        }

        static float Key(KeyCode k) => Input.GetKey(k) ? 1f : 0f;
    }
}
