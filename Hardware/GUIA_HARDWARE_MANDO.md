# FrankaVR: mando físico ESP32 (guía para armarlo)

> **Para el Claude que lea esto:** vas a ayudar a armar y probar el mando físico de FrankaVR. La app de Unity ya está lista para recibirlo, así que **no hay que tocar Unity**. El trabajo es de hardware y firmware: conectar, cargar `FrankaVR_Mando.ino`, verificar con el Monitor Serie y probar con la app. Respetá los pines y el protocolo de esta guía, porque la app los espera exactamente así.

## 1. Qué hace el mando
FrankaVR es una celda de soldadura MIG con un robot Franka Panda. El operario usa lentes de realidad virtual Cardboard con el celular.

- **Enseñar:** el operario mueve la torcha por toda la junta con este mando mientras aprieta el gatillo, y la trayectoria queda grabada.
- **Ejecutar:** el robot repite esa trayectoria solo, con el arco encendido.

El mando reemplaza al teclado. Tiene cuatro partes:

| Parte | Función |
|---|---|
| Palanca (joystick KY-023) | Mueve la punta de la torcha. Cuanto más se empuja, más rápido. |
| Palanca apretada + adelante/atrás | Sube o baja la torcha (distancia a la pieza). |
| 5 pulsadores | Gatillo, ACERCAR, RETIRAR, OK y DESHACER. |
| IMU GY-6500 (opcional) | Al inclinar el mando se inclina la torcha (ángulos de soldadura). |

## 2. Materiales
- 1 ESP32 DevKit V1 (ESP32-WROOM-32, 30 o 38 pines) y un cable micro-USB **de datos**. En Windows hace falta el driver CP2102 (Silicon Labs) o CH340, según la placa.
- 1 joystick KY-023.
- 5 pulsadores. Sirven los módulos de 3 pines o pulsadores comunes.
- 1 IMU GY-6500 (MPU6500). También funcionan una MPU9250 o una MPU6050.
- Protoboard o placa perforada, cables dupont y una caja tipo mando.
- Alimentación: un power bank por USB.

## 3. Conexiones
**Todo va a 3V3, nada a 5 V.** Los ADC del ESP32 admiten como máximo 3,3 V: un KY-023 alimentado con 5 V puede dañar los pines 34 y 35.

| Componente | Pin del módulo | Pin del ESP32 | Nota |
|---|---|---|---|
| KY-023 | GND | GND | |
| KY-023 | +5V | **3V3** | Aunque diga 5 V |
| KY-023 | VRx | GPIO 34 | ADC1, solo entrada |
| KY-023 | VRy | GPIO 35 | ADC1, solo entrada |
| KY-023 | SW | GPIO 33 | Pull-up interno |
| Pulsador GATILLO | una pata / S | GPIO 25 | la otra pata a GND |
| Pulsador ACERCAR | una pata / S | GPIO 26 | la otra pata a GND |
| Pulsador RETIRAR | una pata / S | GPIO 27 | la otra pata a GND |
| Pulsador OK | una pata / S | GPIO 14 | la otra pata a GND |
| Pulsador DESHACER | una pata / S | GPIO 13 | la otra pata a GND |
| GY-6500 | VCC | 3V3 | |
| GY-6500 | GND | GND | |
| GY-6500 | SDA | GPIO 21 | I2C |
| GY-6500 | SCL | GPIO 22 | I2C |
| GY-6500 | AD0 / SDO | GND | Dirección 0x68 |
| GY-6500 | NCS | 3V3 | Obligatorio para trabajar en I2C |
| GY-6500 | FSYNC | GND | Si la placa lo tiene |
| GY-6500 | INT, EDA, ECL | sin conectar | |

**Pulsadores de módulo de 3 pines (S, +, −):** conectá S al GPIO y − a GND, y probá primero sin conectar +. Si en el Monitor Serie un botón aparece apretado cuando no lo está (o al revés), conectá + a 3V3 y poné `BUTTONS_ACTIVE_LOW 0` en el firmware.

**Pines prohibidos:**
- ADC2 (GPIO 0, 2, 4, 12-15 y 25-27) no sirve para entradas analógicas con el Wi-Fi encendido. Por eso la palanca va a los pines 34 y 35. Como entradas digitales, para los botones, los pines 25-27, 13 y 14 andan bien.
- No usar GPIO 0, 2, 12 ni 15 para botones: son pines de arranque.

**Montaje de la IMU:**
- Pegala firme y plana dentro de la caja, con el chip hacia arriba.
- La flecha **X** impresa en la placa tiene que apuntar hacia adelante, lejos de la mano, en el sentido en que apunta el mando.
- Si queda montada de otra forma, hay que cambiar la función `toUnity()` del firmware.

