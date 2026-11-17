# Mando ESP32: joystick, 5 botones e IMU

Mando físico para FrankaVR: un joystick mueve la punta del electrodo, la IMU del mango lo inclina y cinco pulsadores manejan la soldadura. Se comunica con Unity por **WiFi (UDP)** y funciona igual en la PC y en el celular con Cardboard, sin plugins nativos.

**Estado:** el firmware y los scripts de Unity están escritos, pero **ninguno de los dos se compiló ni se probó con hardware**. Ver [Pendiente de validar](#pendiente-de-validar).

## Componentes

| Componente | Modelo |
|---|---|
| Placa | NodeMCU ESP32 WiFi + Bluetooth 4.2, USB-C (ESP32-WROOM-32) |
| Joystick | KY-023 |
| Botones | 5 × Big Key 12×12 mm (negro, azul, verde, rojo, amarillo) |
| IMU | GY-6500 (MPU-6500, acelerómetro + giroscopio, I2C) |

## Cableado

Todo a **3V3**, nunca a 5 V: las entradas del ESP32 no toleran 5 V. GND común para todos.

| Señal | GPIO | Notas |
|---|---|---|
| IMU SDA | 21 | |
| IMU SCL | 22 | |
| IMU VCC / GND | 3V3 / GND | AD0 sin conectar o a GND (dirección 0x68) |
| Joystick VRx | 34 | ADC1, sigue funcionando con WiFi activo |
| Joystick VRy | 35 | ADC1 |
| Joystick SW | 23 | |
| Joystick +5V / GND | **3V3** / GND | |
| Botón Chispa | 25 | otra pata a GND |
| Botón Amperaje + | 27 | otra pata a GND |
| Botón Amperaje − | 14 | otra pata a GND |
| Botón Electrodo | 33 | otra pata a GND |
| Botón Careta | 26 | otra pata a GND |

Los botones usan la resistencia de pull-up interna (`INPUT_PULLUP`): no hace falta agregar resistencias. Una sugerencia de colores: rojo para Chispa, verde y amarillo para Amperaje + y −, azul para Electrodo y negro para Careta.

La IMU va dentro del **mango móvil** con un cable de cuatro hilos hasta la base. Si queda fija a la madera, no mide la mano. La orientación del módulo dentro del mango no importa.

## Controles

| Control | Acción |
|---|---|
| Joystick | Mueve la punta: izquierda/derecha y adelante/atrás |
| Joystick apretado + arriba/abajo | Sube y baja la punta |
| Inclinar el mango (IMU) | Inclina el electrodo |
| Chispa (mantener) | Arco encendido |
| Amperaje + / − (toque) | Sube o baja el amperaje |
| Electrodo, toque | Repone la varilla consumida |
| Electrodo, mantener 0,7 s | Cambia el tipo de electrodo |
| Careta, toque | Sube o baja la careta |
| Careta, mantener 0,7 s | Endereza el electrodo y toma la pose actual del mango como neutra |

El teclado sigue funcionando en paralelo. Si la IMU no está conectada, el joystick igual mueve y la inclinación se hace con el teclado (I/J/K/L/U/O).

## Cargar el firmware

1. Instalar el [Arduino IDE](https://www.arduino.cc/en/software) 2.x.
2. En *Preferencias → URLs adicionales de gestor de placas*, agregar `https://espressif.github.io/arduino-esp32/package_esp32_index.json`. Después, en el gestor de placas, instalar **esp32 de Espressif Systems**.
3. Si Windows no reconoce la placa al enchufarla, instalar el driver del chip USB que trae (en estas placas suele ser CH340 o CP2102; está impreso cerca del conector).
4. Abrir `Firmware/FrankaVR_Mando/FrankaVR_Mando.ino`, elegir la placa **ESP32 Dev Module** y el puerto COM, y subir el sketch. Si la carga se queda en `Connecting...`, mantener apretado el botón **BOOT** de la placa hasta que empiece.
5. Abrir el monitor serie a **115200** baudios. Debe aparecer el `WHO_AM_I` de la IMU (0x70 para el MPU-6500), el centro del joystick y la red WiFi creada.

No hace falta instalar librerías: el sketch usa solo `Wire`, `WiFi` y `WiFiUdp`, que vienen con el core del ESP32.

**Al encender:** dejar el mango quieto y el joystick suelto durante unos 2 s, porque en ese momento se calibran el giroscopio y el centro del joystick.

## Conectar con Unity

Por defecto el ESP32 crea su propia red WiFi:

- Red: `FrankaVR-Mando`
- Clave: `frankavr26`
- IP del mando: `192.168.4.1`

1. Conectar la PC o el celular a esa red.
2. Entrar en Play en `Assets/Main.unity`. `GlovePacketBridge` agrega `UdpControllerReceiver` solo, que escucha en el puerto 4211 y le avisa al mando en el puerto 4210.
3. En Windows, **permitir** el acceso cuando el firewall pregunte por Unity.
4. El LED azul de la placa queda **fijo** cuando Unity está conectado. Parpadea lento mientras espera y rápido si no encontró la IMU.

En el inspector de `UdpControllerReceiver` se ven `PacketsReceived` y `LastPacketAgeSec`. En `BleGloveInput` se ven `JoystickAxes`, `HasImuPacket` e `IsCalibrated`.

Para usar la red de un router en lugar de la del ESP32: en el sketch poner `WIFI_MODO_AP 0`, completar `STA_SSID` y `STA_PASS`, y en `UdpControllerReceiver.ControllerAddress` poner la IP que el ESP32 imprime por serie. Algunas redes de la facultad bloquean el tráfico entre dispositivos; en ese caso, usar el modo AP.

## Protocolo

Cada 20 ms el mando envía un datagrama UDP con estas líneas:

```
IMU,qw,qx,qy,qz,0                          cuaternión ya en ejes de Unity (Y arriba)
BTN,chispa,amp+,amp-,cambiar,reponer,careta,enderezar
JOY,x,y,vertical                           x,y en -1..1, zona muerta aplicada
```

`IMU` y `BTN` son el formato que `GlovePacketBridge` ya parseaba; `JOY` es nuevo. Las acciones de toque se envían como un 1 durante 120 ms, porque Unity reacciona al flanco de subida. Si los paquetes dejan de llegar durante 350 ms, Unity corta la chispa y frena el movimiento.

## Ajustes

| Si pasa esto | Ajustar |
|---|---|
| Joystick al revés | `JOY_INVERTIR_X/Y` en el sketch, o `InvertJoystickX/Y` en `BleGloveInput` |
| La punta se mueve demasiado rápido o lento | `BleGloveInput.JoystickMaxRate` (0,5 por defecto, la misma escala que el teclado) |
| Inclinar el mango hacia adelante inclina el electrodo hacia un costado | `BleGloveInput.MountOffset`: rotar 90° o −90° en Y |
| El electrodo deriva de a poco (yaw) | Mantener **Careta** 0,7 s para recalibrar. Sin magnetómetro, una deriva lenta es normal |
| El joystick se mueve solo | Subir `JOY_ZONA_MUERTA` y reiniciar la placa con el joystick suelto |

## Pendiente de validar

- Compilar el sketch en Arduino IDE. En esta PC no hay `arduino-cli`.
- Compilar los scripts en Unity 6000.5.10f1. En esta PC no hay Unity ni SDK de .NET.
- Revisar el pinout contra la serigrafía de la placa: algunas variantes USB-C cambian el orden de los pines o el chip USB.
- Probar el sentido de los ejes de la IMU y del joystick con el hardware armado.
- Probar en Android: Unity debe incluir el permiso de Internet (*Player Settings → Internet Access*) y el celular tiene que quedar conectado a la red del mando aunque esa red no tenga Internet.
- El Bluetooth de la placa no se usa: Unity no trae soporte BLE y el plugin Android sigue sin hacerse. WiFi resuelve la PC y Android con el mismo código.
