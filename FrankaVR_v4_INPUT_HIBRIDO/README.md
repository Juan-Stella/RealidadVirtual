# FrankaVR v4 - Reparacion de entrada hibrida

## Problema corregido
El configurador anterior asignaba `BleGloveInput` directamente a `WeldingTeleopController.InputSource`.
Por eso, sin ESP32/IMU transmitiendo, el teclado dejaba de mover el efector.

## Solucion
Se agrega `HybridTeleopInput`:
- Sin paquetes IMU recientes: movimiento y orientacion por teclado.
- Con paquetes IMU recientes: movimiento y orientacion por guante.
- Chispa y comandos discretos aceptan teclado o botonera.
- Si BLE se corta, vuelve automaticamente a teclado.

## Instalacion
1. Cerrar Unity.
2. Extraer esta carpeta en la raiz del proyecto (al lado de Assets, Packages y ProjectSettings).
3. Ejecutar `REPARAR_INPUT.bat`.
4. Abrir Unity y esperar que compile.
5. Ejecutar `FrankaVR > 4 - Reparar control Teclado + Guante`.
6. Ejecutar `FrankaVR > 3 - Validar escena`.

## Prueba en PC
- W/S: adelante/atras
- A/D: izquierda/derecha
- Q/E: abajo/arriba
- I/K, J/L, U/O: orientacion
- Space: arco
- R: reponer electrodo
- Tab: cambiar electrodo

En `HybridTeleopInput`, `Active Motion Source` debe mostrar `Teclado` mientras no haya IMU BLE activo.
