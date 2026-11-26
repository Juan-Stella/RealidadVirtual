using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Mando en un segundo celular, sin hardware ni PC intermedia (adaptado del modo
    /// "Actuador" del HMI de la CAVE). La app (el celular del Cardboard, o el Editor en la PC)
    /// abre un servidor HTTP minimo en el puerto 8080:
    ///   GET  /       pagina del mando (Resources/MandoCelular.html)
    ///   POST /mando  cuerpo = una linea CTL igual a la del ESP32
    ///                (CTL,jx,jy,gatillo,acercar,retirar,ok,deshacer,borrar,altura,calibrar,vista,imu,qw,qx,qy,qz)
    /// Cada linea pasa por GlovePacketBridge.OnBleMessage, igual que los paquetes UDP del mando
    /// ESP32: HandControllerInput y HybridTeleopInput no distinguen de donde vienen.
    /// Un solo celular manda a la vez; otro queda rechazado mientras el primero siga enviando.
    /// </summary>
    public class PhoneControllerServer : MonoBehaviour
    {
        public int Port = 8080;
        public GlovePacketBridge Bridge;
        [Tooltip("Otro celular puede tomar el mando si el actual no manda nada en este tiempo.")]
        public float OwnerTimeoutSec = 2f;

        [Header("Estado (solo lectura)")]
        public bool Listening;
        public string Url = "";
        public int Packets;
        public string ClientAddress = "";
        public string LastError = "";

        /// Direccion para abrir en el celular del mando (vacia si el servidor no esta abierto).
        public static string CurrentUrl { get; private set; } = "";

        const int MaxConnections = 8;
        const int MaxBody = 512;

        TcpListener _listener;
        Thread _acceptThread;
        volatile bool _run;
        int _connections;
        readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        byte[] _page = Encoding.UTF8.GetBytes("<h1>FrankaVR</h1><p>Falta Resources/MandoCelular.html</p>");
        readonly object _ownerLock = new object();
        string _owner = "";
        DateTime _ownerSeen = DateTime.MinValue;
        volatile string _status = "Esperando";
        volatile string _client = "";
        float _nextUrlCheck;

        void OnEnable()
        {
            TextAsset page = Resources.Load<TextAsset>("MandoCelular");
            if (page != null) _page = Encoding.UTF8.GetBytes(page.text);
            Open();
        }

        void OnDisable() => Close();

        void Open()
        {
            Close();
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                _run = true;
                TcpListener listener = _listener;
                _acceptThread = new Thread(() => AcceptLoop(listener)) { IsBackground = true, Name = "FrankaVR mando celular" };
                _acceptThread.Start();
                Listening = true;
                LastError = "";
                RefreshUrl();
                Debug.Log("[Mando celular] Abrir en el otro celular: " + Url);
            }
            catch (Exception e)
            {
                Listening = false;
                LastError = e.Message;
                CurrentUrl = Url = "";
                Debug.LogWarning("[Mando celular] No se pudo abrir el puerto " + Port + ": " + e.Message);
            }
        }

        void Close()
        {
            _run = false;
            Listening = false;
            CurrentUrl = "";
            try { _listener?.Stop(); } catch (Exception) { }
            _listener = null;
            _acceptThread = null;
        }

        void Update()
        {
            if (Bridge == null) Bridge = GetComponent<GlovePacketBridge>();
            if (Bridge == null) Bridge = FindAnyObjectByType<GlovePacketBridge>();
            while (_queue.Count > 32) _queue.TryDequeue(out _);
            while (_queue.TryDequeue(out string line))
            {
                Packets++;
                if (Bridge != null) Bridge.OnBleMessage(line);
            }
            ClientAddress = _client;
            // La IP puede cambiar (otra red, hotspot): se vuelve a calcular cada 3 s.
            if (Listening && Time.unscaledTime >= _nextUrlCheck) { _nextUrlCheck = Time.unscaledTime + 3f; RefreshUrl(); }
            if (_hybrid == null && Time.unscaledTime >= _nextHybridSearch)
            {
                _nextHybridSearch = Time.unscaledTime + 1f;
                _hybrid = FindAnyObjectByType<HybridTeleopInput>();
            }
            _status = _hybrid != null ? "Entrada: " + _hybrid.ActiveMotionSource : "";
        }

        HybridTeleopInput _hybrid;
        float _nextHybridSearch;

        void RefreshUrl()
        {
            CurrentUrl = Url = "http://" + LocalAddress() + ":" + Port;
        }

        static string LocalAddress()
        {
            // Truco UDP: Connect no manda nada, solo elige la interfaz de salida.
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("8.8.8.8", 65530);
                    if (s.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address) && !ep.Address.Equals(IPAddress.Any))
                        return ep.Address.ToString();
                }
            }
            catch (Exception) { }
            // Sin ruta a internet (hotspot sin datos): primera IPv4 privada de las interfaces.
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress a = ua.Address;
                        if (a.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a)) continue;
                        byte[] b = a.GetAddressBytes();
                        if (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] < 32) || (b[0] == 192 && b[1] == 168))
                            return a.ToString();
                    }
                }
            }
            catch (Exception) { }
            return "IP-DEL-CELULAR";
        }

        void AcceptLoop(TcpListener listener)
        {
            while (_run && listener == _listener)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    if (Interlocked.Increment(ref _connections) > MaxConnections)
                    {
                        Interlocked.Decrement(ref _connections);
                        client.Close();
                        continue;
                    }
                    new Thread(() => Serve(client)) { IsBackground = true, Name = "FrankaVR mando celular (cliente)" }.Start();
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (!_run || listener != _listener) break; }
                catch (Exception) { }
            }
        }

        void Serve(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                string ip = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
                NetworkStream net = client.GetStream();
                // Navegadores abren conexiones por adelantado y tardan en usarlas: 2 minutos de espera.
                net.ReadTimeout = 120000;
                net.WriteTimeout = 3000;
                var input = new BufferedStream(net, 2048);
                while (_run)
                {
                    string requestLine = ReadLine(input);
                    if (requestLine == null) break;
                    if (requestLine.Length == 0) continue;
                    int contentLength = 0;
                    bool close = false;
                    string header;
                    while (!string.IsNullOrEmpty(header = ReadLine(input)))
                    {
                        int colon = header.IndexOf(':');
                        if (colon <= 0) continue;
                        string name = header.Substring(0, colon).Trim();
                        string value = header.Substring(colon + 1).Trim();
                        if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, out contentLength);
                        else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase) && value.Equals("close", StringComparison.OrdinalIgnoreCase)) close = true;
                    }
                    if (header == null) break;
                    if (contentLength < 0 || contentLength > MaxBody) { Respond(net, 413, "text/plain", "Demasiado grande", true); break; }
                    byte[] body = new byte[contentLength];
                    int read = 0;
                    while (read < contentLength)
                    {
                        int n = input.Read(body, read, contentLength - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read < contentLength) break;

                    string[] parts = requestLine.Split(' ');
                    string method = parts.Length > 0 ? parts[0] : "";
                    string path = parts.Length > 1 ? parts[1] : "/";
                    int q = path.IndexOf('?');
                    if (q >= 0) path = path.Substring(0, q);

                    if (method == "GET" && (path == "/" || path == "/index.html" || path == "/mando"))
                        Respond(net, 200, "text/html; charset=utf-8", _page, close);
                    else if (method == "POST" && path == "/mando")
                        Respond(net, 200, "application/json", HandlePacket(ip, Encoding.ASCII.GetString(body)), close);
                    else if (method == "GET" && path == "/estado")
                        Respond(net, 200, "application/json", StatusJson(true, ""), close);
                    else if (path == "/favicon.ico")
                        Respond(net, 204, "text/plain", "", close);
                    else
                        Respond(net, 404, "text/plain", "No existe", close);
                    if (close) break;
                }
            }
            catch (Exception) { /* celular que se fue o corte de Wi-Fi: se cierra la conexion */ }
            finally
            {
                try { client.Close(); } catch (Exception) { }
                Interlocked.Decrement(ref _connections);
            }
        }

        string HandlePacket(string ip, string body)
        {
            string line = body.Trim();
            if (!line.StartsWith("CTL,", StringComparison.Ordinal) || line.Length > 200 || line.IndexOf('\n') >= 0)
                return StatusJson(false, "paquete invalido");
            lock (_ownerLock)
            {
                DateTime now = DateTime.UtcNow;
                if (_owner.Length > 0 && _owner != ip && (now - _ownerSeen).TotalSeconds < OwnerTimeoutSec)
                    return StatusJson(false, "otro celular tiene el mando (" + _owner + ")");
                _owner = ip;
                _ownerSeen = now;
            }
            _client = ip;
            _queue.Enqueue(line);
            return StatusJson(true, "");
        }

        string StatusJson(bool ok, string reason)
        {
            return "{\"ok\":" + (ok ? "true" : "false") + ",\"estado\":\"" + Escape(_status) + "\",\"motivo\":\"" + Escape(reason) + "\"}";
        }

        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string ReadLine(Stream s)
        {
            var sb = new StringBuilder(64);
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) return sb.Length > 0 ? sb.ToString() : null;
                if (b == '\n') break;
                if (b != '\r') sb.Append((char)b);
                if (sb.Length > 4096) throw new IOException("Linea demasiado larga");
            }
            return sb.ToString();
        }

        static void Respond(NetworkStream net, int code, string type, string body, bool close) =>
            Respond(net, code, type, Encoding.UTF8.GetBytes(body), close);

        static void Respond(NetworkStream net, int code, string type, byte[] body, bool close)
        {
            string reason = code == 200 ? "OK" : code == 204 ? "No Content" : code == 404 ? "Not Found" : "Error";
            string head = "HTTP/1.1 " + code + " " + reason + "\r\n" +
                          "Content-Type: " + type + "\r\n" +
                          "Content-Length: " + body.Length + "\r\n" +
                          "Cache-Control: no-store\r\n" +
                          "Connection: " + (close ? "close" : "keep-alive") + "\r\n\r\n";
            byte[] h = Encoding.ASCII.GetBytes(head);
            net.Write(h, 0, h.Length);
            if (body.Length > 0) net.Write(body, 0, body.Length);
            net.Flush();
        }
    }
}
