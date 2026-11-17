# FrankaVR — Contexto para continuar en Codex

**Actualizacion posterior del 10/10/2026:** se corrige la retirada en practica para que la asistencia no contrarreste al operador, aun con Espacio mantenido. El componente visual existente incorpora detalles de nave industrial sin colliders. Ver `RETIRADA_Y_TALLER_20261010.md`.

**Actualizacion 10/10/2026:** el control de teclado ahora trabaja sobre la punta consumible, con velocidades menores, proteccion por superficies y B para aproximacion asistida en practica. La ayuda regula el arco medido segun el electrodo y centra sobre la costura; examen conserva control manual sin esa asistencia. Ver `CONTROL_SOLDADURA_20261010.md` para controles y pruebas reales, y `MEJORAS_FRANKAVR_20261009.md` para el resto de mejoras verificadas. Las afirmaciones historicas de falta de pruebas en este documento corresponden a la transferencia inicial. BLE y Cardboard en telefono siguen pendientes de prueba fisica.

**Fecha de transferencia:** 9 de octubre de 2026  
**Objetivo académico:** simulador didáctico de soldadura SMAW teleoperada con brazo Franka Panda de 7 ejes, Unity, Google Cardboard en Android y un mando físico con IMU/ESP32. **Fecha de presentación/examen prevista: 26 de octubre de 2026.**  
**Carpeta local conocida (Windows):** `D:\01. Facultad\RV\FrankaVR` (verificar en esta máquina).  
**Este documento resume decisiones y pruebas conversadas; el proyecto Unity que está en disco es la fuente de verdad sobre los scripts y el estado real.** No se ha verificado la compilación o ejecución de ese proyecto desde Codex.

## 1. En qué consiste el proyecto

- El alumno se coloca Google Cardboard con un teléfono Android Poco X7 Pro, observa una celda de taller con Franka Panda y controla su soldadura SMAW.
- Para desarrollo se usa teclado/ratón en PC. Para la presentación se prevé un mando físico sobre MDF, con mango móvil que lleva un IMU MPU-6500 y panel de botones fijo alimentado por USB desde ESP32. La idea de **guante wearable con batería se descartó por simplicidad y tiempo**.
- Control del robot: orientación de mano (cuaternión de IMU) determina orientación/dirección de electrodo; traslación por velocidad con embrague o joystick opcional; un PandaIK jacobiano DLS resuelve 7 articulaciones.
- El sistema contiene entrenamiento, ejercicios N1–N5, medición/calidad de arco, consumo de electrodo, cordón, efectos, mesa/taller, HUD y cambio de cámaras/estaciones.

## 2. Estado comunicado por el usuario (confirmado en conversación)

- El robot llegó a moverse con teclado y el arco llegó a encenderse y soldar con audio/chispas.
- Se corrigió un error de longitud del arco por usar la posición del soporte en vez de la punta física consumible. Con la corrección se logró llegar a menos de 40 mm y soldar.
- `ArcLenMm` aumenta durante soldadura porque la varilla se consume; `R` repone varilla. Se iteró mucho en su visualización. **No dar por demostrado que se acorta visualmente en la escena final sin probarlo.**
- Se instalaron variantes sucesivas v2/v3/v4/v5/v6/v6.1/v6.2, con conflictos de clases duplicadas, en especial `ViewpointManager`, `BleGloveInput` y `WorldBillboardY`; finalmente el usuario informó que volvía a funcionar tras limpiar duplicados.
- El usuario mostró una escena con mucho texto encimado: HUDs/paneles antiguos y nuevos simultáneos. Se entregó parche v6.2 que deshabilita interfaces antiguas, **pero no hay confirmación visual posterior de que haya quedado limpio**.
- V6 agregó locomoción de depuración en PC y mejoras de cordón/visual, pero también introdujo un fallo de `Destroy()` en Editor; v6.1 lo corrigió. En móvil se debe evitar locomoción continua.
- **BLE Android real no está conectado ni probado**: el bridge está preparado pero no hay plugin Android GATT ni hardware físico final.
- No hay confirmación de build Android/Google Cardboard funcional en teléfono; scripts/estructura no equivalen a integración XR completa.

## 3. Piezas clave del software y restricciones críticas

### Robot e IK
- `PandaArm`: 7 `ArticulationBody`; home aproximado `{0,-45,0,-135,0,90,45}`.
- `PandaIK`: jacobiano 6×7 con DLS; controla posición y **dirección** de aproximación del electrodo; el giro axial es irrelevante. Hace inferencia geométrica de eje entre `toolReference` y `endEffector` porque ejes URDF pueden variar.
- **NO cambiar `PandaIK.endEffector` para apuntar a `Tip`/`ArcTip`. Debe seguir en el objeto lógico `Soldador` ya calibrado.** Hubo un cambio accidental que hizo fallar la cinemática.
- `WeldingTorch.Tip` es la referencia lógica de herramienta para medir; `ArcOriginPosition` es la punta física consumible. No confundir ambos.
- `WeldingTeleopController`: mantener desactivado por defecto el antiguo clamp inferior de altura que impedía bajar hasta la chapa; ver variable de versión (`ClampHeight=false` en correcciones).

