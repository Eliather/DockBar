# 🏗️ DockBar — Arquitectura y Estructura del Proyecto

Este documento detalla la arquitectura técnica, la organización de carpetas y las convenciones del proyecto **DockBar**. Está diseñado para guiar a desarrolladores y agentes de Inteligencia Artificial al navegar, mantener o extender el código.

---

## 📌 1. Visión General del Proyecto

* **Tipo de aplicación:** Barra de accesos directos lateral (Dock) moderna, de alto rendimiento y bajo consumo de recursos para Windows.
* **Plataforma:** .NET 10.0 (WPF) sobre Windows 10/11 (`net10.0-windows10.0.19041.0`).
* **UI & Estilos:** WPF nativo con efectos Glass/Acrílico (Win32 P/Invoke), soporte multi-idioma (es, en, ru, zh) y temas oscuro/claro dinámicos.
* **Formatos de distribución:**
  1. Instalador clásico ejecutable NSIS (`DockBarSetup.exe`).
  2. Paquete moderno MSIX (`DockBar.msix`) firmado para Microsoft Store.

---

## 📂 2. Mapa del Repositorio (Estructura de Directorios)

```text
DockBar/
├── .agents/                 # Reglas y configuraciones específicas de agentes IA
├── .tools/                  # Binarios de Windows SDK (makeappx, signtool) para empaquetado MSIX
├── Assets/                  # Recursos gráficos oficiales del ejecutable
│   ├── Dock.ico             # Ícono multi-resolución de la aplicación
│   └── Dock.png             # Logotipo en alta resolución
├── Controls/                # Controles de usuario personalizados (UserControls)
│   ├── DockPreviewControl.xaml    # Vista previa interactiva en tiempo real del Dock
│   └── DockPreviewControl.xaml.cs
├── Converters/              # Conversores IValueConverter de WPF para enlace de datos (Data Binding)
│   ├── BoolToDoubleConverter.cs
│   └── BooleanToVisibilityConverter.cs
├── dist/                    # 📦 Salida de distribución (IGNORADO POR GIT)
│   ├── DockBarSetup.exe     # Instalador NSIS compilado
│   ├── DockBar.msix         # Paquete MSIX firmado
│   └── DockBar-win-x64-*.zip# Paquetes portables de versión
├── docs/                    # 📄 Documentación de publicación y releases
│   ├── release_notes.md     # Notas de la versión actual
│   └── store_listing.md     # Ficha de Microsoft Store en español e inglés
├── Helpers/                 # 🛠️ Clases auxiliares, extensiones y ventanas transparentes
│   ├── EdgeHotspotWindow.cs      # Ventana invisible de detección de cursor en el borde
│   ├── GlassEffectHelper.cs      # Efectos DWM / Acrylic / Mica mediante P/Invoke Win32
│   ├── LocExtension.cs           # Extensión de marcado XAML para localización dinámica
│   └── WindowSwitcherHelper.cs   # Detección y filtrado de ventanas activas (Alt+Tab hook)
├── Models/                  # Modelos de datos y entidades serializables
│   ├── AudioEndpointItem.cs      # Dispositivos de reproducción de audio
│   ├── ColorTheme.cs             # Esquema de color y temas personalizados
│   ├── DockConfig.cs             # Configuración raíz de la aplicación (shortcuts.json)
│   ├── LanguageItem.cs           # Soporte de idiomas en la UI
│   ├── PerformanceHistoryItem.cs # Métricas para gráficos de rendimiento
│   ├── PinnedAppItem.cs          # Apps fijadas y estado de ejecución
│   └── ShortcutItem.cs           # Elemento individual del dock (archivo, URL, app Store)
├── Package/                 # Metadatos y assets exclusivos del empaquetado MSIX
│   ├── AppxManifest.xml          # Manifiesto de identidad y capacidades MSIX
│   ├── generate-assets.ps1       # Generador de íconos escalados (Square44, Square150, etc.)
│   └── Assets/                   # Logos requeridos por Windows App SDK
├── Resources/               # Diccionarios de recursos compartidos WPF
│   └── Theme.xaml                # Estilos globales, paletas, botones y sliders
├── scripts/                 # ⚡ Automatizaciones y scripts de compilación (PowerShell)
│   ├── build-installer.ps1       # Compila y genera el instalador NSIS en dist\
│   ├── build-msix.ps1            # Publica y empaqueta el paquete MSIX en dist\
│   ├── create-release.ps1        # Crea el release en GitHub y sube los assets
│   ├── publish-store.ps1         # Publicación directa en Microsoft Partner Center
│   ├── install-cert.ps1          # Instala certificados locales de desarrollo
│   └── .store-credentials.example.json # Plantilla de credenciales de la Store API
├── Services/                # Capa de lógica de negocio y servicios desacoplados
│   ├── AudioService.cs           # Control de volumen y cambio de endpoints (CoreAudio)
│   ├── AutoStartService.cs       # Gestión del inicio automático en Windows (Registry / MSIX)
│   ├── ConfigService.cs          # Lectura y persistencia segura de %APPDATA%\DockBar\shortcuts.json
│   ├── GpuService.cs             # Lectura de métricas de GPU (DirectX / NVAPI)
│   ├── IconService.cs            # Extracción de íconos de ejecutables y shell de Windows
│   ├── LocalizationService.cs    # Diccionarios de traducción y cambio de idioma en caliente
│   ├── MediaService.cs           # Integración con Windows Media Transport (reproducción y carátulas)
│   ├── PackageHelper.cs          # Detección de ejecución nativa vs contenedor MSIX
│   ├── ShellItemService.cs       # Resolución de accesos directos (.lnk), carpetas y shell
│   ├── SteamService.cs           # Integración con biblioteca y juegos de Steam
│   ├── StoreAppService.cs        # Enumeración rápida de aplicaciones UWP / MSIX instaladas
│   ├── ThemeService.cs           # Aplicación dinámica de estilos y paleta de acento
│   └── UpdateService.cs          # Comprobación de actualizaciones en GitHub Releases
├── storedisplay/            # Capturas de pantalla oficiales para la tienda
├── Views/                   # 🪟 Ventanas y diálogos de interfaz de usuario (WPF XAML)
│   ├── AboutWindow.xaml / .cs              # Acerca de y créditos
│   ├── AddLinkWindow.xaml / .cs            # Diálogo para añadir URL o comando
│   ├── MainWindow.xaml / .cs               # Barra Dock principal (núcleo visual)
│   ├── RenameWindow.xaml / .cs             # Renombrado de accesos directos
│   ├── SettingsWindow.xaml / .cs           # Panel completo de ajustes
│   ├── StoreAppPickerWindow.xaml / .cs     # Selector nativo de apps de la Store
│   ├── ThemedMessageDialogWindow.xaml / .cs # Modales y alertas con tema del dock
│   ├── TrayMenuWindow.xaml / .cs           # Menú contextual de la bandeja del sistema
│   └── UpdateWindow.xaml / .cs             # Notificador y descargador de actualizaciones
│
├── App.xaml / App.xaml.cs   # Punto de entrada de la aplicación y gestión de instancia única
├── DockBar.csproj           # Archivo de proyecto .NET
├── DockBar.sln              # Solución de Visual Studio
├── DockBar.nsi              # Script de instalación para Nullsoft Scriptable Install System
├── README.md                # Documentación pública del repositorio
└── LICENSE                  # Licencia del proyecto (GPL-3.0)
```

