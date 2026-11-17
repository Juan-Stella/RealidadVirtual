/*
  FrankaVR_Mando.ino  -  mando fisico para FrankaVR (celda MIG, ensenar y ejecutar)

  Hardware: ESP32 DevKit (WROOM-32) + joystick KY-023 + 5 pulsadores + IMU GY-6500 (opcional).
  Transporte: el ESP32 crea su propia red Wi-Fi "FrankaVR-Mando" (192.168.4.1) y manda
  paquetes UDP de texto a la app (puerto 4211), 50 veces por segundo:

    CTL,jx,jy,gatillo,acercar,retirar,ok,deshacer,borrar,altura,calibrar,vista,imu,qw,qx,qy,qz

  La app (Unity) le manda "HELLO" cada segundo al puerto 4210; asi el ESP32 sabe a que IP
  contestar. Mientras no hay HELLO, manda broadcast a 192.168.4.255.

  Arduino IDE: placa "ESP32 Dev Module" (paquete "esp32" de Espressif, 2.x o 3.x).
  No necesita librerias externas.
*/
#include <WiFi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include <math.h>
#include <string.h>

// ================== Configuracion ==================
static const char*    WIFI_SSID      = "FrankaVR-Mando";
static const char*    WIFI_PASS      = "frankavr123";   // minimo 8 caracteres
static const uint16_t PORT_ESP32     = 4210;            // aca llegan los HELLO de la app
static const uint16_t PORT_APP       = 4211;            // la app escucha aca
static const uint32_t SEND_PERIOD_MS = 20;              // 50 Hz
static const uint32_t IMU_PERIOD_US  = 5000;            // 200 Hz

#define USE_IMU             1   // 0 si todavia no esta conectada la IMU
#define DEBUG_SERIAL        1   // imprime el estado 5 veces por segundo (115200 baudios)
#define BUTTONS_ACTIVE_LOW  1   // 1: pulsador entre el pin y GND (pull-up interno). 0: pulsador a 3V3
#define JOY_INVERT_X        0   // poner en 1 si "derecha" da negativo
#define JOY_INVERT_Y        0   // poner en 1 si "adelante" da negativo
#define JOY_SWAP_XY         0   // poner en 1 si el joystick quedo girado 90 grados

static const float    STICK_DEADZONE = 0.12f;
static const uint32_t DEBOUNCE_MS    = 25;
static const uint32_t LONG_PRESS_MS  = 900;
static const uint32_t PULSE_MS       = 150;   // cada accion se mantiene 150 ms: UDP puede perder un paquete

// ================== Pines ==================
static const int PIN_JOY_X    = 34;  // ADC1 (con Wi-Fi encendido NO se puede usar ADC2)
static const int PIN_JOY_Y    = 35;  // ADC1
static const int PIN_JOY_SW   = 33;  // boton de la palanca (altura)
static const int PIN_GATILLO  = 25;  // grabar trayectoria (se mantiene)
static const int PIN_ACERCAR  = 26;  // corto: ir al inicio de la junta | largo: cambiar vista
static const int PIN_RETIRAR  = 27;  // retirada segura / abortar ejecucion
static const int PIN_OK       = 14;  // corto: elegir lo que se mira / ejecutar | largo: calibrar
static const int PIN_DESHACER = 13;  // corto: deshacer tramo | largo: borrar todo
static const int PIN_LED      = 2;   // LED azul de la placa
static const int PIN_SDA      = 21;
static const int PIN_SCL      = 22;

// ================== Botones ==================
struct Button
{
  int pin;
  bool activeLow;
  bool raw;
  bool pressed;
  bool longFired;
  uint32_t changedAt;
  uint32_t pressedAt;
};

enum { EV_PRESS = 1, EV_SHORT = 2, EV_LONG = 4 };

static Button bGatillo  = { PIN_GATILLO,  BUTTONS_ACTIVE_LOW == 1, false, false, false, 0, 0 };
static Button bAcercar  = { PIN_ACERCAR,  BUTTONS_ACTIVE_LOW == 1, false, false, false, 0, 0 };
static Button bRetirar  = { PIN_RETIRAR,  BUTTONS_ACTIVE_LOW == 1, false, false, false, 0, 0 };
static Button bOk       = { PIN_OK,       BUTTONS_ACTIVE_LOW == 1, false, false, false, 0, 0 };
static Button bDeshacer = { PIN_DESHACER, BUTTONS_ACTIVE_LOW == 1, false, false, false, 0, 0 };
static Button bPalanca  = { PIN_JOY_SW,   true,                    false, false, false, 0, 0 };  // KY-023: SW a GND

