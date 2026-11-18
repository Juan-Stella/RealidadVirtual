# FrankaVR: celda MIG, ménsula y flujo enseñar/ejecutar (17/11/2026)

## Aplicación
El operario mueve el robot por toda la junta con la torcha. El sistema graba esa trayectoria y después el robot la repite solo, con el arco encendido. La trayectoria grabada se usa de dos formas:
- **Entrenamiento del operario**: nota, cobertura, ángulos y colisiones.
- **Dataset**: JSON y CSV con la punta, la dirección de la torcha, el CTWD, los ángulos y la calidad de cada muestra. Sirve para que en el futuro el robot aprenda a hacer la junta solo.

## Pieza y proceso
- **Pieza**: ménsula con dos cartelas (base de 8 mm, placa vertical de 100 mm y cartelas de 70 mm), sujeta con dos prensas.
- **Proceso**: MIG/MAG con alambre ER70S-6 (0,8 / 1,0 / 1,2 mm). El alambre no se consume, y la distancia ideal punta-pieza (CTWD) es de unos 15 mm.
- **Torcha**: torcha robótica corta, de 165 mm desde el flange, con boquilla de 16 mm (paso de menú *FrankaVR / 14 - Montar torcha MIG*).

| Trabajo | Junta | Nivel |
|---|---|---|
| M1 | Filete en la cara interior de la cartela izquierda, de la placa hacia la punta | Base |
| M2 | Lo mismo en la cartela derecha | Base |
| M3 | Filete frontal entre la base y la placa. La torcha apunta hacia el robot; usar VISTA | Avanzado |

## Uso (teclado, hoy; los mismos comandos irán al joystick ESP32)
1. Elegir el trabajo y pulsar **PRACTICAR** (o EXAMEN).
2. **B**: lleva la torcha al inicio de la junta. Sube por encima de la pieza, se traslada, baja y queda a la distancia de arco.
3. **ENSEÑAR**: mantener **Espacio** (el gatillo) cerca de la junta y avanzar con la tecla que indica el HUD. Mientras se graba, la torcha queda guiada sobre la junta.
   - **A/D** o **W/S**: corrección lateral.
   - **Q/E**: distancia a la pieza.
   - **I/K/J/L**: ángulos.

   En enseñanza no hay arco: solo se graba.
4. **Z** deshace el último tramo y **R** borra todo. **- / +** cambian la velocidad del programa (2 a 12 mm/s).
5. **Enter** o **EJECUTAR**: el robot repite la trayectoria. Se acerca, enciende el arco, se queda quieto 0,5 s al arrancar y otros 0,5 s al final (cráter) y se retira.
6. Al terminar se muestran los resultados y se guardan `Resultados_prueba/<fecha>_<trabajo>.json/.csv`, más `_trayectoria.json/.csv`.
7. **C**: retirada segura en cualquier momento.

## Cambios técnicos
- **Colisiones**: ya no frenan el brazo (RobotCollisionGuard ignora los choques entre el robot y el entorno). Si hay contacto, el eslabón se tiñe de rojo, el HUD avisa y la muestra queda marcada en el dataset.
- **Reorientación**: los giros grandes pivotan sobre la punta y no sobre el TCP, así la punta no barre la pieza. Con la punta quieta, la protección de superficie ya no desplaza el punto sostenido.
- **Posición de la pieza**: la ménsula se corrió 10 cm hacia el robot (`BracketShift`) para no trabajar al límite del alcance.

## Verificación en Unity
Corrida en el Editor el 17/11/2026, menú *FrankaVR / Verificacion*:

| Prueba | Resultado |
|---|---|
| **Ensenar y ejecutar M1** | PASS: 54/60 puntos correctos, 6 fríos, 0 sin soldar, nota 85. Trayectoria enseñada con 97 % de cobertura y 0 colisiones |
| **Control de punta** | PASS |
| **Integracion N1-N5** | PASS (ahora recorre M1-M3) |

No se probó en APK ni con Cardboard ni BLE.
