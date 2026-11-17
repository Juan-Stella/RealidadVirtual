// =====================================================================================
//  FrankaVR - Mando ESP32
//  Placa: NodeMCU ESP32 (ESP32-WROOM-32, USB-C)  ->  Arduino IDE: "ESP32 Dev Module"
//
//  Componentes:
//    - Joystick KY-023 ............ mueve la punta del electrodo (traslacion)
//    - 5 pulsadores Big Key 12x12 .. chispa, amperaje +/-, electrodo, careta
//    - IMU GY-6500 (MPU-6500) ...... orientacion del mango -> inclinacion del electrodo
//
//  Transporte: WiFi UDP con lineas CSV, el mismo protocolo que ya parsea Unity en
//  Assets/Input/GlovePacketBridge.cs:
//    IMU,qw,qx,qy,qz,clutch       (cuaternion ya convertido a ejes de Unity)
//    BTN,spark,ampPlus,ampMinus,cycle,replace,helmet,resetOrientation
//    JOY,x,y,vertical             (x,y en -1..1; vertical=1 mientras se aprieta el joystick)
//
//  No usa librerias externas: solo Wire, WiFi y WiFiUdp del core ESP32.
//  Ver MANDO_ESP32.md en la raiz del repositorio para cableado y puesta en marcha.
// =====================================================================================

#include <Arduino.h>
#include <Wire.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <math.h>

// ------------------------------- Configuracion WiFi ----------------------------------
// 1: el ESP32 crea su propia red (recomendado, no depende del router de la facultad).
// 0: el ESP32 se conecta a una red existente (STA_SSID / STA_PASS).
#define WIFI_MODO_AP 1

const char* AP_SSID  = "FrankaVR-Mando";
const char* AP_PASS  = "frankavr26";       // minimo 8 caracteres
const char* STA_SSID = "NombreDeTuRed";
const char* STA_PASS = "ClaveDeTuRed";

const uint16_t PUERTO_ESP32 = 4210;        // el ESP32 escucha "HELLO" de Unity aca
const uint16_t PUERTO_UNITY = 4211;        // Unity escucha los paquetes aca
const uint32_t HELLO_VALIDO_MS = 5000;     // sin HELLO en este tiempo -> se envia por broadcast

// ------------------------------- Pines ------------------------------------------------
// Pulsadores: un pin a GPIO, el otro a GND (se usa INPUT_PULLUP, apretado = LOW).
const int PIN_BTN_CHISPA    = 25;  // mantener: arco encendido
const int PIN_BTN_AMP_MAS   = 27;  // toque: +amperaje
const int PIN_BTN_AMP_MENOS = 14;  // toque: -amperaje
const int PIN_BTN_ELECTRODO = 33;  // toque: reponer varilla | largo: cambiar tipo de electrodo
const int PIN_BTN_CARETA    = 26;  // toque: subir/bajar careta | largo: enderezar y recalibrar IMU

// Joystick KY-023 alimentado a 3V3 (NO a 5V: los ADC del ESP32 admiten hasta 3.3 V).
// GPIO34/35 son ADC1: siguen funcionando con WiFi activo (ADC2 no).
const int PIN_JOY_X  = 34;
const int PIN_JOY_Y  = 35;
const int PIN_JOY_SW = 23;

// IMU por I2C.
const int PIN_SDA = 21;
const int PIN_SCL = 22;

const int PIN_LED = 2;             // LED azul de la placa

// ------------------------------- Ajustes ---------------------------------------------
const uint32_t PERIODO_IMU_US    = 5000;   // 200 Hz lectura + fusion
const uint32_t PERIODO_ENVIO_MS  = 20;     // 50 Hz hacia Unity (timeouts de Unity: 350 ms)
const uint32_t DIVISOR_SERIAL    = 5;      // por Serial solo 1 de cada 5 envios (10 Hz, depuracion)
const uint32_t REBOTE_MS         = 20;
const uint32_t PULSACION_LARGA_MS = 700;
const uint32_t DURACION_PULSO_MS = 120;    // un toque se mantiene en 1 durante ~6 paquetes