static uint8_t updateButton(Button& b, uint32_t now)
{
  uint8_t ev = 0;
  bool level = digitalRead(b.pin) == HIGH;
  bool r = b.activeLow ? !level : level;
  if (r != b.raw) { b.raw = r; b.changedAt = now; }
  if (b.raw != b.pressed && now - b.changedAt >= DEBOUNCE_MS)
  {
    b.pressed = b.raw;
    if (b.pressed) { b.pressedAt = now; b.longFired = false; ev |= EV_PRESS; }
    else if (!b.longFired) ev |= EV_SHORT;
  }
  if (b.pressed && !b.longFired && now - b.pressedAt >= LONG_PRESS_MS) { b.longFired = true; ev |= EV_LONG; }
  return ev;
}

// Acciones con pulso (la app detecta el flanco de subida).
enum { A_ACERCAR, A_RETIRAR, A_OK, A_DESHACER, A_BORRAR, A_CALIBRAR, A_VISTA, A_COUNT };
static uint32_t pulseUntil[A_COUNT];
static bool pulseActive[A_COUNT];

static void pulse(int action, uint32_t now) { pulseUntil[action] = now + PULSE_MS; pulseActive[action] = true; }
static bool active(int action, uint32_t now)
{
  if (pulseActive[action] && (int32_t)(pulseUntil[action] - now) <= 0) pulseActive[action] = false;
  return pulseActive[action];
}

// ================== Joystick ==================
static int centerX = 2048, centerY = 2048;
static int rawX = 0, rawY = 0;

static int readAvg(int pin)
{
  long sum = 0;
  for (int i = 0; i < 4; i++) sum += analogRead(pin);
  return (int)(sum / 4);
}

static float axisFromRaw(int raw, int center)
{
  float v = raw >= center ? (raw - center) / (float)(4095 - center) : (raw - center) / (float)center;
  if (v > 1.0f) v = 1.0f;
  if (v < -1.0f) v = -1.0f;
  float a = fabsf(v);
  if (a < STICK_DEADZONE) return 0.0f;
  return copysignf((a - STICK_DEADZONE) / (1.0f - STICK_DEADZONE), v);
}

static void readStick(float& jx, float& jy)
{
  rawX = readAvg(PIN_JOY_X);
  rawY = readAvg(PIN_JOY_Y);
  float x = axisFromRaw(rawX, centerX);
  float y = axisFromRaw(rawY, centerY);
#if JOY_SWAP_XY
  float t = x; x = y; y = t;
#endif
#if JOY_INVERT_X
  x = -x;
#endif
#if JOY_INVERT_Y
  y = -y;
#endif
  jx = x;
  jy = y;
}

// ================== IMU (MPU6500 / MPU9250 / MPU6050 por registros) ==================
static uint8_t imuAddr = 0x68;
static bool imuOk = false;
static float gyroBias[3] = { 0, 0, 0 };
static float q0 = 1.0f, q1 = 0.0f, q2 = 0.0f, q3 = 0.0f;   // sensor -> mundo (Hamilton)
static const float TWO_KP = 2.0f * 0.8f;                     // Mahony: correccion con gravedad
static uint32_t lastImuUs = 0;

static bool i2cWrite(uint8_t reg, uint8_t val)
{
  Wire.beginTransmission(imuAddr);
  Wire.write(reg);
  Wire.write(val);
  return Wire.endTransmission() == 0;
}

static bool i2cRead(uint8_t reg, uint8_t* buf, int n)
{
  Wire.beginTransmission(imuAddr);
  Wire.write(reg);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom((int)imuAddr, n) != n) return false;
  for (int i = 0; i < n; i++) buf[i] = Wire.read();
  return true;
}

static bool imuBegin()
{
  Wire.begin(PIN_SDA, PIN_SCL);
  Wire.setClock(400000);
  const uint8_t candidates[2] = { 0x68, 0x69 };
  for (int k = 0; k < 2; k++)
  {
    imuAddr = candidates[k];
    uint8_t who = 0;
    if (!i2cRead(0x75, &who, 1) || who == 0x00 || who == 0xFF) continue;
    Serial.printf("IMU en 0x%02X, WHO_AM_I=0x%02X (6500=0x70, 9250=0x71, 6050=0x68)\n", imuAddr, who);
    i2cWrite(0x6B, 0x80); delay(100);   // reset
    i2cWrite(0x6B, 0x01); delay(10);    // reloj PLL
    i2cWrite(0x6C, 0x00);               // acel y giro encendidos
    i2cWrite(0x1A, 0x03);               // filtro giro ~41 Hz
    i2cWrite(0x19, 0x04);               // 200 Hz
    i2cWrite(0x1B, 0x08);               // giro +-500 dps
    i2cWrite(0x1C, 0x08);               // acel +-4 g
    i2cWrite(0x1D, 0x03);               // filtro acel ~41 Hz (MPU6500)
    return true;
  }
  return false;
}

