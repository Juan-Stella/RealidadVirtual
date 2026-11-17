# FrankaVR — actualización y traspaso a otra IA

> **Actualización posterior:** este traspaso fue redactado antes del trabajo de Claude incorporado en `52fcd48`. Para el estado actual, leer primero `CONTROL_Y_PORTAELECTRODO_20261117.md` y `README.md`: hay correcciones de porta-electrodo, control, HUD y entorno, y pruebas de Editor posteriores aprobadas. Las advertencias sobre tareas incompletas de abajo describen el estado anterior.

**Fecha de este documento: 17 de noviembre de 2026.**

Este archivo está pensado para adjuntarlo completo a otra IA, aunque ya haya trabajado antes en FrankaVR. Es una actualización del proyecto desarrollado principalmente con Codex, con antecedentes, estado actual, problemas y próximos pasos.

**Fuente de verdad:** el proyecto Unity real en disco. Este documento combina la conversación con una revisión de scripts, escena, configuración y registros existentes. No se ejecutó nuevamente Unity para redactarlo.

**Advertencia principal:** los scripts más recientes contienen un operario y cambios de recuperación del robot que todavía no tienen una validación final completa. El último ejecutable Windows validado es anterior a esos cambios. No confundir código escrito, prueba parcial y versión terminada.

---

## 1. Resumen del proyecto y objetivo del usuario

FrankaVR es un simulador didáctico de soldadura SMAW, con un brazo robótico Franka Panda de siete articulaciones. El usuario controla el desplazamiento y la orientación de la herramienta, enciende el arco y realiza ejercicios de soldadura.

Tiene dos contextos de uso:

- **PC:** desarrollo y práctica con teclado y ratón, sin depender de hardware externo.
- **Android + Google Cardboard:** observación inmersiva con movimiento independiente de cabeza y, a futuro, un mando físico con ESP32 e IMU.

El sistema incluye taller, robot, herramienta con electrodo consumible, medición del arco, cordón, efectos, ejercicios N1–N5, práctica/examen, evaluación y exportación de resultados.

La prioridad expresada por el usuario es que resulte **estable, fácil de manejar y visualmente presentable**. La precisión del control y la posibilidad de acercarse, soldar y retirarse sin atascarse importan más que sumar funciones aisladas. Actualmente no hay una validación física de hardware que permita orientar el desarrollo alrededor del BLE real.

La fecha de evaluación originalmente indicada fue el **26/10/2026**. Esa fecha ya pasó al redactar este documento: es un antecedente, no un nuevo plazo confirmado. Consultar al usuario si hace falta planificar una entrega con fecha.

## 2. Ubicación y entorno técnico

Raíz local:

```text
D:\01. Facultad\RV\FrankaVR
```

Configuración comprobada en archivos:

| Elemento | Estado |
|---|---|
| Unity | `6000.5.10f1` |
| Escena de trabajo | `Assets/Main.unity` |
| Escena habilitada en Build Settings | `Assets/Main.unity` |
| Otra escena existente | `Assets/Scenes/SampleScene.unity`; no tomarla como escena principal |
| Entrada | Both, `activeInputHandler: 2` |
| Renderizado usado | Built-in; no se migró a URP/HDRP |
| Input System | `com.unity.inputsystem: 1.20.0` |
| Cardboard | Dependencia Git `com.google.xr.cardboard` en `Packages/manifest.json` |
| Robot importado | URDF Importer, también como dependencia Git |
| XR | Assets en `Assets/XR/`, con configuración diferenciada de Android y Standalone |

Unity instalado durante el trabajo:

```text
C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe
```

El repositorio tiene numerosos cambios sin commit y archivos nuevos sin seguimiento. No usar `git reset`, `git clean`, checkout de archivos completos o borrados masivos para “ordenarlo”: se perderían cambios reales del usuario y de sesiones anteriores.

Hay carpetas históricas de parches V4/V5/V6 y backups fuera de `Assets`. No son instalaciones pendientes. No copiarlas encima de la versión actual.