const float JOY_ZONA_MUERTA = 0.12f;
const bool  JOY_INVERTIR_X  = false;
const bool  JOY_INVERTIR_Y  = true;        // en el KY-023 "arriba" suele leer valores bajos

const float MADGWICK_BETA_INICIAL = 2.0f;  // converge rapido al arrancar
const float MADGWICK_BETA         = 0.06f; // regimen: menos ruido, algo mas de deriva
const uint32_t CONVERGENCIA_MS    = 2000;

// ------------------------------- MPU-6500 ---------------------------------------------
const uint8_t MPU_ADDR_A = 0x68;           // AD0 a GND o sin conectar
const uint8_t MPU_ADDR_B = 0x69;           // AD0 a 3V3
const uint8_t REG_SMPLRT_DIV   = 0x19;
const uint8_t REG_CONFIG       = 0x1A;
const uint8_t REG_GYRO_CONFIG  = 0x1B;
const uint8_t REG_ACCEL_CONFIG = 0x1C;
const uint8_t REG_ACCEL_CONFIG2 = 0x1D;
const uint8_t REG_ACCEL_XOUT_H = 0x3B;
const uint8_t REG_PWR_MGMT_1   = 0x6B;
const uint8_t REG_WHO_AM_I     = 0x75;

const float ESCALA_GYRO  = 65.5f;          // LSB por grado/s con rango +-500 dps
const float GRAD_A_RAD   = 0.01745329252f;

uint8_t mpuAddr = 0;
bool imuOk = false;
float gyroBias[3] = {0, 0, 0};
float q[4] = {1, 0, 0, 0};                 // w, x, y, z en marco del sensor (Z arriba)
uint32_t tArranqueFusion = 0;

// ------------------------------- Estado ----------------------------------------------
struct Boton {
  int pin;
  bool estable;        // true = apretado (ya filtrado)
  bool lectura;
  uint32_t tCambio;
  uint32_t tPresion;
  bool largoDisparado;
};

Boton bChispa    = {PIN_BTN_CHISPA,    false, false, 0, 0, false};
Boton bAmpMas    = {PIN_BTN_AMP_MAS,   false, false, 0, 0, false};
Boton bAmpMenos  = {PIN_BTN_AMP_MENOS, false, false, 0, 0, false};
Boton bElectrodo = {PIN_BTN_ELECTRODO, false, false, 0, 0, false};
Boton bCareta    = {PIN_BTN_CARETA,    false, false, 0, 0, false};
Boton bJoySw     = {PIN_JOY_SW,        false, false, 0, 0, false};

// Pulsos: Unity detecta flancos de subida, asi que un toque se envia como 1 durante un rato.
uint32_t pulsoReponerHasta = 0;
uint32_t pulsoCambiarHasta = 0;
uint32_t pulsoCaretaHasta  = 0;
uint32_t pulsoResetHasta   = 0;

int joyCentroX = 2048, joyCentroY = 2048;

WiFiUDP udp;
IPAddress destinoUnicast;
uint16_t puertoUnicast = PUERTO_UNITY;
uint32_t tUltimoHello = 0;
bool hayHello = false;
uint32_t contadorEnvios = 0;

// =====================================================================================
//  IMU
// =====================================================================================
bool mpuEscribir(uint8_t reg, uint8_t valor) {
  Wire.beginTransmission(mpuAddr);
  Wire.write(reg);
  Wire.write(valor);
  return Wire.endTransmission() == 0;
}

bool mpuLeer(uint8_t reg, uint8_t* buf, uint8_t n) {
  Wire.beginTransmission(mpuAddr);
  Wire.write(reg);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom((int)mpuAddr, (int)n) != n) return false;
  for (uint8_t i = 0; i < n; i++) buf[i] = Wire.read();
  return true;
}

