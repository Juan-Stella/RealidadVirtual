# FrankaVR / RealidadVirtual

Simulador didáctico de soldadura SMAW con robot Franka Panda, desarrollado en Unity. Incluye teclado/ratón para PC, curso N1–N5, práctica/examen, medición de arco, electrodo consumible, resultados y preparación para Android/Cardboard y mando ESP32.

## Abrir el proyecto

1. Clonar este repositorio.
2. Agregar su carpeta raíz a Unity Hub con Unity **6000.5.10f1**.
3. Esperar la importación y resolución de paquetes; Cardboard y URDF Importer requieren acceso a sus repositorios Git.
4. Abrir `Assets/Main.unity` y entrar en Play.

## Estado actual

El commit `52fcd48` incorpora el trabajo más reciente de Claude: porta-electrodo orientado hacia la pieza, control sobre la costura, protección del codo, HUD con información de proceso y escenografía industrial `FactoryLineScenery`.

Consultar primero **[CONTROL_Y_PORTAELECTRODO_20261117.md](CONTROL_Y_PORTAELECTRODO_20261117.md)**. Ese informe registra pruebas de Editor aprobadas y un cordón N1 con 58/60 puntos correctos. Los builds Windows/Android nuevos y las sesiones manuales completas N2–N5 siguen pendientes según ese informe. La limpieza del repositorio no ejecutó nuevas pruebas de Unity ni modificó el proyecto activo.

`TRASPASO_FRANKAVR_OTRA_IA_20261117.md` y los informes de octubre conservan antecedentes, pero preceden a esas correcciones: no usar sus controles o problemas pendientes como diagnóstico definitivo del código actual.

## Contenido del repositorio

- `Assets/`: escenas, scripts, modelos, materiales y sus `.meta`.
- `Packages/` y `ProjectSettings/`: dependencias y configuración.
- `Verificacion/`: reportes y diagnósticos; leer el informe correspondiente para distinguir pruebas antiguas y recientes.
- Documentación y scripts `Verificar_control.bat` / `Verificar_entorno.bat`.

`Library`, `Temp`, `Logs`, builds, resultados de prueba, `_Backups` y `_ArchivoLocal` son locales y se excluyen de nuevas incorporaciones a Git. Unity reconstruye sus cachés al importar.

## Backup local y limpieza

Por pedido del usuario, se eliminaron del disco los backups anteriores y los paquetes históricos V4/V5/V6 que se habían apartado en `_ArchivoLocal`. Se conserva un único backup completo del proyecto activo en `_Backups/Estado_actual_20261117/`, con Assets, Packages, ProjectSettings y documentación. Está ignorado por Git y no se obtiene al clonar. Los commits anteriores conservan el material que ya estaba versionado; no se reescribió el historial.

La limpieza preserva byte por byte `Assets`, `Packages` y `ProjectSettings`; los 338 archivos de esas carpetas se verificaron contra el backup y su manifiesto `Integridad_SHA256.csv`. Se retiró del seguimiento `FrankaVR.slnx`, generado por Unity, manteniendo su copia local. Las rutas a backups anteriores que aparecen en documentos históricos ya no existen localmente.

## Reglas de mantenimiento

Leer `AGENTS.md`. Preservar GUID y `.meta`, entrada híbrida y teclado. Mantener `PandaIK.endEffector` en `Soldador` y deposición en `ArcOriginPosition`. No reactivar el límite vertical global ni instalar paquetes históricos encima del proyecto actual.

Después de validar cambios:

```powershell
git status
git add Assets Packages ProjectSettings
# Agregar también los documentos concretos que se hayan modificado.
git commit -m "Descripción del cambio"
git push
```

Destino configurado: `https://github.com/Juan-Stella/RealidadVirtual.git`.
