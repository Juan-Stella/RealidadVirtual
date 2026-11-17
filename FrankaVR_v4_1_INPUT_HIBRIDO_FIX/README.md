# FrankaVR v4.1 - FIX del reparador de input hibrido

Esta version corrige el error de PowerShell:

`Excepcion al llamar a Substring: startIndex no puede ser mayor que la longitud de la cadena.`

## Que estaba mal
La busqueda de clases devolvia un array anidado. Con una sola coincidencia, PowerShell podia convertir el resultado a `System.Object[]` al llamar a la funcion de reemplazo. Despues el script intentaba calcular una ruta relativa sobre ese texto y fallaba.

## Que cambia
- La busqueda devuelve rutas de archivo reales, sin arrays anidados.
- El backup ya no usa un `Substring()` inseguro.
- Se normalizan todas las rutas con `GetFullPath`.
- Se valida que destino y fuente existan antes de copiar.
- Si hay duplicados, se detiene y los muestra; no elige uno al azar.
- Sigue preservando el `.meta` del archivo existente porque solo reemplaza el contenido del `.cs`.

## Instalacion
1. Cerrar Unity.
2. Extraer `FrankaVR_v4_1_INPUT_HIBRIDO_FIX` en la raiz del proyecto, al lado de `Assets`, `Packages` y `ProjectSettings`.
3. Ejecutar `REPARAR_INPUT.bat`.
4. Abrir Unity.
5. Esperar la compilacion.
6. Ejecutar `FrankaVR > 4 - Reparar control Teclado + Guante`.
7. Ejecutar `FrankaVR > 3 - Validar escena`.
8. Probar Play sin BLE: debe usar teclado.

## Teclado
- W/S: adelante/atras
- A/D: izquierda/derecha
- Q/E: abajo/arriba
- I/K, J/L, U/O: orientacion
- Space: arco
- R: reponer electrodo
- Tab: cambiar electrodo
