# DockBar 1.8.4 Release Notes

DockBar `v1.8.4` introduce la **Barra de Progreso y Búsqueda Multimedia Interactiva (Seek Bar)** en el widget multimedia con soporte de salto, rueda de ratón y diseño circular sin recortes; añade **soporte Multi-GPU y monitorización avanzada de hardware** para NVIDIA, AMD e Intel con detección de nombres de modelos; incorpora el **Modo Cafeína (Keep-Awake)** para evitar la suspensión del equipo; integra un **Control de Previsualización en Tiempo Real** en el panel de Ajustes; y amplía la localización en todos los idiomas.

---

## ✨ Novedades y Mejoras Principales (v1.8.4)

### 1. 🎵 Barra de Progreso y Búsqueda Multimedia (Media Seek Bar)
- **Deslizador interactivo para audio y video**:
  - Permite avanzar o retroceder la reproducción directamente desde el dock para canciones, podcasts y videos (Spotify, YouTube, Edge, Chrome, reproductores del sistema).
  - Etiquetas numéricas de tiempo transcurrido y duración total (`0:00 / 3:45`) con tipografía nítida y semi-negrita.
  - **Búsqueda fluida (Scrubbing)**: Arrastra el cursor con previsualización en tiempo real del tiempo y salto exacto al nuevo punto únicamente al soltar el ratón, evitando saturar el reproductor.
  - **Ajuste fino con rueda de ratón**: Gira la rueda del mouse sobre la barra para avanzar o retroceder en saltos de ±5 segundos.
  - **Diseño Ultra-Compacto (`MediaSeekSliderStyle`)**: Altura de 14px, pista delgada de 4px con relleno de acento y cursor circular de 10px con sombra suave, eliminando cualquier recorte visual.
  - **Filtro Anti-Rebote y Protección de Buffer**: Previene el reinicio a `00:00` y conserva la duración real cuando navegadores web o plataformas de streaming envían datos transitorios de búfer.
  - Opcional: Se activa o desactiva con un checkbox en *Ajustes > Multimedia*.

### 2. 🎮 Monitor de Recursos y Soporte Multi-GPU
- **Detección y Monitoreo Multi-GPU**:
  - Detección exhaustiva de hardware gráfico (NVIDIA NVML, AMD ADLX / DXGI, Intel, GPUs integradas y discretas).
  - Selección dinámica de GPU para monitorizar en tiempo real el uso de carga gráfica y memoria.
  - Detección y presentación del nombre real del hardware (ej. "RTX 4070", "Radeon RX 7800 XT", "Intel Iris Xe") en lugar de etiquetas genéricas.

### 3. ☕ Modo Cafeína (Keep-Awake)
- **Prevención de suspensión con un solo clic**:
  - Previene que Windows active el salvapantallas, apague la pantalla o entre en suspensión (`ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED`) mientras realizas tareas largas, renders o descargas.
  - Activación o desactivación instantánea desde el dock.

### 4. 🪟 Control de Previsualización en Vivo en Ajustes (Dock Preview)
- Previsualización en vivo integrada en el diálogo de Ajustes que refleja instantáneamente tus cambios en el tamaño de iconos, colores, opacidad y posición de la barra antes de guardarlos.

### 5. 📑 Categorías de Configuración Dedicadas (Reloj y Multimedia)
- **Separación modular de ajustes**:
  - **Reloj** y **Multimedia** cuentan ahora con sus propias categorías de configuración exclusivas e independientes de las opciones experimentales.
  - Estructuración limpia en el archivo de guardado (`shortcuts.json`) con esquemas dedicados (`Clock` y `Media`) y migración automática y transparente desde versiones anteriores.

### 6. 🌐 Localización Completa (i18n)
- Traducción completa de todas las nuevas opciones al **Español**, **Inglés**, **Ruso** y **Chino Simplificado** con fallback automático a inglés.
- Añadí bugs que arreglaré en futuras versiones.

---

## 📦 Archivos del Release / Downloads
- **Instalador clásico / Classic Installer**: `DockBarSetup.exe` (v1.8.4).
- **Paquete MSIX / MSIX Package**: `DockBar.msix` (v1.8.4.0).
- **Portable ZIP (x64)**: `DockBar-win-x64-v1.8.4.zip`.

---

# DockBar 1.8.3 Release Notes

