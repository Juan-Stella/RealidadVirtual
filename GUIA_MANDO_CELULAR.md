# Guía del mando celular (26/11/2026)

Se usan dos celulares: **celular A** con la app FrankaVR en el Cardboard (visualización) y **celular B** como mando del robot, en el navegador. No hace falta hardware, ni PC intermedia, ni instalar una app en B. Reemplaza al mando ESP32 (que sigue funcionando: los dos entran por el mismo camino).

Está escrita para el equipo y para otra IA que retome el trabajo. Antes de tocar nada, leer `AGENTS.md` y `CONTEXTO_FRANKAVR_CODEX.md`.

## 1. Cómo funciona

```
Celular B (Chrome/Safari)                Celular A (app FrankaVR, o el Editor en la PC)
MandoCelular.html  --POST /mando-->  PhoneControllerServer (HTTP, puerto 8080)
   ~22 líneas CTL por segundo              │ misma línea CTL que el ESP32
                                           ▼
                                   GlovePacketBridge.OnBleMessage
                                           ▼
                                   HandControllerInput → HybridTeleopInput
                                   (control de punta, guía de costura, enseñar/ejecutar)
```

- La propia app abre un servidor HTTP mínimo en el puerto **8080**: `GET /` entrega la página del mando y `POST /mando` recibe una línea `CTL`.
- La página arma **exactamente** la línea `CTL` del firmware del ESP32 (`Hardware/FrankaVR_Mando/FrankaVR_Mando.ino`):
  `CTL,jx,jy,gatillo,acercar,retirar,ok,deshacer,borrar,altura,calibrar,vista,imu,qw,qx,qy,qz`
  Por eso Unity no distingue entre el celular y el mando físico, y no se tocaron ni la física, ni la IK, ni la deposición.
- La idea sale del modo "Actuador" del HMI de la CAVE (Drive "Cave Nov 2026" → `2_Servidor_y_HMI/hmi`: `web/index.html` y `puente.py`). De ahí se tomaron el estilo visual, el manejo de toques (pointer capture), el envío serializado con tiempo máximo de espera y el aviso "sin respuesta". El proxy Python y la JoystikApp de la CAVE **no** se usan: Unity sirve la página directamente.

## 2. Archivos

| Archivo | Qué es |
|---|---|
| `Assets/FrankaVR/Input/PhoneControllerServer.cs` | Servidor HTTP (TcpListener, sin paquetes externos). Encola las líneas y las pasa al puente en `Update`. Publica `PhoneControllerServer.CurrentUrl`. |
| `Assets/FrankaVR/Input/Resources/MandoCelular.html` | La página del mando: HTML/CSS/JS en un solo archivo, sin librerías. Se carga como `TextAsset` con `Resources.Load("MandoCelular")`. |
| `Assets/Input/GlovePacketBridge.cs` | En `Awake` agrega `PhoneControllerServer` junto a `UdpControllerReceiver` (una línea). No hace falta tocar la escena. |
| `Assets/FrankaVR/V6/VR/VRTrainingHUDV6.cs` | Fuera de la práctica, el HUD muestra "Mando celular: abrí http://IP:8080 en el otro celular" (mientras no haya un mando conectado). |
| `Assets/Editor/FrankaVRProjectChecks.cs` | La verificación *Mando Wi-Fi (UDP)* ahora también prueba el mando celular por HTTP: la página, el rechazo de un paquete inválido, que tome el mando, que mueva la torcha y que se libere al cortar. |

## 3. Puesta en marcha

### Red (una de estas)
1. **Hotspot (recomendado para la demo, no depende del Wi-Fi de la facultad):** el celular B comparte internet (zona Wi-Fi) y el celular A se conecta a esa red.
2. **Mismo router Wi-Fi:** los dos celulares en la misma red. Algunas redes de facultad aíslan a los clientes entre sí ("client isolation"): en ese caso usar la opción 1.
3. **Prueba en la PC:** Unity en Play en la PC y el celular B en la misma red que la PC. La primera vez, Windows pregunta por el firewall del Editor de Unity: permitir en **redes privadas**. Si no aparece el cartel o se rechazó, abrir el puerto en PowerShell como administrador:
   `New-NetFirewallRule -DisplayName "FrankaVR mando celular 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow -Profile Private`

### Pasos
1. Abrir la app en el celular A (o Play en el Editor). Antes de ponerlo en el visor, leer en el HUD la dirección, por ejemplo `http://192.168.43.120:8080`. También queda en el log: `[Mando celular] Abrir en el otro celular: ...`.
2. En el celular B, abrir esa dirección **escribiendo `http://`** en Chrome o Safari.
3. Tocar **Pantalla completa** (en Android además fija la orientación horizontal). Arriba tiene que decir **"Conectado · Entrada: Mando"** en verde.
4. Poner el celular A en el Cardboard. Al terminar, el operario mira hacia adelante y toca **RECENTRAR**.

## 4. Controles

Disposición en horizontal: la palanca bajo el pulgar izquierdo y el GATILLO bajo el pulgar derecho (multitáctil: se pueden usar los dos a la vez).