## 3. Restricciones que evitan repetir fallos graves

Leer también `AGENTS.md` antes de editar.

1. **`PandaIK.endEffector` debe seguir apuntando al objeto lógico `Soldador`.** No cambiarlo a `Tip`, `ArcTip` ni a la punta física: una modificación anterior de ese tipo rompió la cinemática calibrada.
2. **No reactivar el límite vertical global.** En la escena, `ClampHeight` está desactivado; ese límite había impedido alcanzar la pieza.
3. Preservar archivos `.meta`, GUID y referencias de escenas/prefabs. No reemplazar scripts por archivos con GUID nuevos.
4. Mantener el teclado funcional sin BLE y conservar la entrada híbrida. No conectar el controlador exclusivamente al guante.
5. La deposición debe salir de **`WeldingTorch.ArcOriginPosition`**, que es la punta consumible. `WeldingTorch.Tip` y el TCP lógico no son equivalentes a esa punta.
6. No introducir versiones V7/V8 duplicadas de clases ya existentes. Consolidar los scripts actuales.
7. No agregar otro HUD encima de los existentes. Gestionar el panel actual y la limpieza de UI antigua.
8. Flechas para la cámara; WASD/QE para el robot. En Cardboard, mirar debe seguir siendo independiente del movimiento del robot.
9. No alterar física de soldadura, puntuación o IK solo para mejorar el aspecto visual. Los cambios funcionales de control deben estar justificados y probados.
10. Antes de cambios importantes, hacer backup verificable. No afirmar compilación, Play, APK funcional o BLE probado sin evidencia real de esa versión.

El usuario autoriza proponer y aplicar un enfoque de control mejor, aunque no sea idéntico al actual. Eso no equivale a autorizar cambios arbitrarios de TCP, borrado de referencias o pérdida del teclado.

## 4. Qué está realmente conectado en Main

La revisión de los componentes habilitados y de sus GUID muestra este conjunto principal:

| Función | Archivo relativo a la raíz |
|---|---|
| IK del robot | `Assets/Scripts/PandaIK.cs` |
| Articulaciones y drives | `Assets/Scripts/PandaArm.cs` |
| Control de herramienta/objetivo | `Assets/Scripts/Welding/WeldingTeleopController.cs` |
| Teclado del robot | `Assets/Scripts/Welding/KeyboardTeleopInput.cs` |
| Comando de entrada común | `Assets/Scripts/Welding/TeleopInput.cs` |
| Selección teclado/IMU | `Assets/FrankaVR/Input/HybridTeleopInput.cs` |
| Entrada del guante/mando | `Assets/Scripts/Welding/BleGloveInput.cs` |
| Herramienta, arco y consumo | `Assets/Scripts/Welding/WeldingTorch.cs` |
| Sesión, energía y métricas | `Assets/Scripts/Welding/WeldingTaskManager.cs` |
| Costura y muestras | `Assets/Scripts/Welding/WeldSeam.cs` |
| Modelo de proceso y catálogo | `Assets/Scripts/Welding/WeldingModel.cs` |
| Cordón visual | `Assets/Scripts/Welding/WeldBeadRenderer.cs` |
| Flujo del curso | `Assets/FrankaVR/V5/Course/CourseTrainingManagerV5.cs` |
| Generación de piezas | `Assets/FrankaVR/V5/Course/TrainingWorkpieceFactoryV5.cs` |
| HUD principal | `Assets/FrankaVR/V6/VR/VRTrainingHUDV6.cs` |
| Menú y ayuda | `Assets/FrankaVR/V6/Course/CourseWorldMenuV6.cs` |
| Limpieza de interfaces antiguas | `Assets/FrankaVR/V6_2/Runtime/SceneUICleanupV62.cs` |
| Navegación PC | `Assets/FrankaVR/V6/VR/DesktopNavigatorV6.cs` |
| Estaciones y rig | `Assets/VR/ViewpointManager.cs` |
| Taller y operario en ejecución | `Assets/Scripts/Welding/WeldingEnvironmentPolish.cs` |
| Guardado de resultados | `Assets/Scripts/Welding/WeldingSessionReport.cs` |

