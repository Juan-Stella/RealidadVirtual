# Instrucciones de trabajo — proyecto Unity FrankaVR

Lee primero `CONTEXTO_FRANKAVR_CODEX.md`. El proyecto Unity **real en disco** es la fuente de verdad; el contexto recoge decisiones y síntomas históricos.

- Responder en español, de forma directa y relativamente breve.
- Antes de editar, revisar `Assets`, escenas/prefabs, scripts, versiones duplicadas, `Packages/manifest.json` y `.meta`. Enumerar cambios previstos en forma breve.
- Priorizar llegar a un prototipo estable para la evaluación del **26 de octubre de 2026**. Evitar sobreingeniería y paquetes externos salvo necesidad justificada.
- No cambiar `PandaIK.endEffector` de `Soldador` al `Tip` del arco. No volver a activar límite vertical que impedía alcanzar pieza.
- Mantener teclado funcional sin BLE; la entrada debe ser híbrida. PC puede desplazarse con flechas; Cardboard gira cabeza independientemente del movimiento del robot.
- No duplicar clases/archivos (`ViewpointManager`, HUDs, WeldBeadRenderer, etc.) ni introducir otro panel HUD sin retirar/gestionar el anterior.
- Preservar `.meta`/GUID de Unity, escenas y prefabs; crear backup o trabajar bajo control de versiones antes de cambios significativos.
- No modificar física de soldadura, deposición ni IK por motivos meramente estéticos. La deposición debe salir de la punta consumible `ArcOriginPosition`.
- Distinguir claramente implementado, preparado, probado y pendiente. El BLE Android real y el build Cardboard no están confirmados.
- Nunca afirmar compilación o ejecución de Unity/Android salvo haberlo comprobado con herramientas reales.
- Si hay más de un enfoque, elegir el menor cambio seguro y entregar scripts/instrucciones coherentes con rutas actuales del proyecto.
