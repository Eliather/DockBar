# DockBar 1.7.2 Release Notes

---

## 🇪🇸 Español (Spanish)

### Novedades Principales (Corrección Crítica de Detección en Juegos y Optimizaciones)

- **Corrección Crítica en Detección de Juegos en Ventana sin Bordes (Borderless Fullscreen)**:
  - Implementación de un motor de análisis geométrico dual (límites DWM de canal alfa + coordenadas Win32 `GetWindowRect`) con tolerancia adaptativa de 10 píxeles.
  - Corrige el problema donde los videojuegos que se ejecutan en modo "Ventana Pantalla Completa" / "Ventana sin Bordes" marcaban el flag `WS_MAXIMIZE` o `IsZoomed` y provocaban que el dock permaneciera activo e interceptara clics o se abriera accidentalmente durante las partidas.
  - Al detectar un juego en pantalla completa (exclusiva o en ventana), el dock y su ventana sensible de borde (`EdgeHotspotWindow`) se desactivan y ocultan por completo.

- **Diferenciación Inteligente entre Ventanas Maximizadas y Juegos**:
  - Detección precisa basada en el área de trabajo del monitor (`rcWork`) vs área completa del monitor (`rcMonitor`).
  - Las aplicaciones de productividad estándar (Chrome, Edge, VS Code, Explorador) maximizadas en el escritorio mantienen el dock totalmente operativo.
  - En configuraciones con barra de tareas oculta automáticamente, se inspeccionan los estilos de ventana (`WS_POPUP`, `WS_CAPTION`, `WS_THICKFRAME`) para distinguir aplicaciones tradicionales de ventanas de juego.

- **Respuesta Instantánea en Conmutación de Foco**:
  - Ajuste del temporizador de estabilización a 150 ms para que la recuperación del dock al hacer Alt+Tab desde un juego al escritorio sea inmediata.

- **Soporte de Ventanas Minimizadas**:
  - Detección nativa con `IsIconic` para evitar estados de pantalla completa falsos cuando la ventana en primer plano se minimiza.

- **Control de Instancia Única y Superposición Continua**:
  - Protección de proceso único mediante `Mutex` del sistema y mantenimiento ininterrumpido de Z-Order (`HWND_TOPMOST`).

---

## 🇺🇸 English

### Main Highlights (Critical Game Detection Fix & Optimizations)

- **Critical Fix for Borderless Fullscreen Games**:
  - Implemented dual-layer geometric detection (DWM extended frame bounds + Win32 `GetWindowRect`) with 10px adaptive tolerance.
  - Resolves the issue where borderless windowed games setting `WS_MAXIMIZE` or `IsZoomed` caused DockBar to falsely remain active, pop up over games, or capture mouse clicks.
  - When a fullscreen or borderless game is active, DockBar and its edge hotspot window (`EdgeHotspotWindow`) completely collapse and sleep.

- **Intelligent Differentiation Between Maximized Productivity Apps and Games**:
  - Accurate geometry checks comparing monitor work area (`rcWork`) vs full monitor area (`rcMonitor`).
  - Standard desktop apps (Chrome, Edge, VS Code, File Explorer) remain compatible with DockBar while maximized.
  - For auto-hidden taskbar environments, window styles (`WS_POPUP`, `WS_CAPTION`, `WS_THICKFRAME`) accurately discern traditional desktop apps from borderless game surfaces.

- **Instant Responsiveness on Focus Switching**:
  - Debounce timer reduced to 150 ms for seamless, instant dock restoration when Alt-Tabbing between games and desktop.

- **Minimized Window State Awareness**:
  - Native `IsIconic` integration to prevent false detection when active windows are minimized.

- **Single Instance Enforcement & Continuous TopMost**:
  - System-wide mutex protection preventing duplicate processes and rock-solid continuous Z-order management (`HWND_TOPMOST`).

---

## Validación y Empaquetado / Build Artifacts
- **Compilación / Build Target**: Release `net10.0-windows10.0.19041.0` (Architecture: `win-x64`).
- **Instalador clásico / Classic Installer**: `DockBarSetup.exe` (v1.7.2).
- **Paquete MSIX / MSIX Package**: `DockBar.msix` (v1.7.2.0).