También están conectados `WeldingMachine`, `WeldingHelmet`, `WeldingFX` y `WeldingCable`.

`WeldingHUD` antiguo y `PipeTrainingWorkpiece` están deshabilitados en Main. Existen otros scripts V5/V6, pero su existencia no implica que estén en uso. El taller industrial actual se construye desde `WeldingEnvironmentPolish`; no hay que activar otro generador de taller para hacerlo aparecer.

La copia correcta de `ViewpointManager` está en `Assets/VR/`. La antigua de `Assets/Scripts/` aparece eliminada en Git como parte de una corrección anterior; no restaurarla automáticamente.

## 5. Funcionamiento y controles actuales

### Curso y ejercicios

Flujo: selección → instrucciones → práctica o examen → resultados.

- N1: cordón sobre placa.
- N2: junta a tope en chapa.
- N3: junta en T/filete.
- N4: solape.
- N5: junta circular en caño.

La fábrica genera piezas en ejecución y oculta la chapa antigua para evitar solapamientos. Usa 60 muestras para líneas y 84 para el círculo. N2 y N5 tienen superficies de raíz simplificadas para poder medir y depositar sobre la junta. No suponer que esta geometría didáctica equivale a un modelo físico de fusión volumétrica.

| Acción | Tecla o entrada |
|---|---|
| Elegir ejercicio | 1–5 |
| Iniciar práctica | T |
| Iniciar examen | Y |
| Finalizar | F |
| Volver al menú | M |
| Ayuda | F1 |
| Trasladar robot | WASD, Q baja, E sube |
| Orientar electrodo | I/K, J/L, U/O |
| Enderezar | V |
| Pedir arco | Mantener Espacio |
| Acercamiento asistido | B, solo en práctica |
| Retirada/recuperación nueva | C, implementada pero pendiente de validación completa |
| Fino | Ctrl |
| Desplazamiento rápido sin arco | Shift |
| Reponer electrodo | R |
| Cambiar tipo de electrodo | Tab |
| Amperaje | `[` y `]` |
| Careta | H |
| Girar caño en práctica N5 | Z/X, con arco apagado y electrodo libre |
| Reintentar guardado | G en resultados |
| Mover cámara | Flechas, RePag/AvPag |
| Mirar en PC | Botón derecho del ratón |
| Zoom | Rueda; Retroceso restaura |
| Cambiar estación | N/P |

**Conflicto detectado:** `ViewpointManager` también escucha C para recentrado de Cardboard mediante `CardboardRecenterProxy`. La nueva recuperación reutiliza C. Resolver ese doble atajo antes de dar el mapa de controles por definitivo.

En Main, el controlador del robot tiene `MoveSpeed = 0.3`, `OrientationWeight = 0.3`, radio mínimo 0.35 m y máximo 0.75 m. El teclado aplica multiplicadores: aproximadamente 30 mm/s normal, 6 mm/s con Espacio, 3 mm/s con Ctrl y 120 mm/s con Shift sin arco. La orientación normal es de unos 18°/s y la fina de 9°/s. No confundir `MoveSpeed` con la velocidad de navegación de cámara.

## 6. Evolución del control y bugs que motivaron cambios

### Problemas originales

- Era muy difícil colocar la punta a la distancia de cebado.
- El robot parecía querer hundirse en la mesa.
- La esfera del objetivo se movía demasiado rápido y a saltos para seguir una línea.
- Se confundían soporte/TCP lógico, punta nominal y punta física consumible.
- El desgaste cambiaba la distancia del arco sin que fuera sencillo compensarlo.

Se introdujo control cartesiano de la **punta física** para teclado: se mantiene una posición deseada de `ArcOriginPosition` y se convierte al objetivo del TCP lógico usando el desplazamiento medido entre ambos. No se reemplaza el `endEffector` del IK.