DockBar `v1.8.3` expande los widgets experimentales con un **controlador de volumen dinámico en caliente** y un **reproductor multimedia inteligente con marquesina animada**, añade **calibración en píxeles del borde de activación** en configuración básica, introduce el botón **Aplicar con confirmación temporizada de 5 segundos**, moderniza el diseño de las barras de desplazamiento y soluciona problemas ergonómicos en el modo edición.

---

## ✨ Novedades y Mejoras Principales (v1.8.3)

### 1. 🔊 Controlador de Volumen Dinámico (Experimental)
- **Control fluido y directo en el Dock**:
  - Deslizador horizontal moderno integrado en la barra con porcentaje numérico (`0% - 100%`) e icono de altavoz interactivo.
  - **4 estados de icono**: Silenciado/0%, bajo (<35%), medio (<70%) y alto (>=70%).
  - **Ajuste rápido con rueda del ratón**: Gira la rueda del mouse sobre el panel de volumen para subir o bajar el volumen en saltos de 2%.
- **Conmutación en caliente de dispositivos (`IMMNotificationClient`)**:
  - Escucha notificaciones del subsistema CoreAudio de Windows: si conectas o desconectas auriculares inalámbricos (ej. Logitech G733) o alternas con los altavoces de la PC, DockBar se enlaza automáticamente en caliente al nuevo dispositivo sin reiniciar la app.
  - Prioridad sobre el rol `eConsole` con fallback a `eMultimedia` para respetar siempre la selección del menú de sonido de la barra de tareas de Windows 11/10.
  - **Identificación en Tooltip**: Colocar el cursor sobre el panel de volumen muestra el nombre del dispositivo activo (ej. `Altavoces (G733 Gaming Headset) • 54% (Silenciar)`).
  - **Acceso rápido**: Hacer clic derecho en el panel de volumen abre directamente la configuración de sonido de Windows (`ms-settings:sound`).

### 2. 🎵 Controlador Multimedia Inteligente (Experimental)
- **Integración con System Media Transport Controls**:
  - Compatible con Spotify, navegadores web (Chrome, Edge, Firefox, Brave) y reproductores multimedia del sistema.
  - Botones táctiles de **Reproducir / Pausar** y **Siguiente Pista**.
- **Marquesina Animada en Vivo (`MarqueeTextBlock`)**:
  - Si el título de la canción o el nombre del artista excede el ancho del dock, el texto se desplaza suavemente de manera continua para que siempre puedas leer toda la información de lo que estás escuchando.

### 3. 🎯 Calibración del Borde de Activación (Trigger / Hotspot)
- **Ajuste fino en Configuración Básica**:
  - Ubicado intuitivamente debajo de la opción de auto-ocultamiento.
  - Deslizador de 1 a 12 píxeles con caja numérica para calibrar qué tan cerca del borde izquierdo o derecho de la pantalla debe situarse el puntero para desplegar la barra lateral.

### 4. ⏱️ Botón de Aplicar y Diálogo Temporizado de 5 Segundos
- **Previsualización segura de cambios**:
  - Nuevo botón **Aplicar** en la ventana de Ajustes para probar temas, opacidad y opciones sin cerrar la ventana.
  - Al aplicar o guardar cambios, se presenta un diálogo de confirmación con un contador visual en cuenta regresiva de **5 segundos**.
  - Si el usuario no confirma presionando "Guardar cambios", los ajustes previos se restauran automáticamente.

### 5. 🎨 Barras de Desplazamiento Modernas
- **Cero elementos Windows 95**:
  - Creación de `ModernScrollBarStyle` y `ModernScrollViewerStyle`, eliminando por completo los botones grises con flechas cuadradas.
  - Riel minimalista de 4-6 px con esquinas redondeadas en color de fondo y thumb tipo cápsula redondeada que resalta con el color de acento al pasar el cursor o arrastrar, replicando el diseño del modo edición.

### 6. 🛠️ Correcciones Ergonómicas y Estabilidad
- **Modo Edición en lateral derecho**: Se corrigió el cálculo de márgenes y alineación horizontal que provocaba que la lista de accesos directos se desbordara de la pantalla cuando el dock estaba posicionado a la derecha.
- **Inversión de scroll en edición**: La dirección del deslizador vertical se ajustó para un desplazamiento ergonómico estándar.
- **Ventana de Ajustes**: Ahora se inicializa siempre en el centro de la pantalla y reactiva de inmediato el auto-ocultamiento del dock al cerrarse.
- **Soporte Multi-Idioma (Ruso y Chino Simplificado)**: Se incorporó la localización integral al ruso (`ru-RU`) y al chino simplificado (`zh-CN`) en todos los componentes de la aplicación (Dock, Ajustes, Selector de Apps, Menú del sistema, Diálogos y Actualizaciones), manteniendo una cadena de fallback robusta donde cualquier otro idioma no cubierto utiliza el inglés como estándar global.

