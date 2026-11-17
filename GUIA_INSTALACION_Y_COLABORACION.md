# FrankaVR — instalación y trabajo en equipo

Guía para incorporarse al proyecto **RealidadVirtual** de Juan Stella. Actualizada el **17/11/2026** según los archivos del repositorio.

El proyecto simula una práctica de soldadura SMAW con un robot Franka Panda. Se puede trabajar y practicar con teclado y ratón en Windows, sin guante, ESP32 ni visor. Android/Cardboard y la comunicación con hardware tienen pasos adicionales y validaciones pendientes.

Repositorio: [Juan-Stella/RealidadVirtual](https://github.com/Juan-Stella/RealidadVirtual).

## 1. Qué instalar

| Programa | Versión o componente | Para qué sirve |
| --- | --- | --- |
| Git para Windows | Versión estable del sitio oficial; disponible en PATH | Clonar, actualizar y descargar dependencias Git de Unity |
| Unity Hub | Versión disponible en el sitio oficial | Instalar y gestionar el editor |
| Unity Editor | **6000.5.10f1**, revisión `3bd4f66ad299` | Abrir este proyecto con su versión exacta |
| GitHub Desktop | Opcional | Interfaz gráfica para Git |
| Visual Studio 2022 | Opcional; carga de trabajo **Desarrollo de juegos con Unity** | Editar y depurar C# |
| Android Build Support + SDK & NDK Tools + OpenJDK | Opcional; módulos de ese mismo editor en Hub | Generar el APK |

Descargas: [Git para Windows](https://git-scm.com/downloads/win), [Unity Hub](https://unity.com/download), [archivo de versiones de Unity](https://unity.com/releases/editor/archive), [GitHub Desktop](https://desktop.github.com/download/).

No hace falta instalar Visual Studio para probar la escena. Para la primera puesta en marcha, alcanza con Git, Hub y el editor exacto. Reservá espacio para el editor, los paquetes y la caché que Unity genera: el tamaño descargado del repositorio no representa el espacio final de trabajo.

### Instalar Git y comprobarlo

Durante la instalación de Git elegí la opción que permite usarlo desde la línea de comandos y aplicaciones de terceros. Después abrí una **nueva** ventana de PowerShell:

```powershell
git --version
```

Debe aparecer una versión. Si no reconoce el comando, terminá la instalación y reiniciá PowerShell y Unity Hub. Instalar GitHub Desktop por sí solo no garantiza que Unity encuentre `git` en PATH.

### Instalar Unity

1. Instalá Unity Hub e iniciá sesión con tu cuenta de Unity. Activá la licencia que corresponda a tu cuenta.
2. En **Installs / Instalaciones**, buscá **6000.5.10f1**. Si no aparece, entrá al archivo oficial de versiones, buscala y elegí instalar mediante Hub.
3. Instalá el editor. Los módulos Android pueden agregarse después.
4. No elijas otra versión solo porque sea más nueva. La versión de referencia está en `ProjectSettings/ProjectVersion.txt`.

## 2. Descargar el proyecto

Primero aceptá la invitación de Juan al repositorio con la cuenta de GitHub que vas a usar. Confirmá que podés abrir el enlace del repositorio con esa cuenta.

Usá una carpeta local fuera de OneDrive y otras carpetas sincronizadas. Por ejemplo, `C:\Proyectos\RealidadVirtual`. Evitá trabajar dentro de un ZIP: clonar conserva el historial y permite colaborar.

**Con GitHub Desktop:** iniciá sesión, elegí **File → Clone repository → URL**, pegá `https://github.com/Juan-Stella/RealidadVirtual.git` y elegí una ubicación local.

**Con PowerShell**, como alternativa:

```powershell
New-Item -ItemType Directory -Force -Path C:\Proyectos
Set-Location C:\Proyectos
git clone https://github.com/Juan-Stella/RealidadVirtual.git
Set-Location RealidadVirtual
```

Si se solicita autenticación, completá el inicio de sesión de GitHub en el navegador. No compartas contraseñas ni tokens en mensajes.

### Configurar tu identidad antes del primer commit

Desde la carpeta del proyecto ejecutá lo siguiente, **reemplazando ambos valores por tus datos reales**:

```powershell
git config user.name "TU NOMBRE Y APELLIDO"
git config user.email "TU CORREO VERIFICADO EN GITHUB"
git config --get user.name
git config --get user.email
```

Podés usar el correo privado `noreply` que GitHub muestra en la configuración de tu cuenta. No copies el correo de Juan ni dejes correos genéricos como `tu@email.com`: GitHub usa el correo del commit para atribuir contribuciones.

## 3. Abrir y dejar que Unity instale las dependencias

1. En Unity Hub, **Projects → Add / Add project from disk**.
2. Seleccioná la raíz clonada: la carpeta que contiene **Assets, Packages y ProjectSettings**. No selecciones `Assets` ni crees un proyecto vacío.
3. Abrila con **6000.5.10f1**.
4. Esperá la descarga de paquetes, la importación y la compilación. La primera apertura puede tardar bastante; mantené conexión a Internet.
5. Cuando termine, revisá **Window → General → Console**. Antes de probar, resolvé cualquier error rojo de compilación.

Las dependencias están en `Packages/manifest.json` y sus resoluciones en `Packages/packages-lock.json`. Incluyen Google Cardboard, URDF Importer, Input System **1.20.0** y AI Inference **2.6.1**, entre otras. Unity las resuelve automáticamente; Cardboard y URDF Importer necesitan Git e Internet.

No hay que copiar la carpeta `Library` de Juan ni descargar librerías una por una. No actualices paquetes ni agregues Oculus, SteamVR, XR Interaction Toolkit o una nueva configuración de renderizado para conseguir que abra: la configuración necesaria ya está versionada.

## 4. Primera prueba en PC

1. Abrí **`Assets/Main.unity`** desde la ventana Project.
2. Si el proyecto está apuntando a Android, en **File → Build Profiles** seleccioná Windows y cambiá a esa plataforma para esta prueba; esperá la reimportación.
3. Pulsá **Play** y hacé clic dentro de **Game** para que reciba el teclado.
4. Seleccioná el ejercicio **1**, iniciá práctica con **T** y consultá la ayuda con **F1**.
5. Probá cámara, movimiento fino y retirada antes de soldar. Terminá la práctica con **F** para ver resultados.

Parte del entorno se genera al ejecutar: que no se vea toda la nave en la vista Scene antes de Play no indica necesariamente una instalación incompleta. No ejecutes los menús de reconstrucción de la celda durante la instalación normal; la escena ya está preparada.

### Controles principales

| Acción | Control |
| --- | --- |
| Seleccionar ejercicio N1–N5 | `1`–`5` |
| Iniciar práctica / examen | `T` / `Y` |
| Finalizar / volver al menú | `F` / `M` |
| Mostrar ayuda | `F1` |
| Mover la herramienta | `W A S D`; `Q` baja y `E` sube |
| Movimiento preciso / rápido | Mantener `Ctrl` / `Shift` |
| Inclinar / girar herramienta | `I K J L` / `U O` |
| Enderezar; orientación recomendada en práctica | `V` |
| Solicitar arco de soldadura | Mantener `Espacio`, con la punta en condiciones válidas |
| Retirar la herramienta | `C` |
| Reponer electrodo / cambiar tipo | `R` / `Tab` |
| Ajustar amperaje / careta | `[` y `]` / `H` |
| Desplazar al observador en PC | Flechas; `Re Pág` / `Av Pág` suben o bajan |
| Zoom / restablecer zoom | Rueda del ratón / `Retroceso` |

Al mantener Espacio cerca de la costura, el control adapta el movimiento a la junta y reduce la velocidad; Q/E ajustan la separación de arco. Para comenzar, usá N1 en práctica, aproximá con Ctrl y observá la barra de arco. Soltar Espacio deja de solicitar el arco; C permite retirarse. El avance sobre la junta y la posición del observador son controles distintos.

Los controles se contrastaron con los scripts actuales. Si el proyecto cambia después de esta guía, usá F1 y el informe más reciente como referencia.

## 5. Qué comprobar antes de empezar a desarrollar

- Main abre sin errores rojos de compilación.
- En Play se ve el robot, la pieza y el HUD.
- T inicia práctica; el teclado mueve la herramienta y C permite retirarla.
- Flechas y rueda permiten acercar la vista.
- F muestra el resultado de la práctica.
- `git status` permite revisar qué archivos cambiaron tras abrir el proyecto.

El menú **FrankaVR → Verificacion** incluye **Control de punta**, **Cordon completo N1**, **Integracion N1-N5** y **Capturas del entorno**. Son verificaciones del proyecto, no pasos obligatorios para instalarlo. Guardá tu trabajo antes de ejecutarlas y mantené Unity en primer plano: las pruebas automáticas dependen de los tiempos del editor.

El informe [CONTROL_Y_PORTAELECTRODO_20261117.md](CONTROL_Y_PORTAELECTRODO_20261117.md) registra verificaciones anteriores aprobadas en Editor. Eso no prueba automáticamente tu instalación. Al redactar esta guía no se volvió a ejecutar Unity ni se generó un build nuevo.

## 6. Android y Cardboard — opcional

Hacé estos pasos después de conseguir que funcione en PC, y solo si vas a compilar para un teléfono.

1. Cerrá Unity. En Hub, junto a **6000.5.10f1**, elegí **Add modules** e instalá **Android Build Support**, **Android SDK & NDK Tools** y **OpenJDK**.
2. Reabrí el proyecto. En **Edit → Preferences → External Tools**, usá las herramientas Android instaladas con Unity. No apuntes a un Java o NDK distinto sin una razón concreta.
3. En **File → Build Profiles**, seleccioná Android y cambiá de plataforma. Esperá la importación.
4. Ejecutá **FrankaVR → 12 - Preparar Cardboard Android**. Configura el loader y excluye de Android las librerías nativas de escritorio de Assimp del importador URDF. Es especialmente relevante tras una clonación o regeneración de la caché.
5. Confirmá que **Assets/Main.unity** esté habilitada en la lista de escenas. Conservá la configuración del proyecto, que utiliza ARM64 y tiene Target SDK **36**.
6. Si Unity solicita instalar la plataforma SDK correspondiente, completá ese paso; tener Android Build Support instalado no garantiza que todos los niveles SDK ya estén descargados.
7. Usá **Build** y guardá el APK en una carpeta local como `Builds/Android`. Para **Build And Run**, necesitás un teléfono conectado, depuración USB habilitada y aceptar su autorización de conexión.

La preparación del editor y sus dependencias Android se describe también en la [documentación de Unity](https://docs.unity3d.com/es/current/Manual/android-sdksetup.html). Usá los módulos asociados a nuestro editor, aunque la documentación muestre versiones más recientes.

**Estado real:** el soporte está preparado, pero el build nuevo tras los últimos cambios, la experiencia Cardboard en dispositivo y el BLE real requieren validación. No es necesario resolver BLE para continuar el trabajo en PC. No interpretes un APK antiguo como prueba del código actual.

## 7. Problemas frecuentes

| Síntoma | Qué revisar primero |
| --- | --- |
| GitHub muestra 404 o no deja clonar | Invitación aceptada, cuenta correcta y URL. Un repositorio privado puede aparecer como inexistente sin acceso. |
| Unity no puede descargar Cardboard o URDF | Ejecutar `git --version`; revisar Internet; reiniciar Hub después de instalar Git; leer el error concreto en Package Manager. |
| Hub pide otra versión de Unity | Instalar 6000.5.10f1 y asignarla al proyecto. No migrarlo para salir del paso. |
| Primera apertura muy lenta | Dejar terminar importación y compilación; comprobar actividad y errores. `Library` se reconstruye localmente. |
| Safe Mode o errores rojos | Copiar el primer error completo con archivo y línea. Verificar versión y descarga de paquetes antes de cambiar scripts. |
| Teclas sin respuesta | Estar en Play, hacer clic en Game e iniciar práctica con T. |
| Falta entorno o aparece otra escena | Abrir Assets/Main.unity y entrar en Play. |
| Materiales magenta | Revisar Console y shaders/materiales del repo; no convertir todo a URP/HDRP. |
| Android falla por Assimp o plugins nativos duplicados | Ejecutar el menú 12 de preparación Android y revisar el error nuevamente. |
| Android no encuentra SDK, NDK o Java | Revisar módulos instalados en ese editor y Preferences → External Tools. |
| Aparecen muchos cambios sin haber programado | Comprobar versión del editor y revisar `git diff`; no subir cambios masivos sin entenderlos. |

No borres `Assets`, `Packages`, `ProjectSettings` ni archivos `.meta` como solución a una importación. Si hay que regenerar `Library`, cerrá Unity primero, asumí la descarga/importación completa y repetí la preparación Android si corresponde. Antes de hacerlo conviene identificar el error original.

Para pedir ayuda, enviá la versión del editor, la plataforma activa, los pasos para reproducir, el primer error completo y el commit (`git rev-parse --short HEAD`). No hace falta compartir toda la caché.

## 8. Trabajar entre dos sin pisarse cambios

Acuerden quién edita **Main.unity** y los prefabs: son archivos serializados y resolver conflictos manualmente es delicado. Dividan tareas por scripts o funcionalidades cuando sea posible.

Antes de empezar, cerrá Unity y verificá que no tengas cambios pendientes. Desde la raíz:

```powershell
git status
git switch main
git pull --ff-only
git switch -c trabajo/nombre-tarea
```

Reemplazá `nombre-tarea` por algo concreto y usá una rama nueva para cada tarea. Si ya trabajás en una rama existente, volvé a esa rama en lugar de crearla otra vez. Si `git status` muestra trabajo pendiente, guardalo con un commit en su rama antes de cambiar de contexto; no lo descartes automáticamente.

Al terminar, probá en Unity, salí de Play, guardá los cambios que correspondan y revisá:

```powershell
git status
git diff --stat
git diff
```

Agregá solo archivos de tu cambio. Ejemplo para un script nuevo, ajustando la ruta real:

```powershell
git add Assets/Scripts/MiScript.cs Assets/Scripts/MiScript.cs.meta
git diff --cached
git commit -m "Describe el cambio y su finalidad"
git push -u origin trabajo/nombre-tarea
```

Después abrí un **Pull Request** hacia `main` en GitHub e indicá qué cambiaste y cómo lo probaste. El compañero revisa antes de integrarlo. En GitHub Desktop se puede hacer el mismo flujo con ramas, selección de archivos, commit y Publish/Push.

Reglas prácticas:

- Subir los `.meta` junto con sus assets; hacer movimientos y renombrados desde Unity para conservar referencias.
- No usar `push --force` como parte del trabajo normal.
- No resolver conflictos en escenas eligiendo todo de un lado sin revisar con el compañero.
- No subir `Library`, `Temp`, `Logs`, builds o backups. El `.gitignore` ya los contempla.
- No agregar ZIP de parches encima del proyecto ni duplicar scripts/HUDs.
- Mantener teclado funcional. Leer `AGENTS.md` antes de tocar controles, IK, soldadura o referencias.

### Aviso por corrección del historial del 17/11/2026

Los commits antiguos tenían el correo genérico `tu@email.com`, que GitHub asociaba a `marjosavi481`. Se corrigió la identidad a la de Juan manteniendo archivos, mensajes, fechas y coautorías. Eso cambia los identificadores de los commits.

**Si todavía no clonaste:** cloná normalmente; no hay ningún paso especial.

**Si ya tenías una copia anterior a esa corrección:** lo más sencillo es conservarla y clonar de nuevo en otra carpeta. Si tenés cambios propios, no borres la copia anterior: trasladá únicamente tus cambios y sus `.meta` a la copia nueva, revisándolos con Juan. No mezcles el historial anterior mediante un merge ni uses `reset --hard` para resolverlo perdiendo trabajo.

## 9. Qué está y qué falta

El repositorio incluye código, escena, assets, configuración, dependencias declaradas e informes. Unity reconstruye su caché al abrirlo. Los backups y ejecutables locales de Juan no se descargan al clonar.

El trabajo reciente incluye corrección del porta-electrodo, mejoras de control sobre la costura, protección del codo, HUD y entorno industrial. Los siguientes pasos documentados son validar nuevos builds Windows/Android, completar pruebas manuales de N2–N5 y examen, y probar Cardboard/BLE con hardware.

Orden recomendado de lectura: [README.md](README.md), [CONTROL_Y_PORTAELECTRODO_20261117.md](CONTROL_Y_PORTAELECTRODO_20261117.md), [AGENTS.md](AGENTS.md) y [CONTEXTO_FRANKAVR_CODEX.md](CONTEXTO_FRANKAVR_CODEX.md). Los informes anteriores explican antecedentes; el proyecto actual en disco es la fuente de verdad.