Se añadieron velocidad reducida, diagonales normalizadas, paso temporal acotado, suavizado mínimo de 80 ms, límite de adelanto manual de 25 mm y tolerancias finas de IK de 1 mm / 0.5° mientras está activo ese modo. Se restauran las tolerancias anteriores al desactivarlo.

### Aproximación y ayuda en práctica

B hace una aproximación por etapas: retirada inicial, colocación elevada junto a la costura y descenso. Su trayectoria se limita por espacio alcanzable e inclinación, con timeout. El ángulo recomendado deriva de normal, tangente y ángulos del ejercicio.

Con arco pedido cerca del trazo, la asistencia centra lateralmente y regula la distancia medida según el diámetro del electrodo. No avanza sola. Interpola segmentos de costura para no saltar entre muestras. En examen no se ofrece esta asistencia.

Se corrigieron dos errores de esa ayuda:

1. La separación respecto a la guía visual no coincidía con el arco real y dejaba el arco largo. Se pasó a usar la medición de la herramienta y el ideal del electrodo.
2. La corrección atraía la punta de nuevo cuando el usuario intentaba retirarse manteniendo Espacio. Ahora la orden de subir/apartarse tiene prioridad y suspende la asistencia hasta soltar Espacio.

### Última solicitud del usuario y cambios aún en curso

El usuario informó que el robot **seguía trabándose al chocar o acercarse a la mesa** y pidió una mejora significativa, autorizando cambiar el enfoque.

Los scripts más recientes añaden:

- `GuardSurface` con restricciones de proximidad y deslizamiento tangencial, en lugar de cancelar todo el desplazamiento al tocar una superficie.
- Detección de falta de avance durante una orden manual: `MotionBlocked`, `CheckForStall`.
- `PandaArm.HoldActualPose()`, que sincroniza y aplica los targets a la postura medida, para dejar de acumular una orden bloqueada.
- C / `BeginSafeRetreat()`: corta el arco y pide una subida progresiva de unos 80 mm, a 40 mm/s, con timeout y posibilidad de cancelar moviendo.
- Avisos en el HUD y actualización de la ayuda existente.

**Eso no es un planificador de colisiones de todo el robot.** La protección principal opera sobre el destino de la punta. C es una retirada vertical limitada, no una garantía de rescate desde cualquier postura. Todavía hay que revisar choques del codo, muñeca y soporte, singularidades, límites articulares y penetraciones iniciales.

## 7. Estado de pruebas y builds: leer esto antes de anunciar resultados

### Versiones anteriores con evidencia de éxito

- La integración general verificó referencias, flujo del curso, superficies, CSV de entrada, timeouts, cierre de sesión, UI y deposición.
- `Verificacion/Alcance_IK.csv` y `Verificacion/Soldadura_IK.csv` registran aproximaciones y arco en un punto de N1–N5, con el robot real de la simulación. No validan costuras completas ni todos los caminos.
- En esas pruebas hubo arco estable y energía positiva en los cinco ejercicios. N4 depositó poca energía: aproximadamente 24 J, con arco cercano a 8 mm. Requiere validar calidad y aproximación.
- La prueba posterior del control en N1 mantuvo arco de unos 3.3 mm y permitió retirada con y sin Espacio.
- `Verificacion/Unity_retirada_taller.log` terminó en PASS. Comprobó también que los adornos del taller no tuvieran colliders activos.

### Última prueba del operario y recuperación: INCOMPLETA

Archivo: `Verificacion/Unity_operario_control.log`, del 10/10/2026, posterior a las pruebas aprobadas.

Contiene mediciones parciales:

- Aproximación: arco de unos 4.14 mm.
- Avance: unos 15.05 mm, arco de unos 3.74 mm.
- Error al orientar: unos 0.72 mm.
- Retirada con arco pedido: unos 16.76 mm.
- Recuperación C: subida de unos 76.54 mm.

