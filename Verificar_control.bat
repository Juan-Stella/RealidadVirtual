@echo off
rem Cerrar Unity antes de ejecutar: no puede haber dos instancias sobre el proyecto.
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe"
echo Ejecutando RunControlBatch (puede tardar varios minutos)...
%UNITY% -batchmode -projectPath "%~dp0." -executeMethod FrankaVRProjectChecks.RunControlBatch -logFile "%~dp0Verificacion\Unity_control_20261117.log"
echo Codigo de salida: %ERRORLEVEL%
type "%~dp0Verificacion\Resultado_Unity.txt"
echo.
pause