bool mpuDetectar(uint8_t addr) {
  mpuAddr = addr;
  uint8_t who = 0;
  if (!mpuLeer(REG_WHO_AM_I, &who, 1)) return false;
  // 0x70 MPU-6500, 0x71 MPU-9250, 0x73 MPU-9255, 0x68 MPU-6050 (registros compatibles).
  Serial.printf("[IMU] WHO_AM_I en 0x%02X = 0x%02X\n", addr, who);
  return who == 0x70 || who == 0x71 || who == 0x73 || who == 0x68;
}

// Lee acelerometro (g) y giroscopo (rad/s, sin bias).
bool mpuLeerDatos(float acc[3], float gyr[3]) {
  uint8_t b[14];
  if (!mpuLeer(REG_ACCEL_XOUT_H, b, 14)) return false;
  int16_t ax = (int16_t)((b[0] << 8) | b[1]);
  int16_t ay = (int16_t)((b[2] << 8) | b[3]);
  int16_t az = (int16_t)((b[4] << 8) | b[5]);
  int16_t gx = (int16_t)((b[8] << 8) | b[9]);
  int16_t gy = (int16_t)((b[10] << 8) | b[11]);
  int16_t gz = (int16_t)((b[12] << 8) | b[13]);
  acc[0] = ax / 16384.0f; acc[1] = ay / 16384.0f; acc[2] = az / 16384.0f;
  gyr[0] = (gx / ESCALA_GYRO - gyroBias[0]) * GRAD_A_RAD;
  gyr[1] = (gy / ESCALA_GYRO - gyroBias[1]) * GRAD_A_RAD;
  gyr[2] = (gz / ESCALA_GYRO - gyroBias[2]) * GRAD_A_RAD;
  return true;
}

// Bias del giroscopo: el mango tiene que estar QUIETO durante ~1 s al encender.
void calibrarGyro() {
  float suma[3] = {0, 0, 0};
  const int N = 400;
  int validas = 0;
  uint8_t b[14];
  for (int i = 0; i < N; i++) {
    if (mpuLeer(REG_ACCEL_XOUT_H, b, 14)) {
      suma[0] += (int16_t)((b[8] << 8) | b[9]) / ESCALA_GYRO;
      suma[1] += (int16_t)((b[10] << 8) | b[11]) / ESCALA_GYRO;
      suma[2] += (int16_t)((b[12] << 8) | b[13]) / ESCALA_GYRO;
      validas++;
    }
    digitalWrite(PIN_LED, (i / 25) % 2);
    delay(3);
  }
  if (validas > 0)
    for (int k = 0; k < 3; k++) gyroBias[k] = suma[k] / validas;
  Serial.printf("[IMU] bias gyro (dps): %.3f %.3f %.3f\n", gyroBias[0], gyroBias[1], gyroBias[2]);
}

bool iniciarImu() {
  Wire.begin(PIN_SDA, PIN_SCL);
  Wire.setClock(400000);
  if (!mpuDetectar(MPU_ADDR_A) && !mpuDetectar(MPU_ADDR_B)) {
    Serial.println("[IMU] No se encontro MPU-6500. Se envian solo joystick y botones.");
    return false;
  }
  mpuEscribir(REG_PWR_MGMT_1, 0x80);  // reset
  delay(100);
  mpuEscribir(REG_PWR_MGMT_1, 0x01);  // reloj PLL del giroscopo
  delay(10);
  mpuEscribir(REG_SMPLRT_DIV, 0x00);
  mpuEscribir(REG_CONFIG, 0x03);       // DLPF gyro ~41 Hz
  mpuEscribir(REG_GYRO_CONFIG, 0x08);  // +-500 dps
  mpuEscribir(REG_ACCEL_CONFIG, 0x00); // +-2 g
  mpuEscribir(REG_ACCEL_CONFIG2, 0x03);// DLPF accel ~41 Hz
  delay(50);
  calibrarGyro();
  tArranqueFusion = millis();
  return true;
}