Después comienza una prueba de bloqueo controlado, deshabilitando temporalmente el componente IK. El registro termina con un error de conexión IPC del Package Manager y **no contiene el PASS/FAIL final de esa corrida**. No está determinada la causa de la interrupción ni demostrado un fallo del detector de bloqueo. Hay que repetirla y obtener un resultado concluyente.

**Trampa de los reportes:** `Verificacion/Resultado_Unity.txt` conserva un PASS con fecha anterior, del 10/10 a las 12:27. Ese archivo no prueba que la última versión de operario/recuperación haya pasado.

### Ejecutables existentes

| Plataforma | Última evidencia disponible | Alcance |
|---|---|---|
| Windows | `Succeeded`, 0 errores, 136948249 bytes; reporte del 10/10 a las 12:28 | Incluye taller y retirada anteriores. No es un build validado de la última tanda de operario/recuperación. |
| Android | `Succeeded`, 0 errores; APK de desarrollo ARM64 de 39457869 bytes, del 10/10 a las 10:56 | Anterior a las últimas mejoras de control/taller. No probado físicamente en teléfono. |

Rutas:

```text
Builds/FrankaVR_PC/FrankaVR.exe
Builds/FrankaVR_Android/FrankaVR.apk
Verificacion/Resultado_Build_PC.txt
Verificacion/Resultado_Build_Android.txt
```

Para Windows hay que conservar toda la carpeta del build, no solo el `.exe`.

## 8. Taller, operario e interfaz

### Taller ya mejorado

`WeldingEnvironmentPolish` genera en Play materiales, texturas pequeñas procedurales de hormigón/metal, bandas de seguridad, cubierta, vigas, luminarias emisivas, señalización, detalles del banco, perfiles en estantes, pliegues del biombo y detalles de la soldadora.

Los adornos se crean con colliders deshabilitados/eliminados. Comparten materiales y no se agregaron luces dinámicas para cada luminaria. El conjunto se limpia al salir de ejecución. No esperar ver toda esta escenografía en Edit Mode.

La captura `Verificacion/Capturas/10_taller_industrial.png` muestra el taller anterior a la última tanda. No demuestra que el operario esté validado.

### Operario nuevo

Existe `BuildWorker()` en `WeldingEnvironmentPolish`, con un personaje decorativo construido mediante primitivas, ropa de trabajo, chaleco, casco, gafas, protección auditiva y una tabla/clipboard. Se coloca aproximadamente en `(2.25, suelo, 2.65)`, fuera de la zona de trabajo, sin colisiones; tiene un movimiento leve de cabeza.

Es una figura estilizada, no un personaje humano realista importado ni un NPC interactivo. La revisión visual final quedó pendiente: el test que debía generar `11_operario.png` no llegó a completar esa parte. Revisar proporciones, pose, tamaño, iluminación y ubicación antes de darlo por terminado.

### Interfaz actual y mejoras razonables

Se conservaron un HUD y un menú principales, con limpieza de versiones anteriores. Hay botones por clic en PC y preparación para mirada/botón de visor en Android. F1 despliega la ayuda. El zoom intenta conservar el tamaño aparente de los paneles.

Propuestas para continuar, sin sumar otro HUD:

1. Priorizar tres datos durante la práctica: estado del arco, distancia y acción necesaria. Dejar métricas extensas en ayuda/resultados.
2. Mostrar explícitamente cuándo la asistencia está activa, suspendida por retirada o deshabilitada en examen.
3. Distinguir visualmente “punta actual”, “destino pedido” y marcadores de inicio/fin. Hoy la esfera y los extremos de guía pueden confundirse.
4. Dar controles visibles y coherentes para acercar, retirar y cancelar, una vez resuelto el conflicto de C.
5. Evitar que el panel tape la unión al acercarse o cambiar estación. Comprobar varias resoluciones, zoom y vistas, no solo una captura.
6. Revisar contraste y jerarquía de avisos. Los estados no deberían depender solo del color.
7. Diferenciar en resultados práctica asistida, práctica N5 por sectores y examen fijo. Ya hay campos de exportación para asistencia y variante N5.
8. Mantener visible y accesible “Finalizar”, porque el usuario ya necesitó preguntar cómo terminar la práctica.