**Distribución en la caja (sugerida):** la palanca bajo el pulgar, el GATILLO bajo el índice y los otros cuatro botones arriba, al alcance del pulgar.

## 4. Firmware
Archivo: `Hardware/FrankaVR_Mando/FrankaVR_Mando.ino`. No necesita librerías externas.

1. En el Arduino IDE 2, agregá esta URL en *Preferencias*: `https://espressif.github.io/arduino-esp32/package_esp32_index.json`. Después, en el Gestor de placas, instalá **esp32 de Espressif** (versión 2.x o 3.x).
2. Elegí la placa **ESP32 Dev Module** y el puerto COM correspondiente.
3. Subí el sketch. Si no arranca la subida, mantené apretado el botón BOOT de la placa al empezar.
4. Abrí el Monitor Serie a **115200 baudios**.
5. Al encender:
   - **No toques la palanca:** el firmware mide su posición central.
   - **Dejá el mando quieto 2 segundos:** el firmware calibra el giroscopo. Mientras tanto el LED parpadea.

Opciones al principio del archivo:

| Opción | Para qué sirve |
|---|---|
| `USE_IMU` | Ponela en 0 si todavía no conectaste la IMU. |
| `BUTTONS_ACTIVE_LOW` | Polaridad de los pulsadores. |
| `JOY_INVERT_X`, `JOY_INVERT_Y`, `JOY_SWAP_XY` | Corrigen la orientación de la palanca. |
| `WIFI_SSID`, `WIFI_PASS` | Nombre y contraseña de la red del mando. |

### Prueba en el Monitor Serie (antes de usar la app)
Cada 200 ms aparece una línea con este formato:

```
[broadcast] rawX=1870 rawY=1912  CTL,0.00,0.00,0,0,0,0,0,0,0,0,0,1,1.0000,0.0000,0.0000,0.0000
```

Revisá en orden:
- [ ] **Palanca en reposo:** `jx` y `jy` (los dos primeros números) quedan en 0.00.
- [ ] **Palanca a la derecha:** `jx` llega a +1.00. Si da negativo, `JOY_INVERT_X 1`.
- [ ] **Palanca hacia adelante:** `jy` llega a +1.00. Si da negativo, `JOY_INVERT_Y 1`. Si los ejes salen cambiados, `JOY_SWAP_XY 1`.
- [ ] **Palanca apretada:** el 9.º campo (altura) pasa a 1.
- [ ] **GATILLO:** el 3.º campo vale 1 mientras se aprieta.
- [ ] **Toque corto de ACERCAR, RETIRAR, OK o DESHACER:** su campo vale 1 durante unos 150 ms.
- [ ] **OK largo (≥ 0,9 s):** se activa calibrar. **ACERCAR largo:** vista. **DESHACER largo:** borrar.
- [ ] **IMU:** el campo `imu` vale 1. Con el mando quieto, los cuatro números del final no derivan rápido. Al inclinar el mando, cambian.

## 5. Protocolo (fijo: la app lo espera así)
1. El ESP32 crea la red Wi-Fi **FrankaVR-Mando** (contraseña `frankavr123`) con la IP fija **192.168.4.1**.
2. La app manda `HELLO,FrankaVR,4211` cada segundo al puerto **4210** del ESP32.
3. El ESP32 contesta a esa IP en el puerto **4211**. Mientras no recibe ningún HELLO, manda en broadcast a 192.168.4.255:4211.
4. Envía 50 paquetes por segundo, cada uno de una línea de texto terminada en `\n`, con decimales con punto:

```
CTL,jx,jy,gatillo,acercar,retirar,ok,deshacer,borrar,altura,calibrar,vista,imu,qw,qx,qy,qz
```

| Campo | Valor | Qué hace en la app |
|---|---|---|
| jx, jy | -1..1, con zona muerta | Mueve la torcha (derecha y adelante son positivos, según la vista). Durante la grabación, avanza sobre la junta a velocidad proporcional. |
| gatillo | nivel | Grabar la trayectoria (como la tecla Espacio). |
| acercar | pulso | Llevar la torcha al inicio de la junta (B). |
| retirar | pulso | Retirada segura; durante la ejecución, aborta (C). |
| ok | pulso | Hace clic en el botón que se está mirando. Si no se mira ninguno y se está enseñando, ejecuta el programa (Enter). |
| deshacer | pulso | Borra el último tramo grabado (Z). |
| borrar | pulso | Borra toda la trayectoria (R). |
| altura | nivel | Con la palanca apretada, el eje adelante/atrás sube y baja la torcha (Q/E). |
| calibrar | pulso | La pose actual del mando pasa a ser la de la torcha y se recentra el Cardboard. |
| vista | pulso | Cambia de vista (botón VISTA). |
| imu | 0/1 | Indica si qw..qz es válido. |
| qw,qx,qy,qz | cuaternión | Ya convertido a ejes de Unity. |

