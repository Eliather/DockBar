# DockBar
DockBar es una barra lateral de accesos directos estilo dock para Windows desarrollada en C# y WPF. La versión `1.8.4` introduce la barra de búsqueda y progreso multimedia interactiva (Seek Bar) con diseño ultra-compacto, monitor de recursos con soporte avanzado Multi-GPU (NVIDIA, AMD e Intel), modo Cafeína (Keep-Awake) para evitar la suspensión del equipo, previsualización en vivo en el diálogo de Ajustes y localización completa en 4 idiomas.

<img width="256" height="256" alt="Dock" src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" />

<img width="1920" height="1080" alt="{0A9D8F93-0DB9-42CC-979A-CF66218595FC}" src="https://github.com/user-attachments/assets/302484ca-4aa6-4e54-9b0d-35e484cdc4ff" />

Video de demostración:
https://github.com/user-attachments/assets/9a4ea52f-8131-471e-8bd3-89122aa3dec7

---

## Descripción general
DockBar proporciona una barra lateral compacta y moderna para Windows con soporte para accesos directos, modo de edición, ocultación automática fluida, configuración persistente y un panel de ajustes totalmente rediseñado con pestañas y soporte para funciones experimentales.

El proyecto está diseñado bajo cinco prioridades esenciales:

- **Efecto Glass y estética unificada en toda la aplicación**: Composición por hardware DWM (`WindowChrome GlassFrameThickness="-1"`) extendida a todas las ventanas secundarias (Ajustes, Agregar Enlace, Apps Instaladas, Actualizaciones, Renombrar, etc.), respetando la opacidad, color de fondo y efecto Glass elegidos por el usuario.
- **Pestañas de Configuración por Categorías**: Organización modular en el menú de Ajustes dividida en categorías independientes: **Básica**, **Reloj**, **Multimedia** y funciones **Experimentales** (monitor de recursos, Multi-GPU, modo Cafeína y organizador de widgets), con almacenamiento desacoplado en el archivo de configuración.
- **Paleta de Énfasis / Acento Secundaria**: Personalización para botones principales (como *Guardar*), deslizadores (sliders), switches, cajas de selección y resaltados interactivos.
- **Selector de Color Dual HSV y HEX**: El lienzo interactivo de saturación/brillo, el deslizador de tono y la entrada hexadecimal pueden utilizarse tanto para el fondo del dock como para el color de énfasis de los botones.
- **Instancia única y rendimiento nativo**: Enumeración instantánea de aplicaciones y juegos mediante APIs nativas Win32 Shell COM (< 5 ms de tiempo de respuesta) sin subprocesos lentos ni dependencias pesadas.

---

## Novedades en la versión 1.8.4
- **Barra de Progreso y Búsqueda Multimedia Interactiva (Seek Bar)**:
  - Deslizador de reproducción para canciones, videos y podcasts directamente en el dock (compatible con Spotify, YouTube, navegadores y reproductores multimedia).
  - Indicadores numéricos de tiempo transcurrido y duración (`0:00 / 3:45`).
  - Previsualización fluida de tiempo al arrastrar y salto exacto al soltar el ratón sin saturar el reproductor.
  - Ajuste rápido con la rueda del ratón (`MouseWheel`, ±5s).
  - Estilo dedicado `MediaSeekSliderStyle`: diseño ultra-compacto de 14px con track de 4px y thumb circular de 10px con acento y sombra suave.
  - Protección de buffer y anti-rebotes: previene reinicios a `00:00` durante la carga de streaming.
  - Opcional y configurable con casilla de verificación en *Ajustes > Experimental*.
- **Monitor de Recursos y Detección Multi-GPU**:
  - Detección y monitorización integral de múltiples tarjetas gráficas (NVIDIA NVML, AMD ADLX / DXGI, Intel).
  - Detección de nombres reales de modelos en lugar de etiquetas genéricas.
  - Seguimiento de carga y memoria por GPU seleccionada.
- **Modo Cafeína (Keep-Awake)**:
  - Evita que Windows apague la pantalla o entre en reposo durante descargas, tareas largas o presentaciones.
- **Control de Previsualización en Vivo (Dock Preview)**:
  - Previsualización dinámica en tiempo real dentro del panel de Ajustes antes de guardar los cambios.
- **Localización Completa**:
  - Traducciones completas en español, inglés, ruso y chino simplificado.

---

## Novedades en la versión 1.8.3
- **Controlador de Volumen Dinámico (Experimental)**:
  - Widget integrado en el dock con deslizador fluido, indicador numérico de porcentaje (`0-100%`) y botón de silencio/reactivación con iconos según el nivel.
  - Ajuste rápido mediante la rueda del ratón (`MouseWheel`) sobre el panel (+-2%).
  - **Conmutación en caliente de dispositivos (`IMMNotificationClient`)**: Detecta automáticamente si cambias entre auriculares inalámbricos (ej. Logitech G733) y parlantes/altavoces (Realtek), reconectando el control de volumen sin reiniciar la aplicación.
  - Tooltip informativo con el nombre amigable del dispositivo activo y porcentaje.
  - Clic derecho en el control de volumen para abrir al instante la configuración de sonido de Windows (`ms-settings:sound`).
- **Controlador Multimedia Inteligente (Experimental)**:
  - Widget compacto en el dock conectado a Windows System Media Transport Controls (compatible con Spotify, navegadores Chrome/Edge, reproductores de música y video).
  - Botones interactivos de **Reproducir / Pausar** y **Siguiente Pista**.
  - **Marquesina animada en tiempo real (`MarqueeTextBlock`)**: Si el nombre de la canción o el artista es largo, el texto se desplaza suavemente de forma continua para que siempre puedas leer la información de reproducción completa.
