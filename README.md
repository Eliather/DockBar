# DockBar
DockBar es una barra lateral de accesos directos estilo dock para Windows desarrollada en C# y WPF. La versión `1.8.2` introduce un nuevo sistema de pestañas en el panel de Ajustes (Configuración básica y Experimental), un widget de reloj en tiempo real para aprovechar el espacio libre de la barra al estilo de Windows y un sistema de guardado experimental desacoplado y seguro contra corrupciones.

<img width="256" height="256" alt="Dock" src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" />

<img width="1920" height="1080" alt="{0A9D8F93-0DB9-42CC-979A-CF66218595FC}" src="https://github.com/user-attachments/assets/302484ca-4aa6-4e54-9b0d-35e484cdc4ff" />

Video de demostración:
https://github.com/user-attachments/assets/9a4ea52f-8131-471e-8bd3-89122aa3dec7

---

## Descripción general
DockBar proporciona una barra lateral compacta y moderna para Windows con soporte para accesos directos, modo de edición, ocultación automática fluida, configuración persistente y un panel de ajustes totalmente rediseñado con pestañas y soporte para funciones experimentales.

El proyecto está diseñado bajo cinco prioridades esenciales:

- **Efecto Glass y estética unificada en toda la aplicación**: Composición por hardware DWM (`WindowChrome GlassFrameThickness="-1"`) extendida a todas las ventanas secundarias (Ajustes, Agregar Enlace, Apps Instaladas, Actualizaciones, Renombrar, etc.), respetando la opacidad, color de fondo y efecto Glass elegidos por el usuario.
- **Pestañas y Funciones Experimentales**: Organización clara en el menú de Ajustes entre la configuración básica y las funciones experimentales en desarrollo, como el reloj en tiempo real.
- **Paleta de Énfasis / Acento Secundaria**: Personalización para botones principales (como *Guardar*), deslizadores (sliders), switches, cajas de selección y resaltados interactivos.
- **Selector de Color Dual HSV y HEX**: El lienzo interactivo de saturación/brillo, el deslizador de tono y la entrada hexadecimal pueden utilizarse tanto para el fondo del dock como para el color de énfasis de los botones.
- **Instancia única y rendimiento nativo**: Enumeración instantánea de aplicaciones y juegos mediante APIs nativas Win32 Shell COM (< 5 ms de tiempo de respuesta) sin subprocesos lentos ni dependencias pesadas.

---

## Novedades en la versión 1.8.2
- **Pestañas en el Menú de Ajustes**:
  - Navegación segmentada en la parte superior de la ventana de Ajustes con dos vistas: **Configuración básica** (tamaño, auto-ocultamiento, colores y Glass) y **Experimental** (funciones avanzadas y en desarrollo).
- **Reloj en Tiempo Real en la Barra (Dock)**:
  - Aprovecha el espacio libre que queda en las barras donde no cabe otro programa para mostrar la hora y fecha continua como en Windows.
  - Ubicado de forma ergonómica sobre la paginación y los botones de acción del dock.
  - Personalizable con formato 24 horas (ej. `14:25`) o 12 horas con AM/PM (ej. `02:25 PM`).
  - Opción de mostrar segundos en tiempo real (`:45`) y fecha debajo de la hora.
  - Tooltip con fecha completa al pasar el cursor por encima.
- **Slider de Tamaño de Fuente para el Reloj**:
  - Deslizador y caja numérica (10 a 36 px) para graduar con exactitud el tamaño del reloj.
  - Vista previa en tiempo real en la pestaña Experimental que refleja el tamaño, colores y tipografía del dock.
  - Ajuste dinámico del espacio de los accesos directos para evitar solapamientos.
- **Sistema de Guardado Experimental Aislado y Seguro**:
  - Las opciones experimentales se serializan en un bloque desacoplado `Experimental: { ... }` en `shortcuts.json`.
  - Rescate defensivo automático contra corrupciones de archivo para proteger siempre los accesos directos del usuario.

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
- [TrayMenuWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/TrayMenuWindow.xaml.cs): Menú flotante del área de notificación.
- [UpdateWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/UpdateWindow.xaml.cs): Diálogo de comprobación e instalación de actualizaciones.

---

## Licencia y Créditos
Desarrollado por **Eliather**. Licenciado bajo la Licencia MIT.