// Filtro de Madgwick sin magnetometro (6 ejes). Sin magnetometro el yaw deriva despacio:
// la pulsacion larga de "careta" recalibra la pose neutra en Unity.
void madgwick(float gx, float gy, float gz, float ax, float ay, float az, float dt, float beta) {
  float q0 = q[0], q1 = q[1], q2 = q[2], q3 = q[3];
  float qd0 = 0.5f * (-q1 * gx - q2 * gy - q3 * gz);
  float qd1 = 0.5f * ( q0 * gx + q2 * gz - q3 * gy);
  float qd2 = 0.5f * ( q0 * gy - q1 * gz + q3 * gx);
  float qd3 = 0.5f * ( q0 * gz + q1 * gy - q2 * gx);

  float normA = sqrtf(ax * ax + ay * ay + az * az);
  if (normA > 0.0001f) {
    ax /= normA; ay /= normA; az /= normA;
    float _2q0 = 2.0f * q0, _2q1 = 2.0f * q1, _2q2 = 2.0f * q2, _2q3 = 2.0f * q3;
    float _4q0 = 4.0f * q0, _4q1 = 4.0f * q1, _4q2 = 4.0f * q2;
    float _8q1 = 8.0f * q1, _8q2 = 8.0f * q2;
    float q0q0 = q0 * q0, q1q1 = q1 * q1, q2q2 = q2 * q2, q3q3 = q3 * q3;

    float s0 = _4q0 * q2q2 + _2q2 * ax + _4q0 * q1q1 - _2q1 * ay;
    float s1 = _4q1 * q3q3 - _2q3 * ax + 4.0f * q0q0 * q1 - _2q0 * ay - _4q1 + _8q1 * q1q1 + _8q1 * q2q2 + _4q1 * az;
    float s2 = 4.0f * q0q0 * q2 + _2q0 * ax + _4q2 * q3q3 - _2q3 * ay - _4q2 + _8q2 * q1q1 + _8q2 * q2q2 + _4q2 * az;
    float s3 = 4.0f * q1q1 * q3 - _2q1 * ax + 4.0f * q2q2 * q3 - _2q2 * ay;
    float normS = sqrtf(s0 * s0 + s1 * s1 + s2 * s2 + s3 * s3);
    if (normS > 0.000001f) {
      qd0 -= beta * s0 / normS;
      qd1 -= beta * s1 / normS;
      qd2 -= beta * s2 / normS;
      qd3 -= beta * s3 / normS;
    }
  }

  q0 += qd0 * dt; q1 += qd1 * dt; q2 += qd2 * dt; q3 += qd3 * dt;
  float n = sqrtf(q0 * q0 + q1 * q1 + q2 * q2 + q3 * q3);
  if (n < 0.000001f) { q[0] = 1; q[1] = q[2] = q[3] = 0; return; }
  q[0] = q0 / n; q[1] = q1 / n; q[2] = q2 / n; q[3] = q3 / n;
}

void actualizarImu() {
  static uint32_t tPrevio = micros();
  uint32_t ahora = micros();
  if (ahora - tPrevio < PERIODO_IMU_US) return;
  float dt = (ahora - tPrevio) * 1e-6f;
  tPrevio = ahora;
  if (dt > 0.05f) dt = 0.05f;

  float acc[3], gyr[3];
  if (!mpuLeerDatos(acc, gyr)) return;
  float beta = (millis() - tArranqueFusion < CONVERGENCIA_MS) ? MADGWICK_BETA_INICIAL : MADGWICK_BETA;
  madgwick(gyr[0], gyr[1], gyr[2], acc[0], acc[1], acc[2], dt, beta);
}

// =====================================================================================
//  Botones y joystick
// =====================================================================================
// Devuelve true en el flanco de presion.
bool actualizarBoton(Boton& b, uint32_t ahora) {
  bool crudo = digitalRead(b.pin) == LOW;
  if (crudo != b.lectura) { b.lectura = crudo; b.tCambio = ahora; }
  if (ahora - b.tCambio >= REBOTE_MS && b.estable != b.lectura) {
    b.estable = b.lectura;
    if (b.estable) { b.tPresion = ahora; b.largoDisparado = false; return true; }
  }
  return false;
}