- **Calibración de la Zona de Activación de Borde (Trigger / Hotspot)**:
  - Nuevo deslizador de píxeles (1 a 12 px) ubicado en *Configuración básica* debajo del auto-ocultamiento.
  - Permite personalizar qué tan pegado al extremo izquierdo o derecho de la pantalla debe estar el puntero para desplegar la barra.
- **Botón de Aplicar y Diálogo de Confirmación con Temporizador**:
  - Nuevo botón **Aplicar** en la ventana de Ajustes para probar los cambios en vivo.
  - Al aplicar o guardar, se despliega una ventana de confirmación interactiva con una cuenta regresiva de **5 segundos**: si el usuario no presiona "Guardar cambios", los ajustes previos se restauran automáticamente.
- **Scrollbar Moderno en Ajustes > Experimental**:
  - Reemplazo completo de las barras de desplazamiento nativas clásicas con flechas cuadradas por un riel minimalista oscuro con thumb tipo píldora redondeada y resaltado dinámico de acento, replicando la estética del modo edición.
- **Correcciones Ergonómicas y de Diseño**:
  - Corrección de desbordamiento horizontal y alineación en pantallas al situar la barra en el lateral derecho durante el modo edición.
  - Inversión de dirección del deslizador vertical de edición para una navegación vertical más intuitiva.
  - La ventana de Ajustes ahora siempre se abre centrada en la pantalla y restaura el estado de auto-ocultamiento del dock inmediatamente al cerrarse.

---

## Características principales
- Barra lateral sin bordes para el lateral izquierdo o derecho con comportamiento siempre visible (TopMost).
- Ocultación automática suave con borde sensible interactivo.
- Arrastrar y soltar (Drag and Drop) para ejecutables (`.exe`), accesos directos (`.lnk`) y carpetas.
- Integración nativa con librerías y juegos de Steam con extracción automática de íconos en alta resolución.
- Selector instantáneo de aplicaciones instaladas de Microsoft Store y del sistema.
- Modo de edición para reordenar, renombrar, cambiar íconos o eliminar elementos.
- Paginación automática en modo normal cuando los accesos directos exceden la altura de la pantalla.
- Icono en el área de notificación (bandeja del sistema) con acciones rápidas.
- Oculto de los selectores de tareas de Windows (Alt+Tab y Win+Tab).
- Configuración persistente guardada en `%AppData%\DockBar\shortcuts.json`.

---

## Requisitos
- Windows 10 o Windows 11 (64-bit)
- .NET SDK 10.0 (o runtime .NET 10 para ejecutar el binario)
- Visual Studio 2022 / 2026, VS Code o terminal con `dotnet`

---

## Compilación y ejecución

Para compilar y ejecutar en modo depuración:
```bash
dotnet build
dotnet run
```

Para generar la versión de publicación optimizada:
```bash
dotnet publish DockBar.csproj -c Release -r win-x64 --self-contained false -o publish
```

---

## Configuración
Ubicación del archivo de configuración del usuario:
```text
%AppData%\DockBar\shortcuts.json
```

Ejemplo de estructura `shortcuts.json`:
```json
{
  "DockSide": "Left",
  "DockWidth": 175,
  "IconSize": 40,
  "AutoHideDelaySeconds": 0,
  "HideAnimationMs": 200,
  "UseTransparency": true,
  "BackgroundOpacity": 0.45,
  "BackgroundR": 0,
  "BackgroundG": 0,
  "BackgroundB": 0,
  "AccentR": 55,
  "AccentG": 115,
  "AccentB": 245,
  "UseLightText": true,
  "EnableTextShadow": true,
  "AutoStartEnabled": false,
  "Shortcuts": [
    { "Name": "Explorador", "Path": "C:\\Windows\\explorer.exe" },
    { "Name": "Documentos", "Path": "C:\\Users\\Public\\Documents" },
    { "Name": "Steam", "Path": "C:\\Program Files (x86)\\Steam\\Steam.exe" }
  ]
}
```

---

## Arquitectura de componentes
- [MainWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/MainWindow.xaml.cs): Barra lateral principal, interacción táctil/ratón, animación de visibilidad y detección de juegos.
- [SettingsWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/SettingsWindow.xaml.cs): Panel de personalización con selector dual HSV/HEX de fondo y énfasis, sliders y paletas de color.
- [ThemeService.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/Services/ThemeService.cs): Motor centralizado de temas dinámicos y composición DWM Glass por hardware.
- [StoreAppPickerWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/StoreAppPickerWindow.xaml.cs): Selector de aplicaciones UWP y de la tienda Windows.
- [AddLinkWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/AddLinkWindow.xaml.cs): Diálogo para añadir ejecutables, carpetas, URLs web o comandos de sistema.
- [AudioService.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/Services/AudioService.cs): Servicio nativo Windows CoreAudio con conmutación dinámica de endpoints (`IMMNotificationClient`) y control de volumen del sistema.
- [MediaService.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/Services/MediaService.cs): Integración con SystemMediaTransportControls para detección y control de pistas en Spotify, navegadores y reproductores.
- [MarqueeTextBlock.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/Controls/MarqueeTextBlock.cs): Control de texto con animación de desplazamiento continuo (marquesina) para títulos dinámicos en el dock.
- [UpdateWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/UpdateWindow.xaml.cs): Diálogo de comprobación e instalación de actualizaciones.

---

## Licencia y Créditos
Desarrollado por **Eliather**. Licenciado bajo la Licencia MIT.