Son propuestas, no funciones nuevas ya implementadas.

## 9. Riesgos concretos que conviene revisar ahora

Además del atasco informado por el usuario, la lectura del código actual detecta estos puntos para inspeccionar. **No todos son bugs reproducidos.**

| Prioridad | Punto | Verificación recomendada |
|---|---|---|
| P0 | Último test sin resultado final | Repetir `RunControlBatch`, investigar dónde termina y distinguir fallo del proceso de fallo funcional. |
| P0 | Protección de punta no protege todo el brazo | Reproducir choque de codo/muñeca/soporte contra mesa. Observar contactos físicos y seguimiento, no solo la esfera. |
| P0 | `GuardSurface` llama `Collider.ClosestPoint` sobre colliders próximos; N5 genera `MeshCollider` no convexos | Verificar compatibilidad en Unity y ausencia de errores con el caño. No hay un tratamiento específico de esos meshes en esa rama del código actual. |
| P0 | Cambio teclado → IMU tras un bloqueo | Revisar que se limpien `MotionBlocked`/`Retracting`: la rama no física no los reinicia expresamente y el permiso de arco consulta esos estados. |
| P1 | C tiene dos usos | Resolver retirada vs recentrado XR y actualizar la ayuda. |
| P1 | Detector de bloqueo observa órdenes de traslación manual | Probar velocidad fina, límites de espacio, cambio de dirección, soltar tecla y bloqueo por orientación. Evitar falsos positivos. |
| P1 | C solo sube y tiene timeout | Probar desde debajo de un obstáculo o cerca de un límite. No etiquetarlo como recuperación universal. |
| P1 | Márgenes físicos de arco y protección | Revisar que la distancia de seguridad no vuelva imposible el arco ideal, especialmente en N3/N4/N5 y con otros electrodos. |
| P1 | Transiciones entre ejercicios | Retirar antes de cambiar geometría; una transición antigua a N3 sin retirada falló. |
| P1 | N5 completo fijo | Falta validar alcance/orientación por todo el círculo; la práctica indexada no demuestra el examen fijo. |
| P2 | Operario | Revisar proporciones del torso, que usa una cápsula escalada, y apariencia a distancia. |
| P2 | Estado de documentación y builds | Los reportes y capturas pueden pertenecer a una versión anterior. Revisar fecha y contenido antes de citarlos. |

## 10. Soldadura, resultados y VR/BLE

### Proceso de soldadura

La longitud del arco se mide desde `ArcOriginPosition` en la dirección del electrodo. Se ignora el robot y los triggers de UI. La longitud de varilla disminuye al soldar y R repone. Revisar siempre que consumo visual, medición y punto de deposición coincidan.

`WeldingTaskManager` llama a `Seam.Deposit(Torch.ArcOriginPosition, ...)`. El renderer del cordón incorpora perfil de domo, ondulación por distancia y proyección visual sobre la pieza. Eso mejora el aspecto; no sustituye ni redefine el modelo de energía/calidad.

Los resultados automáticos se guardan en `Application.persistentDataPath/Resultados`, en JSON/CSV. El esquema JSON actual es 2 e incluye `assistedControl`; el CSV incluye `control_asistido`. También se registra la variante indexada de N5. Si el guardado falla, G permite reintentar.

### Cardboard y Android

Hay preparación de seguimiento de cabeza de solo rotación, parámetros de visor y recentrado en `Assets/VR/MobileVRQuality.cs`. El rig padre se mueve entre estaciones; no se debe sobrescribir la rotación de la cámara XR cada frame.

