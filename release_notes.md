# DockBar 1.7.1 Release Notes

---

## 🇪🇸 Español (Spanish)

### Novedades Principales (Corrección de Errores y Optimizaciones)

- **Control de Instancia Única (Single Instance)**:
  - Implementación a nivel de sistema mediante `Mutex` y `EventWaitHandle` en `App.xaml.cs`.
  - Evita la apertura de instancias duplicadas en segundo plano o múltiples íconos en la bandeja del sistema.
  - Al ejecutar una segunda instancia, la instancia primaria se revela y adquiere el foco automáticamente mientras la secundaria se cierra de forma instantánea y limpia.

- **Gestión Robusta de Primer Plano y Superposición (TopMost & Z-Order)**:
  - Reafirmación continua del estado superior en Z-Order ante conmutaciones de foco de otras aplicaciones.
  - Sincronización automática de Z-Order con la ventana sensible del borde (`EdgeHotspotWindow`) para asegurar activación constante.

- **Detección Precisa de Pantalla Completa en Windows 11**:
  - Integración de la API nativa Win32 `IsZoomed` para distinguir con precisión entre ventanas maximizadas modernas (Chrome, Edge, Windows Terminal, VS Code con pestañas DWM) y aplicaciones o juegos en pantalla completa real.
  - El dock permanece visible y funcional al interactuar con aplicaciones maximizadas y solo se oculta ante juegos o contenido a pantalla completa exclusiva/sin bordes.

- **Optimización de Eventos del Sistema**:
  - Filtrado selectivo en `SetWinEventHook` para descartar eventos internos y de controles secundarios (`idChild != 0`), reduciendo el consumo de CPU e interop durante el movimiento y arrastre de ventanas.

- **Efecto Glass y Transparencia Real (0% - 100%)**:
  - Motor de composición DWM nativa por hardware con canal alfa (`ACCENT_ENABLE_TRANSPARENTGRADIENT`) y marco extendido `WindowChrome`.
  - Transparencia pura sin capas turbias ni veladuras grises al 0% y 5% de opacidad.
  - Escalado de color continuo y suave de 0% (transparente) a 100% (sólido).

- **Sombra Dinámica en Letras**:
  - Algoritmo de contraste automático inteligente: genera una sombra negra detrás de las letras en modo texto claro (blanco) y una sombra blanca en modo texto oscuro (negro), garantizando legibilidad perfecta sobre cualquier fondo de pantalla.

- **Rediseño Espacioso de la Ventana de Ajustes**:
  - Interfaz organizada en dos columnas con panel de previsualización, selector HSV, controles de opacidad dedicados, entrada HEX y paleta rápida de 12 muestras de color.

---

## 🇺🇸 English

### Main Highlights (Bug Fixes & Optimizations)

- **Single Instance Enforcement**:
  - System-wide enforcement using a named `Mutex` and `EventWaitHandle` in `App.xaml.cs`.
  - Prevents duplicate processes or duplicate tray icons from running in the background.
  - Launching a second instance automatically signals, reveals, and focuses the existing dock while cleanly terminating the new process immediately.

- **Robust TopMost & Z-Order Management**:
  - Continuous Z-order assertion upon application focus switches, preventing DockBar from sinking underneath other windows.
  - Synchronized Z-order maintenance for the edge hotspot window (`EdgeHotspotWindow`) ensuring reliable reveal triggers.

- **Accurate Fullscreen vs Maximized Detection in Windows 11**:
  - Integrated native Win32 `IsZoomed` API to reliably distinguish modern tabbed/custom-caption maximized windows (Chrome, Edge, Windows Terminal, VS Code) from true exclusive/borderless fullscreen games and video playback.
  - Dock stays accessible when using maximized productivity apps and collapses only during full-screen immersion.

- **System Event Hook Optimization**:
  - Intelligent event filtering in `SetWinEventHook` callbacks discarding inner control notifications (`idChild != 0`), eliminating interop overhead during window resizing and movement.

- **True Glass & Hardware Per-Pixel Alpha Composition (0% - 100%)**:
  - Native DWM composition engine using `ACCENT_ENABLE_TRANSPARENTGRADIENT` and `WindowChrome` frame extension.
  - Crystal-clear transparency at 0% and 5% opacity without muddy or milky overlays.
  - Smooth, continuous opacity scaling from 0% (pure transparent) to 100% (solid background).

- **Dynamic Text Contrast Shadow**:
  - Intelligent automatic text contrast: applies a subtle black drop shadow behind white text and a white drop shadow behind dark text, ensuring 100% legibility on any light or dark wallpaper.

- **Spacious Settings Layout Redesign**:
  - Streamlined two-column configuration interface featuring live preview, HSV canvas, dedicated opacity sliders, HEX code input, and a fully visible 12-swatch quick palette.

---

## Validación y Empaquetado / Build Artifacts
- **Compilación / Build Target**: Release `net10.0-windows10.0.19041.0` (Architecture: `win-x64`).
- **Instalador clásico / Classic Installer**: `DockBarSetup.exe` (v1.7.1).
- **Paquete MSIX / MSIX Package**: `DockBar.msix` (v1.7.1.0).
