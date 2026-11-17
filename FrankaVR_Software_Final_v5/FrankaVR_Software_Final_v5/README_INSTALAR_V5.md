# FrankaVR — Software Final V5 (add-on seguro)

Esta version agrega la experiencia de curso sin reemplazar los scripts base que ya funcionan.

## Que agrega

1. Flujo de curso: seleccion -> briefing -> entrenamiento/examen -> resultados.
2. Cinco geometrías compatibles con N1-N5:
   - placa simple;
   - junta a tope;
   - junta en T;
   - solape;
   - caño a tope.
3. Taller low-poly: mesa, piso, pared, soldadora, pinza de masa, cables y señalizacion.
4. Iluminacion industrial liviana para mobile.
5. Nucleo visible del arco + charco luminoso, complementando WeldingFX.
6. HUD VR contextual, con alertas de arco, electrodo y temperatura.
7. Modo PC/Cardboard: en PC, boton derecho + mouse emula movimiento de cabeza sin rotar el cuerpo virtual.
8. Emulador de guante: envia paquetes reales a BleGloveInput desde una ventana del Editor.

## Instalacion

1. Cerrar Unity.
2. Copiar la carpeta `Assets/FrankaVR/V5` de este paquete dentro de TU `Assets/FrankaVR/`.
   Resultado esperado: `Assets/FrankaVR/V5/...`
3. Abrir Unity y esperar que compile.
4. Ejecutar: `FrankaVR > 5 - Instalar experiencia de curso V5`.
5. Ejecutar: `FrankaVR > 6 - Validar experiencia V5`.
6. Play.

IMPORTANTE: esta version NO trae copias de ViewpointManager, WeldingTorch, PandaIK,
WeldingTeleopController, etc. No deberia generar clases duplicadas.

## Controles de curso en PC

- 1..5: seleccionar N1..N5
- T: comenzar en Entrenamiento
- Y: comenzar en Examen
- F: finalizar ejercicio y calcular resultado
- M: volver al menu
- Boton derecho + mouse: mirar alrededor como si fuera la cabeza

Los controles del robot siguen siendo los que ya funcionan:
W/S, A/D, Q/E, orientacion I/K J/L U/O, Space arco, etc.

## Emulador de guante

En Play abrir:
`FrankaVR > Hardware > Emulador de guante V5`

- Streaming ON manda quaternion al `BleGloveInput`.
- Pitch/Yaw/Roll simulan la IMU.
- Clutch prueba movimiento por velocidad.
- Chispa y botones prueban la segunda mano.
- HybridTeleopInput debe cambiar de `Teclado` a `Guante BLE` al recibir paquetes frescos.

## Rendimiento Cardboard

La escenografia usa primitivas y materiales simples, sin texturas externas ni postprocesado.
El arco V5 no crea una segunda luz por defecto porque WeldingFX ya posee una.

## Antes del hardware

Validar en este orden:
1. Todos los ejercicios se construyen sin mover el Panda.
2. Teclado controla el efector en los cinco ejercicios.
3. La cabeza de PC (mouse) no cambia los ejes de movimiento del robot.
4. El emulador de guante toma el control cuando Streaming esta activo.
5. Al detener Streaming, el sistema vuelve a teclado por timeout.
6. Probar en Android/Cardboard y medir FPS antes de cerrar el diseño de hardware.