// Boton con dos funciones: toque corto al soltar, largo mientras se mantiene.
void botonDoble(Boton& b, uint32_t ahora, uint32_t& pulsoCorto, uint32_t& pulsoLargo) {
  bool estabaApretado = b.estable;
  actualizarBoton(b, ahora);
  if (b.estable && !b.largoDisparado && ahora - b.tPresion >= PULSACION_LARGA_MS) {
    b.largoDisparado = true;
    pulsoLargo = ahora + DURACION_PULSO_MS;
  }
  if (estabaApretado && !b.estable && !b.largoDisparado)
    pulsoCorto = ahora + DURACION_PULSO_MS;
}

int leerAdcPromedio(int pin) {
  int suma = 0;
  for (int i = 0; i < 4; i++) suma += analogRead(pin);
  return suma / 4;
}

void calibrarJoystick() {
  long sx = 0, sy = 0;
  for (int i = 0; i < 32; i++) { sx += analogRead(PIN_JOY_X); sy += analogRead(PIN_JOY_Y); delay(2); }
  joyCentroX = sx / 32;
  joyCentroY = sy / 32;
  Serial.printf("[JOY] centro: %d %d\n", joyCentroX, joyCentroY);
}

float normalizarEje(int crudo, int centro, bool invertir) {
  float v;
  if (crudo >= centro) v = (float)(crudo - centro) / max(1, 4095 - centro);
  else                 v = (float)(crudo - centro) / max(1, centro);
  if (v > 1) v = 1;
  if (v < -1) v = -1;
  float a = fabsf(v);
  v = (a <= JOY_ZONA_MUERTA) ? 0.0f : copysignf((a - JOY_ZONA_MUERTA) / (1.0f - JOY_ZONA_MUERTA), v);
  return invertir ? -v : v;
}

// =====================================================================================
//  Red
// =====================================================================================
void iniciarWifi() {
#if WIFI_MODO_AP
  WiFi.mode(WIFI_AP);
  WiFi.softAP(AP_SSID, AP_PASS);
  Serial.printf("[WiFi] Red propia '%s', IP del mando %s\n", AP_SSID, WiFi.softAPIP().toString().c_str());
#else
  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin(STA_SSID, STA_PASS);
  Serial.printf("[WiFi] Conectando a '%s'...\n", STA_SSID);
#endif
  WiFi.setSleep(false);  // menor latencia
  udp.begin(PUERTO_ESP32);
}

bool redLista() {
#if WIFI_MODO_AP
  return true;
#else
  static bool informado = false;
  bool ok = WiFi.status() == WL_CONNECTED;
  if (ok && !informado) Serial.printf("[WiFi] Conectado, IP del mando %s\n", WiFi.localIP().toString().c_str());
  informado = ok;
  return ok;
#endif
}

IPAddress direccionBroadcast() {
#if WIFI_MODO_AP
  IPAddress ip = WiFi.softAPIP();
#else
  IPAddress ip = WiFi.localIP();
#endif
  return IPAddress(ip[0], ip[1], ip[2], 255);
}

// Unity manda "HELLO" cada segundo: a partir de ahi se le envia directo (unicast).
void atenderHello() {
  int n = udp.parsePacket();
  while (n > 0) {
    char buf[32];
    int len = udp.read(buf, sizeof(buf) - 1);
    buf[len > 0 ? len : 0] = 0;
    if (strncmp(buf, "HELLO", 5) == 0) {
      if (!hayHello || destinoUnicast != udp.remoteIP())
        Serial.printf("[WiFi] Unity en %s:%u\n", udp.remoteIP().toString().c_str(), udp.remotePort());
      destinoUnicast = udp.remoteIP();
      puertoUnicast = udp.remotePort();
      tUltimoHello = millis();
      hayHello = true;
    }
    n = udp.parsePacket();
  }
  if (hayHello && millis() - tUltimoHello > HELLO_VALIDO_MS) {
    hayHello = false;
    Serial.println("[WiFi] Unity dejo de responder, vuelvo a broadcast");
  }
}