### Arco y soldadura
- `WeldingTorch`: medición por raycast desde la punta física en dirección del electrodo, ignorando colliders del robot pero **no** la pieza `Chapa`; longitud máxima operativa usual 40 mm, rango de medición mayor.
- Inicialmente daba ~360 mm por medir desde porta-electrodo, ~209 mm tras cambios visuales y luego 9–12 mm al calcular punta real; se comprobó arco y soldadura.
- `RemainingMm` cae mientras el arco está activo; el electrodo tiene largo inicial usual de ~350 mm. Reposición con `R` y selección de tipo con `Tab` (si teclado mantiene esos mapeos).
- **`WeldingTaskManager` debe depositar usando `Torch.ArcOriginPosition`, NO `Torch.Tip.position`.** Verificar en versión efectiva.
- `WeldSeam` modela costuras rectas o circulares y calidad/temperatura; `WeldBeadRenderer` dibuja malla. Se iteró en ancho/altura del cordón, que el usuario aún consideraba poco realista.
- `WeldingFX`, `WeldingHelmet`, `WeldingMachine`, `WeldingEnvironmentPolish`, `WeldArcVisualEnhancerV5/V6` aportan efectos y ambiente. Evitar duplicación de objetos/luces.

### Controles de desarrollo en PC
- Robot, teclado: `W/S` adelante/atrás, `A/D` lateral, `Q/E` bajar/subir; `Space` arco. Puede haber teclas `I/J/K/L/U/O` para orientación, verificar en código.
- Recarga: `R`; cambiar tipo: `Tab`.
- V6 locomoción de **la cámara/cuerpo virtual** para depuración: **Flechas** desplazamiento horizontal, **RePag/AvPag** vertical, **Shift** más rápido, **botón derecho del ratón** mirar. No usar WASD para cámara: reservado al robot.
- `HybridTeleopInput`: prioriza teclado sin IMU conectada y conmuta a `BleGloveInput` cuando hay datos IMU recientes. Se produjo un error anterior por establecer solo `BleGloveInput`, dejando teclado inutilizado. Verificar vínculo `WeldingTeleopController.InputSource` al híbrido.
- Cardboard: giroscopio/head tracking para mirar (**3DoF**, no traslación espacial real); estaciones de vista operador/lateral/isométrica con fundido, desplazando rig padre y **sin sobrescribir rotación de la cámara XR**.
- Scripts V5: `CourseTrainingManagerV5`, `TrainingWorkpieceFactoryV5`, `CourseWorldMenuV5`, `VRTrainingHUDV5`, `DesktopHeadLookV5`, `WorkshopEnvironmentV5`, `WeldArcVisualEnhancerV5`, `MobileVRQuality`/módulos anteriores.
- Scripts V6: `DesktopNavigatorV6`, `VRTrainingHUDV6`, `CourseWorldMenuV6`, `WeldBeadRendererV6`, `WorkshopEnvironmentV6`, `WeldArcVisualEnhancerV6`. V6.2 agregó lógica de limpieza de UI. **No crear versiones V7/V8 automáticamente ni clonar clases existentes. Consolidar.**
- `ViewpointManager` correcto debe exponer `GoTo`, `SnapTo`, `EnsureRigRoot` y `rigRoot`. Existieron dos copias en `Assets/Scripts/ViewpointManager.cs` y `Assets/VR/ViewpointManager.cs`; quedaron errores tanto por duplicados como por conservar versión obsoleta.

## 4. Curso y ejercicios

- `WorkOrder.Catalog` define N1: cordón en placa; N2: junta a tope en chapa; N3: T/filete; N4: solape; N5: caño a tope (costura circular, radio aprox. 0.06 m).
- Menú selección → instrucciones → entrenamiento/examen → resultado. En V5 PC: 1–5 elegir, `T` entrenamiento, `Y` examen, `F` finalizar, `M` volver al menú.
- `TrainingWorkpieceFactoryV5` genera primitivas + costuras y oculta `Chapa` antigua según ajuste. Revisar collider de la pieza y ubicación de puntos de costura en cada ejercicio, sobre todo circular y en T.
- `WeldingTaskManager` ya calcula métricas: puntuación, puntos correctos/defectos, arco promedio y desviación, cortes, pegadas, varillas, tiempo. No es necesario rehacer algoritmo de puntuación sin motivo.
- **Problemas visuales pendientes:** solapamiento de distintos HUD, texto gigante a la derecha, paneles verdes antiguos, legibilidad de menús, cordones poco naturales y sincronización del desgaste visual de varilla. Revisar escena antes de cambiar.

