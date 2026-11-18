using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Mando fisico ESP32: joystick KY-023, 5 botones y IMU GY-6500 opcional.
    /// Llega por GlovePacketBridge (paquetes CTL, normalmente por UDP Wi-Fi) y
    /// HybridTeleopInput lo mezcla con el teclado: usa el mismo control de punta,
    /// guia de costura, acercar/retirar y ensenar/ejecutar.
    ///
    /// CTL,jx,jy,gatillo,acercar,retirar,ok,deshacer,borrar,altura,calibrar,vista,imu,qw,qx,qy,qz
    ///   jx,jy  palanca -1..1 (derecha / adelante positivos, zona muerta ya aplicada)
    ///   0/1    gatillo y altura son niveles; el resto son pulsos de accion
    ///   imu    1 si el cuaternion es valido; qw..qz ya en ejes Unity
    /// </summary>
    public class HandControllerInput : MonoBehaviour
    {
        [Tooltip("Sin paquetes durante este tiempo se sueltan gatillo y palanca.")]
        public float TimeoutSec = 0.35f;
        [Range(0f, 0.5f)] public float StickDeadzone = 0.08f;
        [Tooltip("Curva de la palanca: >1 da mas precision cerca del centro.")]
        [Range(1f, 3f)] public float StickExpo = 1.6f;
        [Tooltip("Velocidad libre a fondo, en la escala del teclado (0.5 = ~100 mm/s con MoveSpeed 0.2).")]
        public float FullStickRate = 0.5f;
        [Tooltip("La IMU del mando orienta la torcha. Apagar si el mando no tiene IMU o molesta.")]
        public bool UseImu = true;
        [Range(0f, 30f)] public float OrientationResponse = 12f;

        [Header("Estado (solo lectura)")]
        public bool Connected;
        public Vector2 Stick;
        public bool Trigger, HeightMode, ImuValid;
        public float PacketAgeSec = 999f;
        public int Packets;

        float _lastPacket = -999f;
        bool _pApproach, _pRetreat, _pOk, _pUndo, _pClear, _pCalibrate, _pView;
        bool _approach, _retreat, _ok, _undo, _clear, _view, _calibrateRequest;
        Quaternion _imu = Quaternion.identity, _zero = Quaternion.identity, _heading = Quaternion.identity;
        Quaternion _viewYaw = Quaternion.identity, _base = Quaternion.identity, _smoothed = Quaternion.identity;
        Quaternion _lastOutput = Quaternion.identity;
        bool _calibrated;

        public bool Fresh => Packets > 0 && Time.unscaledTime - _lastPacket <= Mathf.Max(0.05f, TimeoutSec);

        public void OnPacket(float jx, float jy, bool trigger, bool approach, bool retreat, bool ok, bool undo,
                             bool clear, bool height, bool calibrate, bool view, bool imuValid, Quaternion imu)
        {
            _lastPacket = Time.unscaledTime;
            Packets++;
            Stick = new Vector2(Mathf.Clamp(jx, -1f, 1f), Mathf.Clamp(jy, -1f, 1f));
            Trigger = trigger;
            HeightMode = height;
            ImuValid = imuValid;
            if (imuValid) _imu = Normalize(imu);
            // Flancos: el firmware mantiene cada accion ~150 ms para que un paquete perdido no la borre.
            if (approach && !_pApproach) _approach = true;
            if (retreat && !_pRetreat) _retreat = true;
            if (ok && !_pOk) _ok = true;
            if (undo && !_pUndo) _undo = true;
            if (clear && !_pClear) _clear = true;
            if (view && !_pView) _view = true;
            if (calibrate && !_pCalibrate) { _calibrateRequest = true; CardboardRecenterProxy.TryRecenter(); }
            _pApproach = approach; _pRetreat = retreat; _pOk = ok; _pUndo = undo;
            _pClear = clear; _pCalibrate = calibrate; _pView = view;
        }

        void Update()
        {
            PacketAgeSec = Packets > 0 ? Time.unscaledTime - _lastPacket : 999f;
            Connected = Fresh;
            if (!Connected) { Stick = Vector2.zero; Trigger = HeightMode = false; }
            if (Consume(ref _view))
            {
                ViewpointManager views = FindAnyObjectByType<ViewpointManager>();
                if (views != null) views.Next();
            }
        }

        public bool ConsumeApproach() => Consume(ref _approach);
        public bool ConsumeRetreat() => Consume(ref _retreat);
        public bool ConsumeOk() => Consume(ref _ok);
        public bool ConsumeUndo() => Consume(ref _undo);
        public bool ConsumeClear() => Consume(ref _clear);

        /// Palanca como teclas WASD/QE: x derecha, y arriba, z adelante. Con la palanca
        /// apretada (altura) el eje adelante/atras sube y baja la torcha.
        public Vector3 StickRate()
        {
            if (!Connected) return Vector3.zero;
            float x = Shape(Stick.x), y = Shape(Stick.y);
            return HeightMode ? new Vector3(x, y, 0f) : new Vector3(x, 0f, y);
        }

        float Shape(float v)
        {
            float a = Mathf.Abs(v);
            if (a <= StickDeadzone) return 0f;
            float t = Mathf.Clamp01((a - StickDeadzone) / (1f - StickDeadzone));
            return Mathf.Sign(v) * Mathf.Pow(t, StickExpo);
        }

        /// Orientacion de la torcha a partir de la IMU. El mando se "engancha" a la
        /// orientacion actual de la torcha: al calibrar (OK largo) o cuando otra parte del
        /// programa cambio la torcha (acercar, teclado), no hay saltos.
        public bool TryOrientation(Quaternion torch, out Quaternion result)
        {
            result = torch;
            if (!UseImu || !Connected || !ImuValid) { _calibrated = false; return false; }
            if (!_calibrated || _calibrateRequest || Quaternion.Angle(torch, _lastOutput) > 0.5f) Calibrate(torch);

            Quaternion delta = Normalize(_imu * Quaternion.Inverse(_zero));        // giro del mando (marco IMU)
            Quaternion local = Quaternion.Inverse(_heading) * delta * _heading;    // adelante = frente del mando al calibrar
            Quaternion target = _viewYaw * local * Quaternion.Inverse(_viewYaw) * _base; // adelante = la vista al calibrar
            float k = OrientationResponse <= 0f ? 1f : 1f - Mathf.Exp(-OrientationResponse * Time.deltaTime);
            _smoothed = Quaternion.Slerp(_smoothed, target, k);
            _lastOutput = result = _smoothed;
            return true;
        }

        void Calibrate(Quaternion torch)
        {
            _calibrateRequest = false;
            _zero = _imu;
            _base = _smoothed = _lastOutput = torch;
            Vector3 f = Vector3.ProjectOnPlane(_zero * Vector3.forward, Vector3.up);
            if (f.sqrMagnitude < 0.09f) f = Vector3.ProjectOnPlane(_zero * Vector3.up, Vector3.up); // mando apuntando abajo
            _heading = f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
            Camera cam = Camera.main;
            Vector3 v = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            _viewYaw = v.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(v.normalized, Vector3.up) : Quaternion.identity;
            _calibrated = true;
        }

        static bool Consume(ref bool flag) { bool v = flag; flag = false; return v; }

        static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m < 1e-8f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
    }
}
