# Control de soldadura — 10/10/2026

Objetivo: colocar la punta consumible y seguir la costura con teclado de forma mas controlable.

## Uso

1. Seleccionar ejercicio y entrar en practica con T.
2. Pulsar B para acercar la punta a la zona de costura mas cercana. El robot se retira, orienta y baja progresivamente. Esperar a que desaparezca el aviso de aproximacion. B, cualquier movimiento o Espacio cancelan el recorrido.
3. Mantener Espacio y avanzar con WASD. En practica, cerca del trazo, la ayuda centra suavemente la punta y regula la distancia medida del arco segun el diametro del electrodo. El avance sigue siendo manual.
4. Soltar Espacio para apartarse de la guia. Q/E baja/sube; Ctrl afina y Shift acelera los desplazamientos sin arco.

Velocidades con los valores actuales de Main: normal 30 mm/s, con arco 6 mm/s, Ctrl 3 mm/s, Shift sin arco 120 mm/s. Se normalizan las diagonales. La orientacion habitual baja a 18 grados/s; Ctrl conserva 9 grados/s. Las pausas de render no se convierten en grandes saltos de movimiento.

## Implementacion

- En teclado, el objetivo se expresa en `WeldingTorch.ArcOriginPosition`. El controlador convierte esa posicion al TCP logico usando el desplazamiento fisico medido en cada frame, compensando inclinacion y desgaste. `PandaIK.endEffector` sigue siendo `Soldador`.
- Suavizado minimo de 80 ms; avance manual del objetivo limitado a 25 mm respecto a la punta real para evitar acumular una orden lejana cuando el brazo se retrasa.
- Una unica esfera verde pequena indica el destino de la punta. La esfera del objetivo logico se oculta durante este modo.
- Barrido por raycast entre punta actual y destino contra superficies, sin triggers ni colliders del propio robot. Frena el objetivo a 3 mm del impacto. No se ha vuelto a activar el limite vertical global.
- B solo disponible en practica. La aproximacion respeta el limite de inclinacion existente, se cancela al intervenir y tiene timeout por etapa. Para N5 hay que presentar un sector accesible con Z/X; no fuerza al brazo a recorrer la cara inferior.
- La guia interpola segmentos de la costura, incluyendo el cierre circular, evitando saltos entre muestras. Cerca del trazo y con arco pedido corrige distancia y desviacion lateral; no avanza sola.
- Examen: control de punta, velocidades y proteccion conservados; aproximacion y guia asistida desactivadas. IMU fresca: se conserva el camino original del guante y su entrada hibrida.
- JSON/CSV registran `assistedControl` / `control_asistido`; formato JSON version 2. No se altera la puntuacion ni el modelo de deposicion.

## Alcance y verificacion

Backup anterior: `_Backups/Antes_control_soldadura_20261010`. Sin paquetes nuevos ni componentes nuevos en Main.

Pruebas reales del Editor: aproximacion desde el inicio de Main a 4,13 mm de arco; avance de unos 16 mm con arco estable a 3,29 mm; error de posicion tras girar la orientacion de 0,99 mm. Tambien se comprobo el freno contra chapa, la interpolacion del trazo, el registro de asistencia y su exclusion en examen. La prueba general anterior de referencias, curso, superficies y arco en puntos de N1–N5 paso.

La proteccion actua sobre el objetivo de la punta: no sustituye una simulacion de colisiones de todo el brazo, ni garantiza recuperar una punta que ya aparezca dentro de un collider. Si una aproximacion no termina, cancelar, elevar y volver a intentar.

Las pruebas y el build de este cambio se detallan en `Verificacion/Unity_control_punta.log`, `Verificacion/Unity_control_arco_final.log`, `Verificacion/Resultado_Unity.txt` y `Verificacion/Resultado_Build_PC.txt`. La validacion automatizada de la nueva asistencia usa N1; la inspeccion del alcance y arco del robot cubre puntos de N1–N5. Queda la prueba manual completa de los recorridos y el hardware real. El APK anterior no incluye estos ultimos cambios de control.
