# DockBar - Windows C# WPF Sidebar

[![GitHub release](https://img.shields.io/github/v/release/Eliather/DockBar?style=flat-square&color=blue)](https://github.com/Eliather/DockBar/releases)
[![GitHub stars](https://img.shields.io/github/stars/Eliather/DockBar?style=flat-square&color=gold)](https://github.com/Eliather/DockBar/stargazers)
[![GitHub downloads](https://img.shields.io/github/downloads/Eliather/DockBar/total?style=flat-square&color=green)](https://github.com/Eliather/DockBar/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=flat-square)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/Framework-.NET%2010.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/Eliather/DockBar?style=flat-square&color=orange)](LICENSE)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Apoyar-FF5E5B?style=flat-square)](https://ko-fi.com/eliather)

DockBar es una barra lateral de accesos directos estilo dock de alto rendimiento para Windows, desarrollada en C# y WPF sobre .NET 10. Proporciona una interfaz compacta, moderna e hiperpersonalizable con composición DWM Glass por hardware, integración nativa con el sistema operativo y optimizaciones de nivel de producción.

---

## Capturas de Pantalla y Vista Previa

<p align="center">
  <img src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" alt="DockBar Logo" width="180" />
</p>

### Interfaz Principal

<img width="1917" height="1078" alt="C1" src="https://github.com/user-attachments/assets/8891ef33-ae05-4cbb-93cf-fb6e19d8b24d" />
<img width="1913" height="1078" alt="C2" src="https://github.com/user-attachments/assets/a793fe53-5c35-47d3-9ae6-b09d8eaecf8c" />


### Demostración en Vídeo

[Ver vídeo de demostración en GitHub](https://github.com/user-attachments/assets/9a4ea52f-8131-471e-8bd3-89122aa3dec7)

---

## Tabla de Contenidos

- [Descripción General](#descripción-general)
- [Inspiración y Agradecimientos](#inspiración-y-agradecimientos)
- [Prioridades de Diseño y Arquitectura](#prioridades-de-diseño-y-arquitectura)
- [Características Principales](#características-principales)
- [Arquitectura de Componentes](#arquitectura-de-componentes)
- [Requisitos del Sistema](#requisitos-del-sistema)
- [Compilación y Ejecución](#compilación-y-ejecución)
- [Configuración y Persistencia](#configuración-y-persistencia)
- [Preguntas Frecuentes (FAQ)](#preguntas-frecuentes-faq)
- [Licencia y Créditos](#licencia-y-créditos)

---

## Descripción General

DockBar ofrece un punto de acceso rápido y organizado para aplicaciones, ejecutables, accesos directos, carpetas y juegos. Diseñado para integrarse con la estética visual de Windows 10 y Windows 11, el dock incluye ocultamiento automático fluido, widgets interactivos de control de volumen y multimedia, monitorización de recursos de hardware en tiempo real y soporte multilingüe completo.

---

## Inspiración y Agradecimientos

DockBar es un proyecto personal inspirado en la clásica barra lateral de Windows 8, reimaginada y adaptada como una barra de accesos directos de alto rendimiento, moderna y personalizable para Windows 10 y Windows 11.

Un agradecimiento especial a todos los usuarios que utilizan DockBar en su día a día, comparten sus comentarios y apoyan el desarrollo continuo del proyecto. Su interés y retroalimentación constante han sido fundamentales para mantener esta herramienta activa y en constante evolución.

---

## Prioridades de Diseño y Arquitectura

El desarrollo de DockBar se rige por cinco pilares fundamentales:

1. **Efecto Glass y Estética Unificada**: Implementación de composición por hardware DWM (`WindowChrome GlassFrameThickness="-1"`) extendida a la ventana principal y a todas las ventanas secundarias (Ajustes, Agregar Enlace, Apps Instaladas, Actualizaciones, Renombrar, etc.). La interfaz respeta la opacidad, el color de fondo y el efecto Glass seleccionados por el usuario.
2. **Organización Modular de Ajustes por Categorías**: Menú de configuración estructurado en pestañas independientes:
   - **Básica**: Comportamiento de anclaje, visibilidad, opacidad y colores.
   - **Utilidades**: Reloj digital y Monitor de Recursos del sistema.
   - **Multimedia**: Previsualización y control de reproducción.
   - **Experimentales**: Gestión de energía del sistema, modo Cafeína, slider de páginas y organizador de widgets.
3. **Paleta de Énfasis Secundario**: Personalización del color de acento para elementos interactivos como botones de acción (`Guardar`), deslizadores (`sliders`), interruptores (`switches`), casillas de selección y estados resaltados.
4. **Selector de Color Dual (HSV y HEX)**: Selector de color con lienzo interactivo de saturación/brillo, barra de tono y entrada de código hexadecimal, aplicable tanto al fondo del dock como al color de énfasis.
5. **Instancia Única y Rendimiento Nativo**: Enumeración de aplicaciones y juegos mediante APIs nativas Win32 Shell COM y resolución paralela multihilo con precalentamiento en segundo plano y caché concurrente sin bloqueos en el hilo principal de la interfaz (UI).

---

## Características Principales

### Interfaz y Experiencia de Usuario (UI/UX)
- **Anclaje Flexible**: Soporte para alineación en el borde izquierdo o derecho de la pantalla con comportamiento siempre visible (`TopMost`).
- **Ocultamiento Automático e Interactivo**: Transición suave de ocultado con zona de activación por borde (`Hotspot / Trigger`) calibrable entre 1 y 12 píxeles.
- **Opción Mostrar Siempre (Always Show)**: Posibilidad de fijar la barra de forma permanente desactivando el auto-ocultado.
- **Modo Edición y Reordenamiento**: Modificación visual para reordenar elementos mediante arrastrar y soltar (*drag & drop*), renombrar, cambiar iconos o eliminar accesos directos.
- **Paginación Automática y Reordenable**: Sistema de navegación por páginas (flechas/slider) que se activa cuando los elementos superan la altura disponible, integrado en el panel de reordenamiento de widgets.
- **Tooltips Estilizados en Dark Mode**: Tooltips modernos con fondo acrílico oscuro (`#F2161622`), esquinas redondeadas (`CornerRadius="8"`), borde semitransparente y sombra difusa (`BlurRadius="16"`).
- **Vista Previa en Vivo**: Simulación dinámica en tiempo real de los cambios visuales dentro de la ventana de Ajustes antes de guardar.
- **Exclusión de Selectores de Tareas**: Oculto de la lista de ventanas de `Alt+Tab` y `Win+Tab`.
- **Soporte Multilingüe Integrado**: Traducido completamente a Español, Inglés, Ruso y Chino Simplificado.

### Rendimiento y Optimización
- **Rendimiento a 60 FPS (Zero GC Allocations)**: Control `MarqueeTextBlock` optimizado mediante caché de `FormattedText` y congelación de pinceles/máscaras (`ShadowBrush`, `EdgeFadingMask`), eliminando asignaciones de memoria durante la animación.
- **Suspensión Inteligente de Recursos**: Pausa automática de lecturas de CPU, RAM, GPU y reloj cuando el dock se oculta o cuando se detectan aplicaciones a pantalla completa (juegos o vídeos).
- **Filtrado Eficiente de Eventos de Ventana**: Uso de `WinEventHook` descartando eventos secundarios fuera de foco, reduciendo la carga del chequeo de pantalla completa en más del 95%.
- **Gestión Determinista de Memoria COM**: Liberación explícita de punteros no administrados (`IImageList`) mediante `Marshal.ReleaseComObject`.
- **Carga Instantánea de Aplicaciones (`StoreAppService`)**: Caché en memoria persistente (`ConcurrentDictionary`), resolución paralela multihilo (`Parallel.ForEach`) y precalentamiento silencioso 1 segundo tras el inicio.

### Control Multimedia y Audio
- **Controlador Multimedia Inteligente (GSMTC)**: Conexión con *Windows System Media Transport Controls* compatible con Spotify, navegadores (Chrome, Edge, Firefox) y reproductores locales.
- **Previsualización Inteligente de Carátulas (Smart Artwork)**:
  - Formato adaptativo 1:1 para pistas de música.
  - Formato adaptativo 16:9 panorámico para contenido de vídeo.
  - Modo solo miniatura hiperminimalista y control de reproducción mediante clic directo.
- **Barra de Búsqueda Multimedia (Seek Bar)**: Deslizador de posición de reproducción con tiempo transcurrido/total, ajuste por rueda de ratón (±5s) y protección contra desincronización en streaming.
- **Marquesina Animada (`MarqueeTextBlock`)**: Desplazamiento de texto continuo para títulos y artistas extensos.
- **Controlador de Volumen Dinámico**: Control de volumen con integración `IMMNotificationClient` para detección y reconexión automática en caliente al cambiar de dispositivo de audio (auriculares, altavoces).

### Monitorización y Utilidades
- **Monitor de Recursos y Multi-GPU**: Identificación de hardware real y seguimiento de carga de CPU, RAM y GPU (soporte para NVIDIA NVML, AMD ADLX/DXGI e Intel).
- **Modo Cafeína (Keep-Awake)**: Prevención de suspensión del sistema o apagado de pantalla durante descargas o tareas prolongadas.
- **Gestión de Energía**: Accesos directos integrados para Bloquear, Hibernar, Reiniciar y Apagar el sistema.

---

## Arquitectura de Componentes

La solución se compone de los siguientes archivos y servicios principales:

| Componente / Archivo | Función y Responsabilidad |
| :--- | :--- |
| `MainWindow.xaml.cs` | Ventana principal de la barra lateral, gestión de eventos de ratón/tacto, animaciones y detección de pantalla completa. |
| `SettingsWindow.xaml.cs` | Panel de configuración con pestañas modulares, selectores HSV/HEX y personalización de temas. |
| `ThemeService.cs` | Motor centralizado de temas y composición de efectos DWM Glass por hardware. |
| `StoreAppPickerWindow.xaml.cs` | Interfaz de selección para aplicaciones instaladas de Microsoft Store y del sistema. |
| `StoreAppService.cs` | Motor de enumeración COM nativo, resolución paralela multihilo (`Parallel.ForEach`) y caché concurrente. |
| `ShellItemService.cs` | Interacción con Shell Known Folders, integración con `IShellItemImageFactory` y recorte selectivo de bordes (*AutoCrop*). |
| `IconService.cs` | Extracción de iconos de alta resolución (Jumbo/ExtraLarge 256px) y gestión de caché en memoria. |
| `SteamService.cs` | Lectura de manifiestos ACF de Steam, detección de bibliotecas locales y extracción de iconos de juegos. |
| `AddLinkWindow.xaml.cs` | Diálogo para agregar ejecutables, carpetas, enlaces web o comandos de sistema. |
| `AudioService.cs` | Interacción con Windows CoreAudio API y conmutación de dispositivos mediante `IMMNotificationClient`. |
| `MediaService.cs` | Integración con `SystemMediaTransportControls` para el control e información de reproductores multimedia. |
| `MarqueeTextBlock.cs` | Control personalizado de texto con desplazamiento continuo optimizado. |
| `UpdateWindow.xaml.cs` | Interfaz de comprobación e instalación de actualizaciones de la aplicación. |

---

## Requisitos del Sistema

- **Sistema Operativo**: Windows 10 o Windows 11 (64-bit).
- **Entorno de Ejecución**: .NET 10 Runtime (o SDK .NET 10.0 para compilación).
- **Herramientas de Desarrollo**: Visual Studio 2022 / 2026, VS Code o .NET CLI.

---

## Compilación y Ejecución

### Entorno de Desarrollo (Debug)
Para compilar y ejecutar la aplicación localmente:
```bash
dotnet build
dotnet run
```

### Publicación de Producción (Release)
Para generar el ejecutable optimizado y autocontenido para distribución:
```bash
dotnet publish DockBar.csproj -c Release -r win-x64 --self-contained false -o publish
```

---

## Configuración y Persistencia

La configuración del usuario y la lista de accesos directos se almacenan en formato JSON en la siguiente ruta:

```text
%AppData%\DockBar\shortcuts.json
```

### Ejemplo de Estructura JSON

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
    {
      "Name": "Explorador",
      "Path": "C:\Windows\explorer.exe"
    },
    {
      "Name": "Documentos",
      "Path": "C:\Users\Public\Documents"
    },
    {
      "Name": "Steam",
      "Path": "C:\Program Files (x86)\Steam\Steam.exe"
    }
  ]
}
```

---

## Preguntas Frecuentes (FAQ)

### ¿Qué versiones de Windows son compatibles con DockBar?
DockBar está diseñado y optimizado específicamente para sistemas operativos Windows 10 y Windows 11 de 64 bits (x64).

### ¿Es necesario instalar .NET 10 para ejecutar la aplicación?
Si compilas el proyecto desde el código fuente o ejecutas la versión portable dependiente del ejecutable predeterminado, requerirás el entorno de ejecución .NET 10 Runtime. Si utilizas una compilación autocontenida (*self-contained*), no se requiere ninguna instalación previa.

### ¿Cómo impacta DockBar en el consumo de CPU, GPU y batería?
El consumo de recursos es cercano a 0% en reposo. La aplicación incluye suspensión inteligente: al ocultarse la barra o detectarse una aplicación o juego a pantalla completa, las lecturas de hardware y temporizadores se pausan automáticamente para preservar el rendimiento del sistema y la batería.

### ¿Dónde se guardan los accesos directos y personalizaciones?
Toda la configuración del usuario se conserva en `%AppData%\DockBar\shortcuts.json`. Puedes copiar o respaldar este archivo en cualquier momento para mantener tus accesos directos.

### ¿Cómo detecta DockBar las aplicaciones de la Microsoft Store y juegos de Steam?
Utiliza la API Win32 Shell COM para enumerar las aplicaciones del sistema y lee directamente los manifiestos `.acf` de las bibliotecas locales de Steam, extrayendo de forma nativa e instantánea sus iconos en alta resolución.

### ¿Cómo puedo reportar errores o sugerir nuevas funciones?
Puedes abrir una incidencia (*Issue*) en el repositorio oficial de GitHub del proyecto o contactar al desarrollador mediante la página oficial.

---

## Licencia y Créditos

- **Desarrollador Principal**: Eliather
- **Plataforma de Apoyo**: [Ko-fi](https://ko-fi.com/eliather)
- **Licencia**: Licencia MIT
