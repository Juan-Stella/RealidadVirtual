# FrankaVR — comodidad, resultados y preparación VR

Trabajo iniciado el 09/10/2026; comprobación de Android continuada el 10/10/2026.

## Implementado

- Rueda: zoom PC, entre 38° y 75°. Retroceso restaura el zoom inicial.
  El zoom conserva el tamaño aparente y la posición de los paneles.
- Ctrl: cámara más lenta y control fino del robot. Con el MoveSpeed actual, el avance del teclado
  pasa a aproximadamente 6 mm/s. La orientación fina es independiente de esa reducción.
- Ctrl también reduce temporalmente las tolerancias del solver existente a 1 mm y 0,5°;
  al soltarlo se restauran las tolerancias anteriores. PandaIK, PandaArm y sus algoritmos se conservaron.
- F1 muestra/oculta controles en el menú existente. Al terminar se vuelve al resultado.
- Botones de selección, práctica, examen, menú, finalizar, ayuda y vistas integrados en los paneles existentes.
  PC: clic izquierdo. Android: mirada durante 1,2 s o botón del visor. Los paneles VR se anclan al cuerpo
  para poder apuntarlos con la cabeza libre. Se reutilizan GazeButton y GazeInteractor.
- N5 en práctica permite girar el caño por sectores con Z/X o sus botones.
  Requiere arco apagado y electrodo libre. Conserva energía, calidad y enfriamiento de cada punto.
  En examen el caño sigue fijo. La práctica indexada no equivale a validar el examen fijo completo.
- Resultados automáticos JSON y CSV: ejercicio, modo, variante N5, nota, aprobación, tiempo,
  cobertura, defectos, arco, pegadas, cortes y electrodos. G reintenta un guardado fallido.
- Los resultados se guardan en `Application.persistentDataPath/Resultados`.
  En Windows, con la configuración actual:
  `%USERPROFILE%\AppData\LocalLow\DefaultCompany\FrankaVR\Resultados`.
- El panel explica el defecto predominante sin cambiar el cálculo de la puntuación.
- Materiales del taller y marcas del suelo mejorados en ejecución, conservando los colliders.
- Cordón con perfil de domo, ondulación por distancia y proyección visual sobre la superficie real.
  Se conserva el renderer base. La energía se sigue depositando desde ArcOriginPosition.
- Los controles usan triggers UI; el sensor del arco los ignora para evitar superficies falsas.
- Cardboard: seguimiento de cabeza de solo rotación, parámetros del visor, recentrado y actualización
  de pantalla preparados con el paquete existente. El navegador PC no opera en Android.
  `FrankaVR > 12 - Preparar Cardboard Android` configura el loader de Android, OpenGLES3,
  orientación horizontal y SDK 36. Standalone conserva su configuración XR sin loader.
  La preparación también excluye del APK las bibliotecas nativas de escritorio de Assimp del importador URDF;
  sus GUID y la configuración PC se conservan. La exclusión se reaplica al preparar Android.

## Respaldo y protección

Respaldo anterior a estas mejoras: `_Backups/Antes_mejoras_20261009/`.
No se cambió PandaIK.endEffector, no se activó ClampHeight ni se reemplazó el input híbrido.
Los `.meta` existentes se conservaron. No se instalaron paquetes.
WeldingTaskManager y WeldingModel conservan sus algoritmos. WeldSeam solo suma el giro de geometría
para N5, sin reconstruir ni borrar los datos acumulados.

## Verificación y límites

Las pruebas en Unity están en `Verificacion/Mejoras/Unity_integracion.log` y `Verificacion/Resultado_Unity.txt`.
Incluyen regresión de referencias, teclado/BLE híbrido mediante CSV, timeouts, piezas, zoom,
ayuda, raycast/botones UI, conservación de energía al indexar y exportación JSON/CSV.

`Verificacion/Alcance_IK.csv` mide el acercamiento de la punta del robot existente a un punto de cada pieza,
con los ángulos recomendados, en modo normal y preciso. Es una prueba de puntos y aproximaciones controladas;
no prueba toda la costura, todos los caminos ni manejo manual. La prueba normal se ejecuta antes de la precisa
y las piezas comparten estados previos del robot: no es un benchmark independiente de poses iniciales.

`Verificacion/Soldadura_IK.csv` y las capturas `07_robot_N*.png` registran encendido y energía depositada
en esos puntos con el robot. Consultar esos datos antes de considerar un ejercicio validado.
La última prueba con retirada previa logró arco estable y energía positiva en N1–N5.
El error con precisión fue aproximadamente 1,7–3,0 mm en esos puntos.
N4 depositó poca energía con un arco cercano a 8 mm: necesita aproximación más corta y validar calidad.
Una transición a N3 sin retirada previa no logró soldar en la prueba; retirar la herramienta antes de cambiar piezas.
La prueba final N1 aislando la herramienta verifica además el proceso de arco y la punta consumible.

Build Windows actualizado: **Succeeded, 0 errores**, 136.936.457 bytes informados por Unity.
Ejecutable: `Builds/FrankaVR_PC/FrankaVR.exe`; conservar la carpeta completa.
Consultar `Verificacion/Resultado_Build_PC.txt`.
Build Android: consultar `Verificacion/Resultado_Build_Android.txt` y el log correspondiente.
Resultado final: **Succeeded, 0 errores**. APK de desarrollo ARM64:
`Builds/FrankaVR_Android/FrankaVR.apk` (39.457.869 bytes).
Compilar un APK no confirma seguimiento, confort, rendimiento ni funcionamiento en teléfono.

Siguen pendientes hardware/firmware, transporte BluetoothGatt y pruebas BLE reales.
El firmware no se generó porque el pinout y el formato final del mando todavía necesitan validación física.
N5 fijo completo requiere validar orientación y alcance; su práctica por sectores conserva el límite de inclinación actual.
