# DockBar
DockBar es una barra lateral de accesos directos estilo dock para Windows desarrollada en C# y WPF. La versión `1.8.0` renueva integralmente el sistema visual de toda la aplicación, unificando todos los menús, diálogos y ventanas de configuración bajo una estética cristalina DWM Glass pura, controles semiredondeados, paleta de énfasis secundaria personalizable y selector de color dual HSV/HEX interactivo.

<img width="256" height="256" alt="Dock" src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" />

<img width="1920" height="1080" alt="{0A9D8F93-0DB9-42CC-979A-CF66218595FC}" src="https://github.com/user-attachments/assets/302484ca-4aa6-4e54-9b0d-35e484cdc4ff" />

Video de demostración:
https://github.com/user-attachments/assets/9a4ea52f-8131-471e-8bd3-89122aa3dec7

---

## Descripción general
DockBar proporciona una barra lateral compacta y moderna para Windows con soporte para accesos directos, modo de edición, ocultación automática fluida, configuración persistente y un panel de ajustes totalmente rediseñado.

El proyecto está diseñado bajo cinco prioridades esenciales:

- **Efecto Glass y estética unificada en toda la aplicación**: Composición por hardware DWM (`WindowChrome GlassFrameThickness="-1"`) extendida a todas las ventanas secundarias (Ajustes, Agregar Enlace, Apps Instaladas, Actualizaciones, Renombrar, etc.), respetando la opacidad, color de fondo y efecto Glass elegidos por el usuario.
- **Paleta de Énfasis / Acento Secundaria**: Nueva personalización para botones principales (como *Guardar*), deslizadores (sliders), switches, cajas de selección y resaltados interactivos.
- **Selector de Color Dual HSV y HEX**: El lienzo interactivo de saturación/brillo, el deslizador de tono y la entrada hexadecimal pueden utilizarse tanto para el fondo del dock como para el color de énfasis de los botones.
- **Instancia única y rendimiento nativo**: Enumeración instantánea de aplicaciones y juegos mediante APIs nativas Win32 Shell COM (< 5 ms de tiempo de respuesta) sin subprocesos lentos ni dependencias pesadas.
- **Detección robusta de juegos y modo ventana completa**: Reconocimiento inteligente de juegos en pantalla completa exclusiva o ventana sin bordes (*Borderless Windowed*), evitando aperturas accidentales del dock durante las partidas.

---

## Novedades en la versión 1.8.0
- **Estética Unificada DWM Glass en Todas las Ventanas y Menús**:
  - Todas las ventanas secundarias (`SettingsWindow`, `AddLinkWindow`, `StoreAppPickerWindow`, `UpdateWindow`, `AboutWindow`, `RenameWindow`, `ThemedMessageDialogWindow` y `TrayMenuWindow`) han sido reconstruidas con `WindowChrome` nativo sin bordes opacos ni barras de título estándar del sistema operativo.
  - El fondo de cada ventana y menú hereda la composición Glass DWM real y la opacidad configurada en el dock.
- **Controles Semiredondeados Estilizados**:
  - Los botones de acción, cajas de texto, switches y deslizadores ahora adoptan esquinas semiredondeadas (`CornerRadius="4"` a `6"`) acordes a la identidad visual de la barra de acciones de DockBar.
- **Paleta Secundaria / Color de Énfasis (Accent Color)**:
  - Nueva propiedad `AccentR`, `AccentG`, `AccentB` en `DockConfig.cs` con persistencia en `shortcuts.json`.
  - Permite seleccionar el color de contraste de los botones principales (como *Guardar*), pistas activas de sliders, switches y elementos seleccionados.
- **Selector de Color Dual (Fondo y Énfasis) con Lienzo HSV y HEX**:
  - Selector segmentado `[ 🎨 Fondo del dock ]` / `[ ⚡ Énfasis / Botones ]` en la ventana de Ajustes.
  - El lienzo interactivo HSV y el cuadro de entrada HEX se reutilizan para ajustar con total libertad cualquier color para ambos objetivos con previsualización en vivo.
- **Menús Contextuales Flotantes Glass**:
  - Menú contextual de "Agregar acceso" (`+`) y menú de clic derecho actualizados con estilos dinámicos translúcidos y esquinas semiredondeadas.
- **Detección de Fuente de Instalación en Actualizaciones**:
  - El diálogo de actualización y el menú contextual reconocen automáticamente si la aplicación fue instalada mediante Microsoft Store (MSIX) o GitHub, dirigiendo al usuario a la tienda o al repositorio según corresponda.

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