static bool imuRead(float a[3], float g[3])
{
  uint8_t b[14];
  if (!i2cRead(0x3B, b, 14)) return false;
  for (int i = 0; i < 3; i++)
  {
    int16_t ra = (int16_t)((b[2 * i] << 8) | b[2 * i + 1]);
    int16_t rg = (int16_t)((b[8 + 2 * i] << 8) | b[8 + 2 * i + 1]);
    a[i] = ra / 8192.0f;                          // g
    g[i] = rg / 65.5f * (float)(M_PI / 180.0);    // rad/s
  }
  return true;
}

static void calibrateGyro()
{
  Serial.println("Calibrando giroscopo: dejar el mando QUIETO 2 segundos...");
  float sum[3] = { 0, 0, 0 };
  int n = 0;
  for (int i = 0; i < 400; i++)
  {
    float a[3], g[3];
    if (imuRead(a, g)) { for (int k = 0; k < 3; k++) sum[k] += g[k]; n++; }
    digitalWrite(PIN_LED, (i / 25) % 2);
    delay(5);
  }
  for (int k = 0; k < 3; k++) gyroBias[k] = n > 0 ? sum[k] / n : 0.0f;
  Serial.printf("Bias giro [rad/s]: %.4f %.4f %.4f (%d muestras)\n", gyroBias[0], gyroBias[1], gyroBias[2], n);
}

// Filtro de Mahony (version de Madgwick, solo acelerometro + giroscopo).
static void mahony(float gx, float gy, float gz, float ax, float ay, float az, float dt)
{
  float norm = sqrtf(ax * ax + ay * ay + az * az);
  if (norm > 0.0001f)
  {
    ax /= norm; ay /= norm; az /= norm;
    float vx = q1 * q3 - q0 * q2;               // gravedad estimada (mitad)
    float vy = q0 * q1 + q2 * q3;
    float vz = q0 * q0 - 0.5f + q3 * q3;
    float ex = ay * vz - az * vy;
    float ey = az * vx - ax * vz;
    float ez = ax * vy - ay * vx;
    gx += TWO_KP * ex; gy += TWO_KP * ey; gz += TWO_KP * ez;
  }
  gx *= 0.5f * dt; gy *= 0.5f * dt; gz *= 0.5f * dt;
  float qa = q0, qb = q1, qc = q2;
  q0 += -qb * gx - qc * gy - q3 * gz;
  q1 += qa * gx + qc * gz - q3 * gy;
  q2 += qa * gy - qb * gz + q3 * gx;
  q3 += qa * gz + qb * gy - qc * gx;
  norm = sqrtf(q0 * q0 + q1 * q1 + q2 * q2 + q3 * q3);
  q0 /= norm; q1 /= norm; q2 /= norm; q3 /= norm;
}

// Montaje: IMU plana, chip hacia arriba, flecha X de la placa hacia ADELANTE (lejos de la mano).
// Sensor (derecho): X adelante, Y izquierda, Z arriba -> Unity (izquierdo): x derecha, y arriba, z adelante.
static void toUnity(float& w, float& x, float& y, float& z)
{
  w = q0;
  x = q2;
  y = -q3;
  z = -q1;
}

// ================== Red ==================
static WiFiUDP udp;
static IPAddress appIP;
static bool haveApp = false;
static uint32_t lastHello = 0;

static void pollHello(uint32_t now)
{
  int size = udp.parsePacket();
  while (size > 0)
  {
    char buf[64];
    int len = udp.read(buf, sizeof(buf) - 1);
    buf[len > 0 ? len : 0] = 0;
    if (strncmp(buf, "HELLO", 5) == 0)
    {
      if (!haveApp || appIP != udp.remoteIP())
        Serial.printf("App conectada: %s\n", udp.remoteIP().toString().c_str());
      appIP = udp.remoteIP();
      haveApp = true;
      lastHello = now;
    }
    size = udp.parsePacket();
  }
  if (haveApp && now - lastHello > 3000)
  {
    haveApp = false;
    Serial.println("Sin HELLO de la app: vuelvo a broadcast");
  }
}

static void sendPacket(const char* msg)
{
  IPAddress dest = haveApp ? appIP : IPAddress(192, 168, 4, 255);
  udp.beginPacket(dest, PORT_APP);
  udp.write((const uint8_t*)msg, strlen(msg));
  udp.endPacket();
}

// ================== Programa ==================
static uint32_t lastSend = 0, lastDebug = 0;

