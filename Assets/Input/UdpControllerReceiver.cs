using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Transporte WiFi/UDP para el mando ESP32 (Firmware/FrankaVR_Mando).
    ///
    /// Recibe datagramas con lineas CSV (IMU/BTN/JOY) en un hilo aparte y las entrega a
    /// GlovePacketBridge.OnBleMessage en el hilo principal, igual que lo haria un plugin BLE.
    /// Cada segundo envia "HELLO" al mando para que este pase de broadcast a unicast.
    ///
    /// Funciona en Windows y Android sin plugins nativos. En Windows, permitir el acceso
    /// cuando el firewall lo pregunte la primera vez.
    /// </summary>
    public class UdpControllerReceiver : MonoBehaviour
    {
        public GlovePacketBridge Bridge;

        [Header("Red")]
        [Tooltip("Puerto local donde Unity escucha los paquetes del mando.")]
        public int ListenPort = 4211;
        [Tooltip("IP del ESP32. 192.168.4.1 cuando el mando crea su propia red (modo AP).")]
        public string ControllerAddress = "192.168.4.1";
        public int ControllerPort = 4210;
        public float HelloIntervalSec = 1f;
        [Tooltip("Tope de lineas procesadas por frame; el resto queda para el siguiente.")]
        public int MaxLinesPerFrame = 64;

        [Header("Estado (solo lectura)")]
        public bool Listening;
        public int PacketsReceived;
        public float LastPacketAgeSec = 999f;
        public string LastSender = "";

        UdpClient _udp;
        Thread _thread;
        volatile bool _running;
        volatile string _sender = "";
        int _packets;
        readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        IPEndPoint _controller;
        float _nextHello;
        float _lastPacketTime = -999f;
        static readonly byte[] Hello = Encoding.ASCII.GetBytes("HELLO");

        void Awake()
        {
            if (Bridge == null) Bridge = GetComponent<GlovePacketBridge>();
            if (Bridge == null) Bridge = FindObjectOfType<GlovePacketBridge>();
        }

        void OnEnable()
        {
            try
            {
                _udp = new UdpClient(ListenPort) { EnableBroadcast = true };
            }
            catch (SocketException ex)
            {
                Debug.LogWarning("[Mando UDP] No se pudo abrir el puerto " + ListenPort + ": " + ex.Message);
                _udp = null;
                return;
            }

            _controller = IPAddress.TryParse(ControllerAddress, out IPAddress ip)
                ? new IPEndPoint(ip, ControllerPort)
                : null;

            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "FrankaVR UDP" };
            _thread.Start();
            Listening = true;
            _nextHello = 0f;
        }

        void OnDisable()
        {
            _running = false;
            Listening = false;
            try { _udp?.Close(); } catch (Exception) { }
            if (_thread != null && _thread.IsAlive) _thread.Join(200);
            _thread = null;
            _udp = null;
        }

        void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp.Receive(ref from);
                    string text = Encoding.ASCII.GetString(data);
                    foreach (string raw in text.Split('\n'))
                    {
                        string line = raw.Trim();
                        if (line.Length > 0) _lines.Enqueue(line);
                    }
                    _sender = from.Address.ToString();
                    Interlocked.Increment(ref _packets);
                }
                catch (SocketException)
                {
                    // Windows devuelve ConnectionReset si un HELLO no tuvo destino: se ignora.
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        void Update()
        {
            int processed = 0;
            while (processed < MaxLinesPerFrame && _lines.TryDequeue(out string line))
            {
                processed++;
                _lastPacketTime = Time.unscaledTime;
                if (Bridge != null) Bridge.OnBleMessage(line);
            }

            PacketsReceived = _packets;
            LastSender = _sender;
            LastPacketAgeSec = _lastPacketTime < 0f ? 999f : Time.unscaledTime - _lastPacketTime;

            if (_udp != null && _controller != null && Time.unscaledTime >= _nextHello)
            {
                _nextHello = Time.unscaledTime + Mathf.Max(0.2f, HelloIntervalSec);
                try { _udp.Send(Hello, Hello.Length, _controller); }
                catch (Exception) { /* sin red hacia el mando: se reintenta */ }
            }
        }
    }
}
