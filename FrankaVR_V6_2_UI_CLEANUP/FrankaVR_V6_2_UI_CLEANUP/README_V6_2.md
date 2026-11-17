# FrankaVR V6.2 — limpieza automatica de UI

Esta actualizacion corrige la superposicion de textos/HUDs sin configuracion manual.

## Que hace automaticamente
- Desactiva componentes legacy:
  - `VRTrainingHUDV5`
  - `CourseWorldMenuV5`
  - `CardboardStatusHUD`
  - `WeldingHUD`
  - `TaskHUD`
- Oculta los `TextMesh` referenciados por los HUD viejos (`MachinePanel`, `BriefPanel`, `ResultPanel`, etc.).
- Vigila durante Play y vuelve a apagar objetos legacy si algun script viejo intenta generarlos.
- Mantiene activos solamente los sistemas V6:
  - `CourseWorldMenuV6`
  - `VRTrainingHUDV6`
- El panel grande del curso se oculta mientras el ejercicio esta en `Running`.
- La linea larga de ayuda del HUD se oculta mientras se esta soldando.

## Instalacion
1. Cerrar Unity.
2. Copiar las carpetas `Assets/FrankaVR/V6` y `Assets/FrankaVR/V6_2` dentro del proyecto y reemplazar V6 cuando lo solicite.
3. Abrir Unity y esperar que compile.
4. Ejecutar:
   `FrankaVR -> 9 - Limpiar textos y UI V6.2`
5. Validar con:
   `FrankaVR -> 10 - Validar UI V6.2`

No modifica PandaIK, WeldingTorch, ViewpointManager ni la logica de soldadura.