`Assets/Scripts/Welding/Editor/FrankaVRSetupWizard.cs`, menú **FrankaVR → 12 - Preparar Cardboard Android**, configura loader, OpenGLES3, LandscapeLeft y SDK objetivo 36. También excluye de Android las librerías nativas de escritorio de Assimp del URDF Importer. Fue necesario para resolver un fallo de empaquetado por plugins duplicados. Reaplicar/comprobar esa preparación cuando se regenere la caché de paquetes o se vuelva a construir Android.

APK compilado no significa experiencia VR probada. Siguen pendientes instalación, head tracking, interacción por mirada, rendimiento, confort y lectura de interfaz en el teléfono real.

### Mando físico/BLE

Se discutió inicialmente un guante, pero el concepto posterior fue un mando sobre MDF con ESP32, botones y un mango móvil con IMU conectado por cable. Los nombres `Glove` en los scripts son históricos; no obligan a fabricar un guante wearable.

Antecedentes de hardware: ESP32 con CP2102, MPU-6500 previsto, cinco pulsadores, joystick opcional. No asumir compra/montaje actualizado ni conservar COM5 como configuración universal.

Existe puente de paquetes CSV IMU/BTN y entrada híbrida con timeouts, probado mediante inyección de mensajes. **No hay evidencia de transporte Android BluetoothGatt y hardware final funcionando.** No confundir un CSV emulado con una conexión BLE real. El formato binario de 20 bytes fue una propuesta, no un protocolo final validado. Pinout y firmware necesitan confirmación física antes de cerrarse.

## 11. Orden recomendado para retomar

### Paso 1 — cerrar la tanda pendiente

- Leer el código actual y el log incompleto antes de cambiar el enfoque.
- Crear backup del estado actual.
- Repetir compilación y `RunControlBatch`; determinar si el proceso se interrumpe, el test queda colgado o aparece una condición funcional fallida.
- Revisar el conflicto de C y las ramas de cambio a IMU.
- Obtener una captura real del operario y corregir sus proporciones si hace falta.

### Paso 2 — hacer confiable el manejo

- Probar manualmente acercamiento, avance, roce, retirada y cancelación, con y sin Espacio y con modo fino/rápido.
- Separar “objetivo bloqueado por guardia”, “robot físicamente en contacto”, “límite de alcance” y “solver sin converger”. No tienen la misma solución.
- Primero corregir el caso reproducible más frecuente. Si hace falta rediseñar el control, preservar el TCP y la deposición, ofrecer cancelación y demostrar mejora con recorridos comparables.
- Evaluar un modo de trabajo guiado en el plano de la junta y una altura de tránsito como alternativa, si el control libre sigue siendo difícil. Esto es una propuesta futura, no una implementación existente.

### Paso 3 — consolidar experiencia

- Mejorar el HUD existente y la claridad de estados/controles.
- Validar sesiones completas N1–N5, no solo puntos. Revisar en especial N3, N4 y N5.
- Recompilar Windows después de cerrar los cambios, guardando logs y diferenciando esa versión del APK viejo.

### Paso 4 — hardware cuando sea posible

- Validar mando, ejes, calibración y timeouts en PC.
- Incorporar transporte Android real y permisos con hardware disponible.
- Validar Cardboard en teléfono y ajustar rendimiento de la escena.

## 12. Cómo comprobar el proyecto

Herramienta existente: `Assets/Editor/FrankaVRProjectChecks.cs`.

Métodos públicos:

- `FrankaVRProjectChecks.RunBatch`: integración general y pruebas de curso/robot.
- `FrankaVRProjectChecks.RunControlBatch`: control de punta, aproximación, retirada, recuperación/bloqueo y capturas según el código actual.
- `FrankaVRProjectChecks.BuildDesktop`: build Windows.
- `FrankaVRProjectChecks.BuildAndroid`: preparación y build Android.

Ejemplo, con el proyecto cerrado en cualquier otra instancia de Unity:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" `
  -batchmode `
  -projectPath "D:\01. Facultad\RV\FrankaVR" `
  -executeMethod FrankaVRProjectChecks.RunControlBatch `
  -logFile "D:\01. Facultad\RV\FrankaVR\Verificacion\Unity_control_nueva_corrida.log"
```

