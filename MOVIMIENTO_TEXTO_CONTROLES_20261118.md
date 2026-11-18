# Movimiento más suave, texto nítido y panel de CONTROLES (18/11/2026)

Hecho sin acceso a Unity. El código compila con `mcs` contra stubs de la API (PC y Android), pero **todavía no se probó en Unity**: primero hay que correr las verificaciones de la sección 4.

## 1. Movimiento del robot
### Por qué se movía "tosco"
1. **Rebote al frenar.** El servo (`PandaIK`) sumaba el error completo en cada paso de física. Con el retardo del drive del brazo (amortiguador/resorte ≈ 0,1 s), esa cadena queda subamortiguada: se pasa al frenar, rebota y oscila dentro de la tolerancia.
2. **Saltos de imagen.** La física corría a 50 Hz y el juego está limitado a 60 fps (`MobileVRQuality`). Uno de cada 6 cuadros el brazo no se mueve: se ve como un tironeo periódico.
3. **Gravedad sin compensar.** Con gravedad, el drive del brazo cede algunos milímetros y la IK lo va corrigiendo a los tirones. El Panda real compensa la gravedad en su propio controlador.

### Qué cambió
| Archivo | Cambio |
|---|---|
| `PandaIK.cs` | `smoothServo`: control por velocidad con prealimentación (`SetTargetVelocity`) y ganancias por segundo (`positionGain` = 8, `orientationGain` = 8). Los valores no dependen de la frecuencia de física; el servo viejo también se reescaló a 50 Hz equivalentes. |
| `PandaArm.cs` | `gravityCompensation` (sin gravedad en los eslabones). `SetTargetDeg(i, grados, velocidad)` adelanta el objetivo del drive `damping/stiffness · v`, así el brazo sigue la referencia sin atrasarse ni pasarse. Todo va en grados, sin ambigüedad de unidades. |
| `WeldingTeleopController.cs` | Le pasa a la IK la velocidad suavizada de la punta. |
| `TimeManager.asset` | Física a **120 Hz** (`Fixed Timestep` 0.008333): exactamente 2 pasos por cuadro a 60 fps y 1 a 120 Hz. `Maximum Allowed Timestep` en 0.1. |

### Resultado en simulación (1 junta, drive real 10000/1000, 60 fps)

| Caso | Antes (50 Hz) | Ahora (120 Hz) |
|---|---|---|
| Avanzar a 60 mm/s y frenar | Se pasa 1,3 mm, rebota y no termina de asentarse | No se pasa; se asienta en 0,2 s |
| Avanzar a 150 mm/s y frenar | Se pasa 2,1 mm, 2 rebotes | Se pasa 0,2 mm, sin rebote |
| Salto de 20 mm | Se pasa 3,7 mm, 2 rebotes | Sin sobrepaso ni rebote |
| Velocidad mientras avanza | Desvío de 1,9 mm/s | Desvío de 0,2 mm/s (pareja) |

El resultado es casi igual con inercias de 0,1 a 2 kg·m² y ganancias de 4 a 12.

**Volver atrás si algo sale mal:** destildar `PandaIK.smoothServo` y `PandaArm.gravityCompensation`, y en *Project Settings › Time* poner `Fixed Timestep` 0.02.

## 2. Texto nítido
- **Shader nuevo `Resources/FrankaVRUIText.shader`:** promedia 4 muestras por píxel. La fuente dinámica no tiene mipmaps y el texto chico se veía borroso o serruchado. También se aplicó a los carteles de la nave (`FrankaVRText3D`).
- **`CrispTextV6`:** todo el texto de HUD, menú y botones se rasteriza a 64 px y se dibuja con ese material, ocupando el mismo tamaño que antes.
- **Cardboard (`MobileVRQuality`):** escala de render por ojo 1,25 y MSAA 4x (solo Android).
- **En la PC:** el Game view tenía **Scale 1.3x**, que agranda la imagen y la vuelve borrosa (la rueda del mouse sobre el Game view la cambia). Ponerla en **1x** y usar *Play Maximized* o una resolución fija, por ejemplo 1920×1080.

## 3. Panel de CONTROLES
Es un documento grande delante de la cámara con una tabla **Acción / Teclado / Mando**: mover, enseñar, ejecutar, y vista y menú.

- **Abrir:** botón **CONTROLES** en el menú (abajo); botón **CONTROLES** en el HUD (reemplaza a AYUDA); tecla **F1**.
- **Cerrar:** **CERRAR** (mirarlo 1 s o mirarlo + OK), **F1** o **Esc**. En el mando, OK sin mirar ningún botón también lo cierra.
- **HUD y menú:** mientras el documento está abierto se ocultan, así no se mezcla el texto. La ayuda vieja del menú (lista de teclas) se reemplazó por este documento.

## 4. Pruebas pendientes (en Unity)
1. Dejar compilar y revisar que la consola no tenga errores.
2. Correr en *FrankaVR › Verificacion*:
   - **Ensenar y ejecutar M1**
   - **Control de punta**
   - **Integracion N1-N5** (ahora también abre y cierra CONTROLES y captura `06_controles`)
   - **Mando Wi-Fi (UDP)**
3. Mover a mano con WASD: arrancar, frenar y cambiar de dirección no debería rebotar. Revisar también que en la pausa el brazo no derive.
4. Revisar `Verificacion/Capturas/06_controles.png` y el texto del HUD con Scale 1x.
5. Si algún umbral de las pruebas falla por el cambio de servo, ajustar `positionGain` (6-10) antes de tocar otra cosa.
