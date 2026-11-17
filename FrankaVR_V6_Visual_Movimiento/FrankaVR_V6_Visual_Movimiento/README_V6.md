# FrankaVR V6 — movimiento de depuracion + mejora visual

## Que agrega
- Movimiento por teclado en el espacio para PC:
  - **Flechas** = desplazarse
  - **RePag / AvPag** = subir / bajar
  - **Shift** = rapido
  - **Mouse derecho** = mirar
- HUD nuevo mas limpio (`VRTrainingHUDV6`)
- Menu/panel lateral mas ordenado (`CourseWorldMenuV6`)
- Entorno de taller mas completo (`WorkshopEnvironmentV6`)
- Arco visual mejorado (`WeldArcVisualEnhancerV6`)
- Cordon de soldadura mas creible (`WeldBeadRendererV6`)

## Instalacion
1. Copiar la carpeta `Assets/FrankaVR/V6` dentro del proyecto.
2. Abrir Unity y esperar compilacion.
3. Ejecutar:
   - `FrankaVR -> 7 - Instalar mejoras visuales + movimiento V6`
4. Opcionalmente validar con:
   - `FrankaVR -> 8 - Validar V6`

## Notas
- Esta version **no reemplaza** `WeldingTorch`, `PandaIK` ni `ViewpointManager`.
- El renderer viejo del cordon (`WeldBeadRenderer`) se desactiva automaticamente y se agrega `WeldBeadRendererV6` en el mismo objeto.
- Si ya existia `DesktopHeadLookV5`, el instalador lo desactiva y deja activo `DesktopNavigatorV6`.
