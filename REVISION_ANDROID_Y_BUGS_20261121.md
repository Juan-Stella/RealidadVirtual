# Revisión para el build Android y búsqueda de bugs (21/11/2026)

Rama `revision-android-20261121`, hecha sobre `27fd4c9` (servo suave + CONTROLES), sin la PC.
**No está compilada en Unity ni probada en el celular.** Lo que sí se verificó: todos los `.cs` tocados pasan el parser de C#. Además, el receptor UDP, el puntero, la limpieza de UI y el script de Gradle compilan contra stubs de Unity. El script de Gradle también se probó sobre un proyecto Gradle de prueba (agrega y no duplica).

Falta combinarla con la rama de la notebook (`mejoras-movimiento-20261119`, commit `cff627f`, que no está en GitHub). Los cambios tocan zonas distintas de los mismos archivos: debería entrar con `git cherry-pick` sin conflictos.

## 1. Build Android / Cardboard (lo que frenaba el paso 2)

La guía oficial de Cardboard para Unity 2023.1+ pide tres cosas que el proyecto no tenía. El build del 17/11 compiló igual, pero la app se iba a cerrar en el celular.

| Problema | Efecto en el celular | Arreglo |
|---|---|---|
| *Application Entry Point* = GameActivity | Cardboard pide Activity | `ProjectSettings`: Activity (y el paso 12 del asistente lo fija) |
| Faltaban las dependencias Java del SDK (appcompat, play-services-vision, material, protobuf-javalite) | El primer arranque abre el escáner del QR del visor y la app se cierra | `Assets/Editor/CardboardAndroidGradle.cs` las agrega al proyecto Gradle en cada build, junto con AndroidX/Jetifier |
| *Optimized Frame Pacing* prendido | La guía lo pide apagado | `ProjectSettings` y paso 12 |

## 2. Bugs corregidos

| Dónde | Problema | Arreglo |
|---|---|---|
| `GazeInteractor` (puntero de mirada) | El puntero estaba a 2 m, detrás del menú (1.8 m), el HUD (1.55 m) y CONTROLES (1.3 m): justo al mirar un botón quedaba tapado y no se veía el anillo de progreso | Se dibuja encima de todo y a la distancia de lo que se mira, con el mismo tamaño aparente |
| `GazeInteractor` | Quedarse mirando un botón lo activaba cada 1.2 s: CONTROLES del HUD abría y cerraba, VISTA cambiaba de puesto | Una activación por mirada: hay que sacar la vista para volver a activar |
| `UdpControllerReceiver` | Con datos móviles prendidos, Android manda el HELLO por la red celular. El ESP32 queda en broadcast, con más latencia y saltos | La app se ata a la Wi-Fi del mando (sin internet), recién después del QR del visor. Si Android no deja, queda como antes: hay que apagar los datos móviles |
| `UdpControllerReceiver` | Al reabrir el puerto, el hilo viejo seguía leyendo el socket nuevo en paralelo | Cada hilo lee solo su socket |
| `SceneUICleanupV62` | Cada 1.5 s recorría 6 veces todos los objetos de la escena. En el celular, probable tirón periódico | Un recorrido por barrido y solo los primeros 12 s |
| `VRTrainingHUDV6`, `CourseWorldMenuV6` | Rearmaban todos los textos en cada cuadro: basura constante para el recolector | HUD a 20 Hz; menú solo al cambiar de fase o ejercicio (y cada 0.5 s) |
| `PandaIK` | Creaba un arreglo por junta en cada paso de física (~840/s) | Buffer reutilizado |
| `WeldingTaskManager` | "Cortes de arco" contaba también soltar el gatillo y el fin del programa (cada ejecución sumaba 1) | Solo cuenta si el arco se apaga mientras se sigue pidiendo |
| `ViewpointManager` | El fundido al cambiar de vista no tapaba HUD ni puntero | Se dibuja último |
| Textos | "MOVIMIENTO BLOQUEADO — E: retirar" (E sube, no retira); "ACERCANDO" solo nombraba B | Mencionan RETIRAR/ACERCAR del mando |

## 3. Encontrado y no cambiado (decidir)

1. **Programa con varios trazos:** al ejecutar, el robot une el final de un trazo con el comienzo del siguiente **con arco encendido**. Si se graba dos veces el mismo tramo sin DESHACER, lo suelda dos veces (perforación). Opciones: cortar el arco entre trazos u ordenar los trazos por avance.
2. **Rendimiento del taller:** la línea de fondo (`FactoryLineScenery`) son cientos de primitivas sin agrupar. En Cardboard cada ojo las dibuja de nuevo. Si en el celular no llega a 60 fps, lo primero es apagar `FactoryLine` en `WeldingEnvironmentPolish`.
3. **Calidad en el celular:** MSAA 4x + 1.25x de resolución por ojo puede ser mucho para un celular de gama media. Medir antes de dejarlo.
4. **`RobotCollisionGuard`:** busca todos los colliders de la escena cada 0.5 s (basura moderada). Se deja hasta medir en el celular.
5. **Examen:** ACERCAR (B) no hace nada porque no hay asistencia, pero CONTROLES no lo aclara.
6. **Teclado SMAW:** R borra el programa y además repone electrodo. Con MIG (M1-M3) no molesta.

## 4. Mañana, con la notebook

1. Pasar estos commits a la rama de trabajo de la notebook (`cherry-pick` sobre `cff627f`).
2. Abrir Unity (6000.5.10f1 desde el Hub), esperar la compilación y correr las cuatro verificaciones (Control de punta, Ensenar y ejecutar M1, Integracion N1-N5, Mando Wi-Fi).
3. Correr *FrankaVR › 12 - Preparar Cardboard Android*. Después, en *File › Build Profiles › Android*, activar *Development Build* la primera vez y compilar el APK.
4. En el celular: instalar, escanear el QR del visor **con internet** (Wi-Fi común o datos), después conectarse a FrankaVR-Mando.
5. Revisar: que el puntero se vea sobre los botones, el texto, los fps (si hay tirones, ver punto 3.2) y que el mando responda con los datos móviles prendidos.