---

## 🧩 3. Principios y Flujos de Arquitectura

### 3.1. Punto de Entrada e Instancia Única (`App.xaml.cs`)
* La aplicación se ejecuta con `ShutdownMode="OnExplicitShutdown"`.
* **Aislamiento de Entornos:**
  * En modo `DEBUG`, el `Mutex` y el `EventWaitHandle` usan el sufijo `_Debug`.
  * En modo `RELEASE`, usan el identificador oficial `DockBar_SingleInstance_...`.
  * Esto permite depurar el código local sin anular ni recibir señales cruzadas de una versión instalada en producción.
* La barra no se cierra con `Alt+F4` ni al perder el foco; se oculta en el System Tray (`NotifyIcon`) y se reactiva al acercar el cursor al borde o hacer clic en la bandeja.

### 3.2. Persistencia y Configuración (`Services/ConfigService.cs`)
* Ubicación única del archivo de configuración:
  `%APPDATA%\DockBar\shortcuts.json`
* **Deserialización defensiva:** El servicio está preparado para rescatar la lista de accesos directos incluso si una versión añade campos experimentales o si el JSON sufre daños parciales.

### 3.3. Detección en Bordes y Z-Order (`Helpers/EdgeHotspotWindow.cs` & `Views/MainWindow.xaml.cs`)
* Cuando el Dock se oculta automáticamente, crea una ventana transparente en el borde (`EdgeHotspotWindow`) configurada con `SWP_NOACTIVATE` y `HWND_TOPMOST` para capturar el `MouseEnter` sin robar el foco de juegos o aplicaciones en pantalla completa.
* Al detectar el cursor, dispara una animación suave de deslizamiento con curva cúbica hacia el escritorio.

### 3.4. Reglas de Inicio con Windows (`Services/AutoStartService.cs`)
* **Packaged (MSIX):** Usa `Windows.ApplicationModel.StartupTask`.
* **Standalone (NSIS / Desarrollo):** Usa la clave de registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
* **Protección DEBUG:** En compilación `DEBUG`, la función `ApplyRegistry` está desactivada para evitar que Windows ejecute binarios de desarrollo (`bin\Debug\...`) al reiniciar el equipo.

---

## 🛠️ 4. Reglas para Agentes de IA

1. **Mantener la raíz limpia:**
   * **NUNCA** generes ejecutables (`.exe`), zips, archivos `.msix` o scripts sueltos en la raíz del repositorio.
   * Toda salida de empaquetado debe dirigirse a la carpeta `dist/`.
   * Todo script utilitario o de automatización debe ir a la carpeta `scripts/`.
2. **Ubicación de Nuevos Componentes:**
   * Nuevas ventanas o diálogos $\rightarrow$ `Views/`.
   * Nuevos servicios o APIs desacopladas $\rightarrow$ `Services/`.
   * Nuevos modelos o DTOs serializables $\rightarrow$ `Models/`.
   * Nuevas clases de ayuda o Win32 Interop $\rightarrow$ `Helpers/`.
   * Controles visuales reutilizables $\rightarrow$ `Controls/`.
3. **Espacios de nombres (Namespaces):**
   * Por convención y simplicidad de enlace XAML, las vistas en `Views/` y helpers en `Helpers/` utilizan el espacio de nombres principal `namespace DockBar;`.
4. **Pruebas de compilación:**
   * Siempre valida los cambios ejecutando `dotnet build`. Si la compilación no puede sobrescribir `bin\Debug\...\DockBar.exe` debido a un proceso en ejecución, compila a un directorio temporal (por ejemplo, `dotnet build -o bin\BuildVerify`) para confirmar la ausencia de errores sintácticos o de enlace.

