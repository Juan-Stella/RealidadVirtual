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

## 4. Aplicado y probado en la PC (19/11)
Rama `mejoras-movimiento-20261119`. Todo compilado y probado en Unity 6000.5.10f1 (Editor, no en el celular).

**Física por cuadro (`FramePhysicsDriver`).** El temblor venía de que Unity hacía 1, 2 o 3 pasos de física por cuadro según el momento, y el ArticulationBody no interpola. Ahora la física se simula una vez por cuadro (en subpasos de ≤ 1/120 s) y el servo de `PandaIK` corre antes de cada subpaso (`ServoStep`). `PandaIK` crea el driver solo si no existe; sin driver vuelve a `FixedUpdate`.

**Suavidad medida** (*Verificacion › Suavidad del movimiento*, `Verificacion/Suavidad.txt`):

| | Crucero | Desvío entre cuadros | Sobrepaso al frenar | Rebotes | Quieto en | Giro 20° (máx / final) |
|---|---|---|---|---|---|---|
| anterior | 151 mm/s | 77.1 | 2.8 mm | 2 | 0.51 s | 21.1° / 20.3° |
| nuevo | 148 mm/s | 1.8 | 0 mm | 0 | 0.40 s | 20.0° / 20.0° |

**Torcha.** La brida del Panda queda 154 mm detrás del TCP; se agregaron adaptador (aluminio), sensor anticolisión y brazo de torcha que llenan ese tramo (*FrankaVR › 14 - Montar torcha MIG*). El TCP y la IK no cambian. El mismo paso baja la luz del arco (intensidad 6 → 2.5, alcance 2.5 → 1.2 m) porque quemaba el brazo blanco.

**Acercar (B) y ejecutar.** La aproximación terminaba con la punta hasta 5 mm por encima de la distancia de arco; ahora la última etapa espera 2 mm (con 2 s de margen). Al ejecutar, el arco se ceba recién con la punta sobre el primer punto y la espera de arranque cuenta con el arco ya establecido. Resultado en M1: 45 → 50 de 60 puntos correctos. Los fríos que quedan están solo en los extremos, y el consejo del resultado ahora lo dice ("graba desde el inicio de la junta hasta el final") en vez de pedir menos velocidad.

**Paneles.** El documento CONTROLES y el HUD usan `FrankaVR/UIPanel` (fondo encima de la escena, como el texto) y se dibujan después de vidrios y brillos del taller. Antes el robot y el vidrio del cerco atravesaban el documento, y en el HUD los botones FIN y CONTROLES quedaban sin fondo detrás de la mesa.

**Pruebas que pasan:** Ensenar y ejecutar M1, Control de punta, Integracion N1-N5, Mando Wi-Fi (UDP), Suavidad del movimiento.

**Pendiente:** build Android y prueba en el Cardboard (texto, 1.25x de resolución, MSAA 4x, suavidad con el giro de cabeza).