---

## 📦 Archivos del Release / Downloads
- **Instalador clásico / Classic Installer**: `DockBarSetup.exe` (v1.8.3).
- **Paquete MSIX / MSIX Package**: `DockBar.msix` (v1.8.3.0).
- **Portable ZIP (x64)**: `DockBar-win-x64-v1.8.3.zip`.

---

# DockBar 1.8.2 Release Notes

DockBar `v1.8.2` introduce un nuevo sistema de pestañas en el panel de Ajustes (Configuración básica y Experimental), un widget de reloj en tiempo real para aprovechar el espacio libre de la barra al estilo de Windows y un sistema de guardado experimental desacoplado y seguro contra corrupciones.

---

## ✨ Novedades y Mejoras Principales

### 1. 📑 Pestañas en el Menú de Ajustes
- **Navegación segmentada moderna**:
  - En la parte superior de la ventana de Ajustes ahora dispones de dos secciones organizadas con botones segmentados:
    - **Configuración básica**: Agrupa el tamaño de la barra, tamaño de iconos, auto-ocultamiento, inicio con Windows, opacidad y el sistema dual de colores Glass y Énfasis.
    - **Experimental**: Pestaña dedicada a características en desarrollo para expandir las capacidades del dock sin sobrecargar el menú principal.

### 2. 🕒 Reloj en Tiempo Real en la Barra (Dock)
- **Aprovechamiento del espacio libre**:
  - Diseñado especialmente para aprovechar el espacio vertical inferior que queda libre cuando los accesos directos no llenan la pantalla o no cabe un programa adicional.
  - Ubicado de forma elegante sobre la barra de paginación (`Página 1/2`) y los botones de acción (`+`, `=`, etc.), emulando la bandeja del sistema de Windows.
- **Personalización completa**:
  - **Formato 24 horas / 12 horas**: Elige entre notación estándar (ej. `14:25`) o AM/PM (ej. `02:25 PM`).
  - **Segundos en tiempo real**: Opción para mostrar el avance de los segundos (`:45`).
  - **Fecha bajo la hora**: Muestra el día de la semana y fecha abreviada (ej. `sáb., 5 sept.`).
  - **Tooltip con fecha completa**: Al colocar el cursor sobre el reloj, se despliega la fecha detallada del sistema.
- **Slider de tamaño de fuente (10 a 36 px)**:
  - Permite ajustar con un deslizador o caja numérica la escala exacta del texto del reloj.
  - La fecha escala proporcionalmente según el tamaño seleccionado.
- **Vista previa interactiva en vivo**:
  - En la pestaña *Experimental* puedes visualizar el reloj funcionando en tiempo real con los colores, opacidad y tipografía de tu dock antes de guardar.
- **Ajuste automático de paginación**:
  - La barra descuenta la altura del reloj de forma dinámica para que los accesos directos nunca se solapen ni se corten.

### 3. 🛡️ Sistema de Guardado Experimental Aislado y Seguro
- **Bloque de configuración desacoplado**:
  - Las opciones experimentales se serializan en su propio objeto `"Experimental": { ... }` en `shortcuts.json`, manteniendo intacto el esquema base.
- **Migración retrocompatible**:
  - Si existen versiones anteriores o valores en la raíz, el cargador los lee y migra sin fricción.
- **Mecanismo de rescate defensivo**:
  - Si un parámetro experimental tuviese un formato anómalo, el cargador extrae y preserva intactos todos los accesos directos (`Shortcuts`), impidiendo que el guardado se corrompa o que salte el diálogo de error de lectura.

---

## 📦 Archivos del Release / Downloads
- **Instalador clásico / Classic Installer**: `DockBarSetup.exe` (v1.8.2).
- **Paquete MSIX / MSIX Package**: `DockBar.msix` (v1.8.2.0).
- **Portable ZIP (x64)**: `DockBar-win-x64-v1.8.2.zip`.

---

## 💻 Requisitos
- Windows 10 (versión 19041+) o Windows 11 (64-bit).
- .NET Desktop Runtime 10.0 (x64).