// =====================================================================================
//  Envio
// =====================================================================================
inline int activo(uint32_t hasta, uint32_t ahora) { return (int32_t)(hasta - ahora) > 0 ? 1 : 0; }

void enviarEstado(uint32_t ahora) {
  char paquete[200];
  int len = 0;

  if (imuOk) {
    // Madgwick: marco del sensor, diestro, Z arriba. Unity: zurdo, Y arriba.
    // Intercambiar Y<->Z es una reflexion: q_unity = (w, -x, -z, -y).
    len += snprintf(paquete + len, sizeof(paquete) - len, "IMU,%.4f,%.4f,%.4f,%.4f,0\n",
                    q[0], -q[1], -q[3], -q[2]);
  }

  len += snprintf(paquete + len, sizeof(paquete) - len, "BTN,%d,%d,%d,%d,%d,%d,%d\n",
                  bChispa.estable ? 1 : 0,
                  bAmpMas.estable ? 1 : 0,
                  bAmpMenos.estable ? 1 : 0,
                  activo(pulsoCambiarHasta, ahora),
                  activo(pulsoReponerHasta, ahora),
                  activo(pulsoCaretaHasta, ahora),
                  activo(pulsoResetHasta, ahora));

  float jx = normalizarEje(leerAdcPromedio(PIN_JOY_X), joyCentroX, JOY_INVERTIR_X);
  float jy = normalizarEje(leerAdcPromedio(PIN_JOY_Y), joyCentroY, JOY_INVERTIR_Y);
  len += snprintf(paquete + len, sizeof(paquete) - len, "JOY,%.3f,%.3f,%d\n",
                  jx, jy, bJoySw.estable ? 1 : 0);

  if (redLista()) {
    if (hayHello) udp.beginPacket(destinoUnicast, puertoUnicast);
    else          udp.beginPacket(direccionBroadcast(), PUERTO_UNITY);
    udp.write((const uint8_t*)paquete, len);
    udp.endPacket();
  }

  if (++contadorEnvios % DIVISOR_SERIAL == 0) Serial.print(paquete);
}

// =====================================================================================
void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.println("\n=== FrankaVR - Mando ESP32 ===");

  pinMode(PIN_LED, OUTPUT);
  int pines[] = {PIN_BTN_CHISPA, PIN_BTN_AMP_MAS, PIN_BTN_AMP_MENOS, PIN_BTN_ELECTRODO, PIN_BTN_CARETA, PIN_JOY_SW};
  for (int p : pines) pinMode(p, INPUT_PULLUP);
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);  // rango ~0-3.1 V

  calibrarJoystick();              // no tocar el joystick al encender
  imuOk = iniciarImu();            // no mover el mango al encender
  iniciarWifi();
}

void loop() {
  uint32_t ahora = millis();

  if (imuOk) actualizarImu();

  actualizarBoton(bChispa, ahora);
  actualizarBoton(bAmpMas, ahora);
  actualizarBoton(bAmpMenos, ahora);
  actualizarBoton(bJoySw, ahora);
  botonDoble(bElectrodo, ahora, pulsoReponerHasta, pulsoCambiarHasta);
  botonDoble(bCareta, ahora, pulsoCaretaHasta, pulsoResetHasta);

  atenderHello();

  static uint32_t tEnvio = 0;
  if (ahora - tEnvio >= PERIODO_ENVIO_MS) {
    tEnvio = ahora;
    enviarEstado(ahora);
  }

  // LED: fijo = Unity conectado; parpadeo lento = esperando a Unity; rapido = sin IMU.
  uint32_t periodo = imuOk ? 500 : 150;
  digitalWrite(PIN_LED, hayHello ? HIGH : ((ahora / periodo) % 2));
}