void setup()
{
  Serial.begin(115200);
  delay(300);
  Serial.println("\nFrankaVR mando - arrancando");
  pinMode(PIN_LED, OUTPUT);
  Button* buttons[] = { &bGatillo, &bAcercar, &bRetirar, &bOk, &bDeshacer };
  for (Button* b : buttons) pinMode(b->pin, BUTTONS_ACTIVE_LOW ? INPUT_PULLUP : INPUT_PULLDOWN);
  pinMode(PIN_JOY_SW, INPUT_PULLUP);
  analogReadResolution(12);

  // Centro de la palanca: no tocarla al encender.
  long sx = 0, sy = 0;
  for (int i = 0; i < 32; i++) { sx += analogRead(PIN_JOY_X); sy += analogRead(PIN_JOY_Y); delay(2); }
  centerX = (int)(sx / 32);
  centerY = (int)(sy / 32);
  if (centerX < 500 || centerX > 3600 || centerY < 500 || centerY > 3600)
    Serial.printf("OJO: centro de palanca raro (X=%d Y=%d). Revisar VRx/VRy y que el KY-023 este a 3V3.\n", centerX, centerY);
  Serial.printf("Centro palanca: X=%d Y=%d\n", centerX, centerY);

#if USE_IMU
  imuOk = imuBegin();
  if (imuOk) calibrateGyro();
  else Serial.println("IMU no encontrada: sigo sin IMU (revisar SDA=21, SCL=22, 3V3, NCS a 3V3)");
  lastImuUs = micros();
#endif

  WiFi.mode(WIFI_AP);
  WiFi.softAP(WIFI_SSID, WIFI_PASS, 6, 0, 2);
  WiFi.setSleep(false);   // menos latencia
  udp.begin(PORT_ESP32);
  Serial.printf("Red \"%s\" lista. IP del mando: %s. Conectar el celular/PC a esa red.\n",
                WIFI_SSID, WiFi.softAPIP().toString().c_str());
}

void loop()
{
  uint32_t now = millis();

#if USE_IMU
  uint32_t us = micros();
  if (imuOk && us - lastImuUs >= IMU_PERIOD_US)
  {
    float dt = (us - lastImuUs) * 1e-6f;
    lastImuUs = us;
    if (dt > 0.05f) dt = 0.05f;
    float a[3], g[3];
    if (imuRead(a, g))
      mahony(g[0] - gyroBias[0], g[1] - gyroBias[1], g[2] - gyroBias[2], a[0], a[1], a[2], dt);
  }
#endif

  // Botones: cortos al soltar, largos al cumplir el tiempo; RETIRAR actua al apretar.
  updateButton(bGatillo, now);
  uint8_t e = updateButton(bAcercar, now);
  if (e & EV_SHORT) pulse(A_ACERCAR, now);
  if (e & EV_LONG) pulse(A_VISTA, now);
  e = updateButton(bRetirar, now);
  if (e & EV_PRESS) pulse(A_RETIRAR, now);
  e = updateButton(bOk, now);
  if (e & EV_SHORT) pulse(A_OK, now);
  if (e & EV_LONG) pulse(A_CALIBRAR, now);
  e = updateButton(bDeshacer, now);
  if (e & EV_SHORT) pulse(A_DESHACER, now);
  if (e & EV_LONG) pulse(A_BORRAR, now);
  updateButton(bPalanca, now);

  pollHello(now);

  if (now - lastSend >= SEND_PERIOD_MS)
  {
    lastSend = now;
    float jx, jy;
    readStick(jx, jy);
    float w = 1.0f, x = 0.0f, y = 0.0f, z = 0.0f;
    if (imuOk) toUnity(w, x, y, z);
    char msg[192];
    snprintf(msg, sizeof(msg), "CTL,%.2f,%.2f,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%.4f,%.4f,%.4f,%.4f\n",
             jx, jy,
             bGatillo.pressed ? 1 : 0,
             active(A_ACERCAR, now) ? 1 : 0,
             active(A_RETIRAR, now) ? 1 : 0,
             active(A_OK, now) ? 1 : 0,
             active(A_DESHACER, now) ? 1 : 0,
             active(A_BORRAR, now) ? 1 : 0,
             bPalanca.pressed ? 1 : 0,
             active(A_CALIBRAR, now) ? 1 : 0,
             active(A_VISTA, now) ? 1 : 0,
             imuOk ? 1 : 0,
             w, x, y, z);
    sendPacket(msg);
#if DEBUG_SERIAL
    if (now - lastDebug >= 200)
    {
      lastDebug = now;
      Serial.printf("[%s] rawX=%4d rawY=%4d  %s", haveApp ? "app" : "broadcast", rawX, rawY, msg);
    }
#endif
  }

  // LED: apagado = nadie conectado; parpadeo = conectado sin app; fijo = app recibiendo.
  bool station = WiFi.softAPgetStationNum() > 0;
  digitalWrite(PIN_LED, haveApp ? HIGH : station ? ((now / 250) % 2) : LOW);
}