| Botón en el celular | Campo CTL | Qué hace (igual que el mando ESP32) |
|---|---|---|
| Palanca (analógica) | jx, jy | Mueve la torcha (derecha y adelante según la vista). Grabando: avanza sobre la junta, más palanca = más rápido. En MOVERSE: camina. |
| ALTURA (interruptor, amarillo) | altura | Con ALTURA prendida, la palanca arriba/abajo sube y baja la torcha (grabando: distancia a la pieza). Equivale a la palanca apretada del ESP32. |
| GATILLO (mantener) | gatillo | Grabar la trayectoria (como Espacio). Al soltar se corta. |
| ACERCAR | acercar | La torcha va sola al inicio de la junta (B). |
| RETIRAR | retirar | Aleja la torcha (C); durante la ejecución, aborta. |
| OK | ok | Activa el botón que se está mirando; si no se mira ninguno y hay trayectoria, ejecuta (Enter). |
| DESHACER | deshacer | Borra el último tramo (Z). |
| BORRAR (mantener 0,9 s) | borrar | Borra toda la trayectoria (R). Con una pulsación corta no hace nada. |
| VISTA | vista | Cambia el punto de vista. Equivale a ACERCAR largo del ESP32. |
| RECENTRAR | calibrar | Recentra el Cardboard. Equivale a OK largo del ESP32. |

Las acciones (ACERCAR, OK, etc.) se mandan como un pulso de unos 180 ms y al menos 2 paquetes, igual que el firmware: así un paquete perdido no las borra. Unity actúa en el flanco de subida.

**Sin IMU:** el celular B no orienta la torcha (en la línea CTL va `imu=0`). Los navegadores solo dan los sensores de orientación con HTTPS, y la app sirve HTTP. La orientación queda como con el teclado (la guía de costura fija los ángulos). Ver pendientes.

## 5. Seguridad y comportamiento ante fallas

- Si la app no recibe nada durante **0,35 s** (`HandControllerInput.TimeoutSec`), suelta el gatillo y la palanca y vuelve al teclado. Pasa si se cae el Wi-Fi, si se bloquea el celular B o si se cambia de pestaña. Además, la página suelta todo al pasar a segundo plano.
- **Un solo celular manda a la vez.** Si otro celular intenta mandar mientras el primero sigue activo, el servidor lo rechaza durante 2 s (`OwnerTimeoutSec`) y la página muestra "Rechazado: otro celular tiene el mando".
- Cada envío espera como máximo 1,5 s: con Wi-Fi inestable, un pedido colgado no frena a los siguientes. Si pasan 2 s sin respuesta, la página muestra "Sin respuesta de la app · revisá el Wi-Fi" y se recupera sola, sin recargar.
- El teclado sigue funcionando siempre (entrada híbrida). El mando ESP32 por UDP también: si los dos mandan a la vez, se mezclan. No usar los dos a la vez.

## 6. Estado (no exagerar)

| Ítem | Estado |
|---|---|
| Servidor, página, integración en el puente y HUD | **Implementado** |
| Compilación | **Comprobada** el 26/11 con el compilador de Unity 6000.5.10f1 (Roslyn de `Editor/Data/DotNetSdk`) contra las DLL del motor: runtime + Editor, 0 errores. **No** es una compilación dentro del Editor. |
| Servidor + página de punta a punta | **Probado** fuera de Unity: el `.cs` real con stubs mínimos de UnityEngine y la página abierta en Chromium headless. Palanca, gatillo, los 6 pulsos, ALTURA, BORRAR corto (no borra) y largo (borra): llegaron las líneas CTL correctas, a ~22 por segundo. Se ve bien en horizontal y en vertical. |
| Verificación *Mando Wi-Fi (UDP)* con la parte HTTP | **Escrita, no ejecutada.** Unity en modo batch dio "No valid Unity Editor license found" (hay que iniciar sesión en el Hub). Correrla desde el menú *FrankaVR › Verificacion › Mando Wi-Fi (UDP)*. |
| Celular A real (build Android/Cardboard) + celular B real | **Pendiente.** No probado en dispositivos. |

## 7. Problemas comunes

| Síntoma | Causa probable / solución |
|---|---|
| El celular B no abre la página | Distinta red, o red con aislamiento de clientes → usar hotspot. Escribir `http://` (no `https://`). En la PC: firewall (sección 3). |
| El HUD muestra `http://IP-DEL-CELULAR:8080` | La app no encontró su IPv4. Buscarla en Ajustes → Wi-Fi → red actual del celular A. |
| "Conectado" pero el robot no se mueve | Mirar si arriba dice `Entrada: Mando`. Si dice `Guante BLE`, hay paquetes IMU del guante, que tienen prioridad. Revisar también que la práctica esté iniciada (OK sobre el botón del menú). |
| Se corta a ratos | Wi-Fi débil o ahorro de energía del celular B. Mantener la pantalla encendida (subir el tiempo de bloqueo). |
| Puerto 8080 ocupado (Editor) | Otro programa en la PC usa el 8080. Cambiar `Port` en el componente `PhoneControllerServer` (en Play) o en el código. |

## 8. Para quien siga (reglas)

- **No duplicar** receptores ni puentes: todo entra por `GlovePacketBridge.OnBleMessage` con el protocolo CTL. Si se agrega un campo a CTL, cambiar a la vez el firmware, `GlovePacketBridge.ParseController` (exige 17 campos), `HandControllerInput.OnPacket`, `lineaCTL()` en la página y `Ctl()` en las verificaciones.
- La página se edita en `Resources/MandoCelular.html` (no hace falta recompilar, pero sí un nuevo build para el celular).
- No mover la deposición ni la IK por cuestiones estéticas (`AGENTS.md`).
- **Ideas pendientes (en orden de valor):**
  1. Probar en dos celulares reales y medir la latencia.
  2. Mostrar en la página el estado de la práctica (fase, puntos grabados): el servidor ya responde un JSON con `estado` en cada envío.
  3. Orientar la torcha con el giroscopio del celular B: necesita HTTPS (certificado autofirmado), o una app nativa que mande la misma línea CTL por UDP al puerto 4211, cosa que `UdpControllerReceiver` ya acepta de cualquier IP.
  4. QR en el HUD con la dirección (como `qr_hmi.png` del HMI de la CAVE).
