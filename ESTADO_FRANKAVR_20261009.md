# FrankaVR — estabilización del 9 de octubre de 2026

Este documento registra el primer bloque de estabilización. Las mejoras posteriores, sus controles,
verificaciones y límites actuales están en `MEJORAS_FRANKAVR_20261009.md`.

## Respaldo y alcance

Respaldo previo: `_Backups/Antes_estabilizacion_20261009/`.
Incluye Assets, Packages, ProjectSettings, instrucciones, contexto y cambios locales previos de Git.
`SHA256.json` permite comprobar el contenido del respaldo. No se creó un commit con el trabajo anterior del usuario.

Los scripts y parámetros de PandaIK y PandaArm se conservaron. También se conservaron WeldingTorch,
WeldingTaskManager, WeldSeam y WeldingModel. La deposición sigue saliendo de ArcOriginPosition.
Los `.meta` existentes se conservaron; la referencia rota se reparó en Main.unity.
No se instalaron paquetes ni se modificó el protocolo para inventar un transporte BLE.

## Implementado en el proyecto real

- ViewpointManager vuelve a estar enlazado, con rig padre explícito. N/P cambia de estación;
  las flechas quedan para navegación PC. La cabeza permanece separada de la referencia del robot.
- Máquina enlazada al controlador: amperaje y selección de electrodo reciben comandos del input híbrido.
- Arranque único en selección del curso N1. T inicia práctica y Y, examen.
  El arco solo se permite durante una sesión. F finaliza y M vuelve al menú.
- CourseTrainingManagerV5 y TrainingWorkpieceFactoryV5 conectados en Main.
  El generador antiguo de caño permanece desactivado. Chapa se oculta al generar una pieza.
- HUD V6 compacto y menú V6 con instrucciones/resultados. Interfaces anteriores desactivadas.
  Texto ajustado al tamaño de los paneles y orden de dibujo corregido; menú visible al mirar hacia abajo.
  En examen se ocultan guías de trayectoria/arco y pistas de calidad; la puntuación mantiene su algoritmo.
- Navegador PC conectado: flechas, RePag/AvPag, Shift y ratón derecho.
- Radio del caño corregido; MeshCollider coincide con su superficie. N2 y N5 incluyen una raíz
  simplificada y soldable en el hueco de la junta. Son geometrías didácticas, no un modelo de fusión de metal.
- Costura N3 ubicada en el lado compatible con el ángulo de trabajo recomendado.
- Muestreo fijo por tipo de costura: 60 puntos en rectas y 84 en círculos.
  Volver de N5 a N1 no altera la discretización ni la evaluación de N1.
- Se conserva el renderer de cordón base: sección de 10 segmentos y cierre circular corregido.
- Parser CSV rechaza números no finitos, cuaterniones nulos y booleanos inválidos.
  El emulador existente usa el puente CSV cuando está presente.
- Main habilitada en la lista de escenas del build.

## Verificación

`Verificacion/Resultado_Unity.txt` y `Verificacion/Unity_integracion.log` registran la prueba real
en Unity 6000.5.10f1. `Assets/Editor/FrankaVRProjectChecks.cs` comprueba:

- ausencia de Missing Scripts y referencias críticas;
- inicio en selección, bloqueo del arco fuera de sesión y cierre de sesión;
- paquetes CSV, conmutación híbrida, amperaje, electrodo y retorno al teclado por timeout;
- superficies soldables próximas a todos los puntos de las costuras N1–N5;
- aproximación de N3 con el ángulo de trabajo recomendado y retorno N5 → N1 con muestreo consistente;
- generación de paneles y límites de ancho del texto;
- arco, consumo y energía depositada en N1 con una herramienta colocada de forma controlada.

La prueba de soldadura aísla la herramienta de los cuerpos articulados: verifica el proceso y
la punta consumible, **no demuestra alcance del IK ni manejo interactivo del teclado**.
Las capturas en `Verificacion/Capturas/` corresponden a esta prueba, no a una sesión con hardware.

La compilación inicial terminó correctamente; quedan advertencias existentes de APIs antiguas.
La verificación final de Play terminó en PASS y el build Windows terminó en Succeeded, con 0 errores
y 136.922.393 bytes informados por Unity. Consultar `Verificacion/Resultado_Build_PC.txt`
y `Verificacion/Unity_build_PC.log`. El ejecutable está en `Builds/FrankaVR_PC/FrankaVR.exe`;
distribuir esa carpeta completa, no solo el `.exe`.
El log de cierre de Unity registra avisos internos de JobTempAlloc; no se verificó su causa ni
el rendimiento del Player en uso interactivo. No equivalen a errores de compilación.
Las modificaciones automáticas de PlayerSettings y UnityConnect hechas por el build se restauraron
a su estado previo. `Verificacion/Integridad.txt` registra la comparación final y
`Verificacion/Cambios_estabilizacion.diff` muestra los cambios respecto al respaldo anterior al trabajo.

## Prueba manual recomendada

1. Abrir Assets/Main.unity en Unity 6000.5.10f1 y entrar en Play.
2. Elegir N1 con 1 y comenzar con T. Mover robot con WASD/QE; orientar con IJKL/UO.
3. Usar flechas y ratón derecho: comprobar que mirar no altera los ejes de movimiento del robot.
4. Soldar con Espacio, reponer con R, cambiar tipo con Tab y ajustar corriente con [ / ].
5. Finalizar con F, revisar resultado y volver con M. Probar también Y para examen sin guías.
6. Abrir FrankaVR > Hardware > Emulador de guante V5. Activar Streaming, calibrar, probar
   clutch y botones; desactivar Streaming y comprobar que vuelve el teclado.

## Pendiente, sin dar por confirmado

- Prueba interactiva de teclado/ratón y alcance real del robot sobre cada pieza.
- N5 completo: el límite actual de inclinación de 65° no permite asumir acceso a los 360° de un caño fijo.
  Su geometría y costura están preparadas; el ejercicio completo requiere validar alcance/orientación.
- Ejecutable PC usado de forma interactiva fuera del Editor.
- Integración XR Android: loader para Android, seguimiento de cabeza, ciclo de vida y pruebas en teléfono.
- Transporte BluetoothGatt, permisos Android, firmware y hardware real. El parser/emulador no equivalen a BLE conectado.
- Rendimiento, legibilidad y confort en Cardboard real.

Prioridad para el 26/10: asegurar una demostración PC repetible de N1, luego validar N2–N4
y delimitar honestamente la demostración N5. Continuar el pulido visual sobre estos componentes,
sin instalar otra generación de parches o duplicar paneles.
