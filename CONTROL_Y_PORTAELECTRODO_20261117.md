# FrankaVR — controles, porta-electrodo y entorno (17/11/2026)

Backup previo: `_Backups/Antes_correcciones_entorno_20261117/` (incluye `Assets/Main.unity` original).

## Causa raiz del "se traba cerca de la mesa"
El eje de herramienta del IK es +Y de `Soldador` (sale del flange), pero el porta-electrodo
estaba armado en -Y, hacia adentro de la muneca. La punta del arco (`Tip` + 350 mm) quedaba
junto a la boca del cable y el flange trabajaba a ~2 cm de la chapa. Al consumirse la varilla
el controlador bajaba el flange y `BocaCable` (collider en panda_link8) chocaba contra la chapa
a los ~15 mm de consumo: el brazo se frenaba, el arco se iba a 40 mm y saltaba el detector.

Correccion (menu **FrankaVR → 13 - Orientar porta-electrodo hacia la pieza**, ya aplicado y guardado):
mordazas en la cara del flange (+0.012), mango y entrada de cable hacia la muneca, `Tip` en +0.002,
electrodo de 350 mm visible hacia la pieza. `PandaIK.endEffector` sigue siendo `Soldador` y
`ToolBase` no se movio. `WeldingCellBuilder` arma la misma geometria. El mismo paso asigna
`MatCordon` al cordon (no tenia material y se veia magenta). Es idempotente.

Consecuencia: el flange trabaja ~35 cm mas alto y el techo de alcance queda ~15 cm sobre la
costura. `ResetTo`/`TipStart` ahora son posicion de la PUNTA, no del flange. Ajustes por eso:
- Costuras rectas (N1-N4) se sueldan hacia la base del robot (`TrainingWorkpieceFactoryV5.WeldTowardRobot`):
  con arrastre el mango se inclina hacia el avance y el flange queda cerca. Al invertir el avance
  el angulo de trabajo cambia de signo (`WeldSeam.ReverseWorkSide`); N3 conserva el lado abierto.
- `PandaIK.elbowKeepBentDeg = -20`: el codo no llega al tope de -4 (brazo recto), donde el DLS
  quedaba trabado sin poder recogerlo (reproducido: error fijo de 29 mm con J4 = -4).
- B sube solo hasta la altura de aproximacion; C corta si no hay avance real.

## Controles (teclado)
- Libre: ~60 mm/s, acelera a ~150 mm/s al mantener; Shift ~150; Ctrl ~10.
- I/K/J/L inclinan respecto de la vista; U/O giran; V endereza (practica: angulo recomendado).
- Espacio a <30 mm de la costura: las teclas se alinean con la junta (avance 4 mm/s,
  Shift 7, Ctrl 2; lateral 3 mm/s; Q/E largo de arco 2 mm/s, E sostenida levanta y corta).
  Sobre la costura I/K y J/L cambian por separado angulo de avance y de trabajo.
- C solo retirada; recentrado Cardboard pasa a Inicio. C corta si no hay avance real.
- Limite de alcance se informa como tal, no como "bloqueado".

## HUD
Barra de largo de arco con zona ideal (oculta en examen). En practica: velocidad de avance y
angulos reales contra ideales. Ayuda F1 actualizada.

## Entorno
`FactoryLineScenery` (creado por `WeldingEnvironmentPolish.FactoryLine`): nave, linea robotizada
con cinta, 3 robots, AGV, puente grua, almacen, tablero. Sin colliders; la pared izquierda de la
celda pasa a valla. Carteles con shader `Resources/FrankaVRText3D` (prueba de profundidad).

## Otros arreglos
- `GuardSurface` ignora `MeshCollider` no convexos en `ClosestPoint` (caño N5).
- Cambio teclado→IMU limpia `MotionBlocked`/`Retracting`.

## Verificacion
Menu **FrankaVR → Verificacion**: Capturas del entorno / Control de punta / Cordon completo N1.
Resultados 17/11 (Unity 6000.5.10f1, editor):
- Control de punta: PASS (aproximacion, avance, arco, orientacion, superficies, retirada, C, bloqueo, examen).
- Cordon completo N1 solo con Espacio + tecla de avance: 58/60 correctos, arco 100 %, sin trabarse.
- Integracion N1-N5: PASS; alcance en modo fino 1,7-2,8 mm y < 1 grado; arco estable en los cinco (N4 71 J).
Pendiente: build Windows/Android nuevos, prueba manual completa de N2-N5 y del examen.
Con Unity abierto, no cambiar de ventana durante las pruebas (el editor se ralentiza y vencen los tiempos).
