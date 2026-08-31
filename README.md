# DockBar
DockBar es una barra lateral de accesos directos estilo dock para Windows desarrollada en C# y WPF. La versión `1.7.2` perfecciona la detección inteligente de juegos en modo ventana completa / sin bordes (*Borderless Fullscreen*), optimiza los tiempos de respuesta ante transiciones de pantalla completa, mantiene el efecto Glass real por hardware y la gestión continua de superposición y Z-Order.

<img width="256" height="256" alt="Dock" src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" />

<img width="1920" height="1080" alt="{0A9D8F93-0DB9-42CC-979A-CF66218595FC}" src="https://github.com/user-attachments/assets/302484ca-4aa6-4e54-9b0d-35e484cdc4ff" />

Video de demostración:
https://github.com/user-attachments/assets/9a4ea52f-8131-471e-8bd3-89122aa3dec7

---

## Descripción general
DockBar proporciona una barra lateral compacta y moderna para Windows con soporte para accesos directos, modo de edición, ocultación automática fluida, configuración persistente y un panel de ajustes totalmente rediseñado.

El proyecto está diseñado bajo cinco prioridades esenciales:

- **Instancia única garantizada**: Protección del sistema mediante `Mutex` y `EventWaitHandle` para evitar procesos duplicados; abrir un segundo ejecutable revela y enfoca instantáneamente la instancia activa.
- **Interacción ultrarrápida**: Enumeración instantánea de aplicaciones y juegos mediante APIs nativas Win32 Shell COM (< 5 ms de tiempo de respuesta) sin subprocesos de PowerShell.
- **Detección robusta de juegos y modo ventana completa**: Análisis geométrico DWM y Win32 de doble capa con tolerancia adaptativa que reconoce juegos en pantalla completa exclusiva o ventana sin bordes (*Borderless Windowed*), evitando aperturas accidentales del dock durante las partidas.
- **Efecto Glass y transparencia real**: Composición por hardware DWM con soporte de transparencia cristalina pura (0% - 100%) y sombra dinámica adaptativa para texto claro y oscuro.
- **Cero dependencias de terceros**: Código fuente en C# 13 y .NET 10.0 que utiliza exclusivamente APIs estándar de Windows sin librerías externas pesadas.

---

## Novedades en la versión 1.7.2
- **Detección Geométrica Robusta para Juegos en Ventana Completa (`MainWindow.xaml.cs`)**: Corrección crítica que permite a DockBar detectar y ocultarse correctamente ante videojuegos que corren en modo "Ventana Pantalla Completa" / "Sin Bordes" (*Borderless Windowed*), evitando que el dock se despliegue o intercepte clics durante el juego.
- **Diferenciación Dinámica de Aplicaciones Maximizadas vs Juegos**: Las ventanas de productividad maximizadas (Chrome, VS Code, Explorer) siguen conviviendo con el dock normalmente, mientras que los juegos a pantalla completa suspenden el dock y su borde sensible por completo.
- **Respuesta Ultrarrápida en Conmutación de Foco**: Reducción del temporizador de estabilización a 150 ms para una recuperación y ocultación instantáneas al alternar entre juegos y escritorio (Alt+Tab).
- **Protección de Ventanas Minimizadas**: Detección nativa con `IsIconic` para evitar transiciones de estado erróneas cuando las aplicaciones en primer plano se minimizan.
- **Mantenimiento Continuo de TopMost e Instancia Única**: Máxima estabilidad de superposición y prevención estricta de procesos duplicados.

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

## Mejoras de rendimiento
- **Enumeración Shell COM nativa**: Búsqueda instantánea de aplicaciones de Microsoft Store en menos de 5 ms.
- **Deserialización JSON por Stream**: Carga y guardado directo por flujo de datos para máxima velocidad y menor recolección de basura.
- **Caché inteligente de íconos**: `IconService` y `ShellItemService` evitan extracciones redundantes del disco.
- **Guardado atómico por lotes**: Persistencia segura de la configuración al modificar múltiples accesos directos.

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

Si la compilación reporta que el archivo `.exe` o `.dll` está bloqueado, cierra la instancia de DockBar en ejecución y vuelve a compilar.

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
  "BackgroundOpacity": 0.72,
  "BackgroundR": 17,
  "BackgroundG": 24,
  "BackgroundB": 39,
  "UseLightText": true,
  "AutoStartEnabled": false,
  "Shortcuts": [
    { "Name": "Explorador", "Path": "C:\\Windows\\explorer.exe" },
    { "Name": "Documentos", "Path": "C:\\Users\\Public\\Documents" },
    { "Name": "Steam", "Path": "C:\\Program Files (x86)\\Steam\\Steam.exe" }
  ]
}
```

---

## Arquitectura del código
- [App.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/App.xaml.cs): Ciclo de vida, control de instancia única (`Mutex` y `EventWaitHandle`) e icono de la bandeja del sistema.
- [MainWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/MainWindow.xaml.cs): Interfaz principal, auto-ocultación, drag & drop, paginación, hooks de eventos del sistema (`SetWinEventHook`) y detección de pantalla completa.
- [EdgeHotspotWindow.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/EdgeHotspotWindow.cs): Ventana de borde ultraligera con gestión de Z-order para detectar el puntero del mouse al estar oculto.
- [GlassEffectHelper.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/GlassEffectHelper.cs): Composición nativa por hardware DWM para el efecto Glass acrílico y transparente.
- [SettingsWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/SettingsWindow.xaml.cs): Interfaz de ajustes, previsualización en vivo, selector HSV y configuración de apariencia.
- [AddLinkWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/AddLinkWindow.xaml.cs): Diálogo para añadir ejecutables, carpetas, URLs web o comandos de sistema.
- [StoreAppPickerWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/StoreAppPickerWindow.xaml.cs): Selector de aplicaciones de Microsoft Store con enumeración COM nativa.
- [UpdateWindow.xaml.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/UpdateWindow.xaml.cs): Ventana de actualización con notas de versión y barra de progreso.
- [WindowSwitcherHelper.cs](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/WindowSwitcherHelper.cs): Oculta la aplicación de los conmutadores de ventanas de Windows (Alt+Tab).
- [Services/](file:///c:/Users/danie/Documents/Trabajos/Cosas/DockBar/Services): Servicios modulares de configuración, íconos, shell, Steam y actualizaciones.

---

## Empaquetado y Distribución

### Opción A: Paquete MSIX (Microsoft Store y Sideloading)
DockBar incluye un script automatizado sin dependencias externas para generar el paquete MSIX:
```powershell
.\build-msix.ps1
```
* Genera `DockBar.msix` firmado con certificado de desarrollador listo para pruebas o publicación en Microsoft Partner Center.

### Opción B: Instalador clásico NSIS (Win32)
```powershell
.\build-installer.ps1
```
* Compila la versión Release para `win-x64` y genera el instalador `DockBarSetup.exe`.

---

## Privacidad
- Sin telemetría ni rastreo de actividad.
- Sin sincronización en la nube ni envío de datos personales.
- Funcionamiento 100% local y autónomo.
