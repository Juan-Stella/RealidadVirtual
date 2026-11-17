using System;
using System.Globalization;
using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Puente simple entre un transporte BLE Android y BleGloveInput.
    ///
    /// El plugin/servicio Android solo tiene que llamar por UnitySendMessage:
    ///   gameObject = objeto que tenga este componente
    ///   metodo     = OnBleMessage
    ///
    /// Formatos CSV soportados:
    ///   IMU,qw,qx,qy,qz,clutch
    ///   BTN,spark,ampPlus,ampMinus,cycle,replace,helmet,recenter,nextView,calibrate
    ///
    /// Todos los booleanos pueden ser 0/1 o true/false.
    /// </summary>
    public class GlovePacketBridge : MonoBehaviour
    {
        public BleGloveInput Glove;
        public ViewpointManager Viewpoints;
        public bool LogPackets = false;

        bool _prevRecenter, _prevNextView, _prevCalibrate;

        void Awake()
        {
            if (Glove == null) Glove = FindObjectOfType<BleGloveInput>();
            if (Viewpoints == null) Viewpoints = FindObjectOfType<ViewpointManager>();
        }

        public void OnBleMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (LogPackets) Debug.Log("[BLE] " + message);

            try
            {
                string[] p = message.Trim().Split(',');
                if (p.Length == 0) return;

                string kind = p[0].Trim().ToUpperInvariant();
                if (kind == "IMU") ParseImu(p);
                else if (kind == "BTN") ParseButtons(p);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BLE] Paquete invalido: '" + message + "' -> " + ex.Message);
            }
        }

        void ParseImu(string[] p)
        {
            if (Glove == null || p.Length < 6) return;

            // El protocolo manda qw primero por comodidad del firmware.
            float qw = F(p[1]);
            float qx = F(p[2]);
            float qy = F(p[3]);
            float qz = F(p[4]);
            bool clutch = B(p[5]);

            Glove.OnPrimaryGlovePacket(new Quaternion(qx, qy, qz, qw), clutch);
        }

        void ParseButtons(string[] p)
        {
            if (Glove == null || p.Length < 8) return;

            bool spark   = B(p[1]);
            bool ampPlus = B(p[2]);
            bool ampMinus= B(p[3]);
            bool cycle   = B(p[4]);
            bool replace = B(p[5]);
            bool helmet  = B(p[6]);
            bool reset   = B(p[7]);

            bool recenter = p.Length > 8 && B(p[8]);
            bool nextView = p.Length > 9 && B(p[9]);
            bool calibrate= p.Length > 10 && B(p[10]);

            Glove.OnSecondaryButtons(spark, ampPlus, ampMinus, cycle, replace, helmet, reset);

            if (recenter && !_prevRecenter) CardboardRecenterProxy.TryRecenter();
            if (nextView && !_prevNextView && Viewpoints != null) Viewpoints.Next();
            if (calibrate && !_prevCalibrate) Glove.CalibrateOrientationToCurrent();

            _prevRecenter = recenter;
            _prevNextView = nextView;
            _prevCalibrate = calibrate;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static bool B(string s)
        {
            s = s.Trim();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