**Reglas del protocolo:**
- Las acciones se mandan como pulsos de 150 ms porque UDP puede perder un paquete; la app detecta el flanco de subida.
- Si la app no recibe paquetes durante 0,35 s, suelta el gatillo y la palanca y vuelve al teclado.

**Del lado de Unity (ya implementado, para referencia):**
- `Assets/FrankaVR/Input/UdpControllerReceiver.cs`
- `Assets/FrankaVR/Input/HandControllerInput.cs`
- `Assets/Input/GlovePacketBridge.cs` (paquete `CTL`)
- Prueba automática en el menú *FrankaVR / Verificacion / Mando Wi-Fi (UDP)*. Simula el mando por UDP con este mismo formato y pasa.

## 6. Prueba con la app

### En la PC (Unity Editor)
1. Conectá la PC a la red **FrankaVR-Mando**.
2. Abrí `Assets/Main.unity` y dale Play. Si aparece el firewall de Windows, permití Unity en redes privadas.
3. En el Monitor Serie tiene que aparecer `App conectada: 192.168.4.x` y el LED tiene que quedar fijo.
4. En el HUD, la línea de entrada tiene que decir **Entrada: Mando**.
5. Probá el flujo completo:
   - Elegí M1 → PRACTICAR → ACERCAR.
   - Mantené el GATILLO y mové la palanca hacia el sentido que indica el HUD: avanza sobre la junta.
   - OK → el robot ejecuta.
6. Si algo falla, en el Inspector del objeto que tiene `GlovePacketBridge` mirá:
   - `UdpControllerReceiver`: `Received` sube y `LastSender` muestra 192.168.4.1.
   - `HandControllerInput`: `Connected`, `Stick` y `Trigger`.

### En el celular (APK con Cardboard)
1. Compilá el APK como siempre. *Internet Access = Require* ya quedó activado en Player Settings.
2. Poné el celular en **modo avión, activá solo el Wi-Fi** y conectalo a FrankaVR-Mando. Si aparece "sin internet", elegí mantener la conexión.

   Esto es necesario porque con datos móviles prendidos Android manda el tráfico por la red móvil y los paquetes no llegan. Desde la rama `revision-android-20261121` la app intenta atarse sola a la red Wi-Fi (permiso `ACCESS_NETWORK_STATE`, sin probar en un celular todavía): si el mando anda con datos móviles prendidos, este paso deja de ser obligatorio.
3. Abrí la app, poné los lentes y apretá OK largo para calibrar con el mando apuntando hacia adelante.

## 7. Problemas comunes

| Síntoma | Causa probable |
|---|---|
| No se ve el puerto COM | El cable es solo de carga, o falta el driver CP2102/CH340. |
| La subida se queda en `Connecting...` | Hay que mantener BOOT apretado. |
| La palanca da ±1 sola o se mueve sin tocarla | Está alimentada a 5 V, o conectada a un pin ADC2. |
| Avisa «centro de palanca raro» | VRx/VRy mal conectados, o se estaba tocando la palanca al encender. |
| `IMU no encontrada` | Revisar SDA/SCL, 3V3 y NCS a 3V3. Probar AD0 a GND. |
| La torcha gira al revés al inclinar el mando | La IMU está montada en otra orientación: ajustar `toUnity()` (mapeo de ejes, ver comentario). |
| La IMU deriva de a poco en el giro horizontal | Es normal sin magnetómetro: OK largo recalibra. |
| La app no recibe nada | Firewall (PC), datos móviles prendidos (celular), o el dispositivo no está en la red FrankaVR-Mando. |
| Un botón queda «apretado» | Polaridad: cambiar `BUTTONS_ACTIVE_LOW`, o conectar + del módulo a 3V3. |

## 8. Estado
- **App:** el lado de la app está probado en el Editor con un mando simulado. Verifica conexión, palanca, altura, IMU, acercar, enseñar, deshacer, borrar, OK/ejecutar, retirar y timeout.
- **Firmware:** escrito para el núcleo Arduino-ESP32, **todavía sin compilar ni probar en una placa real**. Ese es el primer paso del compañero: compilarlo, subirlo y hacer las pruebas de las secciones 4 y 6. Si algo de la API del núcleo ESP32 no compila, corregirlo sin cambiar pines ni protocolo.