No agregar `-quit` de forma indiscriminada a las pruebas asíncronas de Play: la herramienta gestiona la salida al terminar. Para capturas hacen falta gráficos. No abrir dos instancias sobre el mismo proyecto ni terminar una sesión del usuario para liberar el bloqueo.

Revisar el proceso hasta su salida, el log final y la fecha de los reportes. El test de bloqueo deshabilita temporalmente el IK para simular falta de avance; eso no valida por sí solo una colisión física de todo el brazo.

## 13. Backups, documentos previos y OneDrive

Backups locales relevantes:

```text
_Backups/Antes_estabilizacion_20261009/
_Backups/Antes_mejoras_20261009/
_Backups/Antes_control_soldadura_20261010/
_Backups/Antes_retirada_taller_20261010/
_Backups/Antes_operario_control_20261010/
```

El último es especialmente útil para comparar lo nuevo de operario/deslizamiento/bloqueo con la versión anterior. No restaurar todo sin examinar diferencias.

Documentos históricos:

- `AGENTS.md`: instrucciones de trabajo y restricciones.
- `CONTEXTO_FRANKAVR_CODEX.md`: transferencia original, con síntomas y decisiones históricas; varias frases de “no probado” corresponden al momento inicial y no al estado de todos los builds posteriores.
- `ESTADO_FRANKAVR_20261009.md`: estabilización inicial.
- `MEJORAS_FRANKAVR_20261009.md`: comodidad, resultados, preparación VR y builds anteriores.
- `CONTROL_SOLDADURA_20261010.md`: cambio a control de punta y asistencia.
- `RETIRADA_Y_TALLER_20261010.md`: retirada y taller antes del operario.
- `Verificacion/ControlSoldadura/` y `Verificacion/RetiradaTaller/`: diffs y comprobaciones de esas etapas.

OneDrive se discutió para ahorrar espacio. El usuario decidió dejarlo para otra cosa. **No hay una migración pendiente ni se movió/borró el proyecto por ese motivo.**

## 14. Mensaje para iniciar la próxima conversación

Si la otra IA no tiene acceso a la carpeta local, este Markdown aporta contexto pero no reemplaza los archivos del proyecto. Para resolver el control, proporcionar al menos las versiones actuales de `WeldingTeleopController`, `PandaArm`, `PandaIK`, `KeyboardTeleopInput`, `HybridTeleopInput`, `WeldingTorch`, el test y su log. Para interfaz, agregar HUD, menú y `ViewpointManager`; para el operario, `WeldingEnvironmentPolish`. Pedir archivos concretos cuando falte información, en lugar de inventar referencias o reconstruir el proyecto a partir de parches viejos.

> Estoy continuando FrankaVR, un simulador Unity de soldadura SMAW con Franka Panda, PC y preparación Cardboard/ESP32. Este documento actualiza el trabajo hecho principalmente con Codex. Leelo completo y revisá el proyecto real antes de proponer cambios. La prioridad es resolver atascos del robot cerca de la mesa y consolidar un control sencillo. Hay cambios de deslizamiento, detección de bloqueo, recuperación con C y un operario que todavía no tienen una corrida final aprobada ni un build actualizado. No cambies PandaIK.endEffector de Soldador, no reactives el clamp vertical ni rompas .meta o entrada híbrida. Primero identificá lo activo, reproducí el problema y proponé una intervención pequeña y verificable. Después continuamos mejorando el HUD existente y la experiencia de práctica, evitando duplicar versiones, paneles y componentes.

---

**Punto exacto de continuidad:** cerrar y validar la última tanda de operario/control, resolver los atascos con evidencia y mejorar la claridad de la interfaz. La existencia de pruebas y builds anteriores aprobados no convierte automáticamente los scripts actuales en una entrega terminada.