## 5. Diseño de hardware actual: priorizar prototipo simple para 26/oct

**Concepto decidido más recientemente:** base de MDF (aprox. 30×20 cm) sobre escritorio + ESP32 + 5 botones + joystick opcional. IMU en un **mango móvil conectado por cable**; si la IMU se fija a la madera no medirá la orientación de mano. Se alimenta ESP32 por micro-USB: no batería, no guante complicado, no PCB propia.

**Materiales disponibles/previstos por usuario**:
- ESP32 DevKit clásico con puente USB CP2102, probado por puerto COM5 de su PC anterior.
- MPU-6500 GY-6500 6 ejes, pendiente compra/recepción en ese momento.
- Cinco pulsadores Big Key 12×12, opcional KY-023, cables Dupont, protoboard, madera base, USB.

**Pinout propuesto (a verificar con placa física y módulo):**
- IMU I2C: SDA GPIO21, SCL GPIO22, VCC 3V3, GND GND; INT opcional GPIO32.
- Botones `INPUT_PULLUP`, al GND: chispa GPIO25, clutch GPIO26, amperaje+ GPIO27, amperaje− GPIO14, electrodo GPIO33.
- Joystick opcional alimentado a 3V3: VRx GPIO34, VRy GPIO35, SW GPIO23. GND común.
- Comportamiento botón: chispa y clutch mantenidos; amperaje ± por toque; electrodo corto reponer, largo cambiar tipo (propuesta a validar). Joystick SW opción careta/vertical. La distribución física se decidirá al armar el mando.
- Fusión orientación: MPU-6500 acelerómetro+gyro con Mahony/Madgwick en ESP32, en vez de depender de librería DMP MPU-9250 en ESP32 sin validación. Sin magnetómetro: deriva yaw, contemplar recentrado.
- BLE: un servicio NOTIFY para estado, WRITE para comandos. Se discutieron dos formatos: CSV IMU/BTN ya presente en Unity y **binario 20 bytes como propuesta futura, NO implementado ni acordado definitivamente**. Evitar inventar compatibilidad entre ambos.
- Android: puente nativo BluetoothGatt pendiente; permisos BLUETOOTH_SCAN/CONNECT en Android 12+ y lifecycle; chequear UUID/formato cuando firmware exista. BLE no implica automáticamente comunicación lista con Unity.

## 6. Flujo recomendado para Codex

1. Abrir **la raíz del proyecto Unity real**, no un ZIP histórico. Inspeccionar primero `Assets/`, escenas/prefabs (`.unity`, `.prefab`), scripts, `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, configuración XR y editor assembly definitions.
2. **No instalar automáticamente paquetes históricos**. Hacer inventario de clases duplicadas, scripts activos y referencias en escena; distinguir versiones antiguas y nuevas.
3. Crear copia de seguridad o usar Git con commit inicial. **No editar `.meta` a ciegas** ni cambiar GUID de scripts ya referenciados.
4. Identificar y documentar estado actual y errores reproducibles; priorizar compilar sin errores.
5. Resolver HUD único y limpieza visual en escena, pieza/costura y locomoción PC sin afectar robot.
6. Definir flujo de hardware mínimo con los componentes existentes y escribir firmware modular/comprobable solo tras validar pinout exacto y formato de paquetes, pensando en fecha 26/oct.
7. Probar en Unity/Android cuando herramientas estén disponibles. No afirmar pruebas reales o compilación si no fueron ejecutadas.

## 7. Qué NO hacer

- No poner `PandaIK.endEffector` en `Tip` / `ArcTip`.
- No volver a cambiar fuente híbrida de input por BLE exclusiva.
- No introducir scripts con clases idénticas en diferentes rutas ni acumular parches V7/V8 sin consolidación.
- No dibujar dos varillas, dos cordones, varios paneles HUD o varias luces de arco simultáneos.
- No hacer movimiento libre de cámara en Cardboard; sí permitirlo en PC como herramienta de depuración.
- No usar `Destroy` en modo Editor (usar `DestroyImmediate` cuando corresponda; proteger estados).
- No omitir backup ni romper `.meta` o referencias de escena.
- No asumir que el contenido de esta síntesis sustituye los scripts locales.

## 8. Próxima tarea sugerida

**Primero una auditoría sin modificar archivos:** inspeccionar el proyecto Unity local, encontrar scripts realmente en uso, duplicados y componentes de UI activos en escena, dependencias Cardboard/BLE, errores potenciales y riesgos previos. Proponer un plan mínimo de estabilización con prioridad al examen del 26 de octubre. Después, aplicar cambios en el repo, pequeños y verificables, mostrando diffs y pidiendo pruebas de Unity cuando sean necesarias.
