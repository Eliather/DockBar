# DockBar
A simple dockbar for Windows, minimal and practical.

<img width="256" height="256" alt="Dock" src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" />

DockBar es una barra lateral tipo dock para Windows (WPF) con accesos directos, auto-ocultamiento suave y configuración persistente en AppData. Diseñada para ser liviana, sin polling agresivo y con enfoque en rendimiento.

## Características

- Barra lateral anclada a izquierda o derecha con ventana sin bordes y TopMost.
- Auto-ocultamiento estilo Windows 8 (deslizamiento con un borde mínimo visible).
- Accesos directos con íconos: arrastrar y soltar .lnk, .exe o carpetas.
- Soporta accesos por URI/comando y apps de Microsoft Store.
- Modo edición: reordenar por arrastre, renombrar, cambiar ícono y eliminar.
- Paginación en modo normal si excede el alto visible.
- Selector de color tipo HTML (HEX + área HSV + swatches).
- Ajustes persistentes en %AppData%\DockBar\shortcuts.json.
- Ícono de bandeja con menú (Abrir, Ajustes, Salir).
- Oculta su ventana en Alt+Tab y Win+Tab.

## Requisitos

- Windows 10/11
- .NET SDK 9.0
- VS Code (opcional) o terminal con dotnet

## Compilar y ejecutar

En terminal dentro de la carpeta del proyecto:

```
cd DockBar
dotnet build
dotnet run
```

Si el build falla por archivo bloqueado, cierra la instancia de DockBar y vuelve a ejecutar.

## Uso rápido

- Arrastra archivos .lnk/.exe/carpetas a la barra para agregar accesos.
- Botón "...": abre Ajustes.
- Botón lápiz: activa modo edición (no se auto-oculta).
- En modo edición puedes arrastrar para reordenar.

## Funcionamiento general

- Inicio: carga la configuración desde AppData; si no existe o está corrupta, crea un JSON predeterminado y avisa.
- Ventana: sin bordes, anclada a un lado y TopMost; se oculta en Alt+Tab y Win+Tab.
- Bandeja: crea el ícono en el tray para abrir, cambiar lado, ajustes y salir.
- Monitor: usa el monitor más cercano para calcular alto completo y posición.

## Ajustes

La ventana de ajustes permite:

- Ancho de barra y tamaño de íconos.
- Retardo de ocultamiento y velocidad de animación.
- Transparencia y opacidad (afecta solo el efecto glass y el tinte del fondo).
- Color de fondo con selector HSV y HEX.
- Color de texto (claro u oscuro).

Nota: el color seleccionado se guarda al presionar Guardar.

## Selector de color (HEX + HSV)

- Barra vertical de tono (Hue).
- Área cuadrada de saturación/valor (S/V).
- Campo HEX (#RRGGBB) y swatches básicos.
- No usa librerías externas; es un control WPF implementado en el proyecto.

## Modo edición

- Desactiva el auto-ocultamiento y ensancha temporalmente la barra para editar.
- Permite reordenar por arrastre y muestra indicadores visuales.
- Botones por item: renombrar, cambiar ícono y eliminar.
- El orden se guarda al soltar.

## Paginación

- En modo normal, calcula items por página según alto del monitor e IconSize.
- Si hay más de una página, se muestran flechas de navegación.
- En modo edición la lista usa scroll vertical.

## Persistencia y ubicación del JSON

Archivo de configuración:

```
%AppData%\DockBar\shortcuts.json
```

Si el JSON no existe o está corrupto, la app muestra un mensaje y crea uno predeterminado.

### Ejemplo de shortcuts.json

```
{
  "DockSide": "Left",
  "DockWidth": 175,
  "IconSize": 40,
  "AutoHideDelaySeconds": 0,
  "HideAnimationMs": 200,
  "UseTransparency": false,
  "BackgroundOpacity": 0.85,
  "BackgroundR": 0,
  "BackgroundG": 0,
  "BackgroundB": 0,
  "UseLightText": true,
  "Shortcuts": [
    { "Name": "Explorador", "Path": "C:\\Windows\\explorer.exe" },
    { "Name": "Mis documentos", "Path": "C:\\Users\\Public\\Documents" },
    { "Name": "Steam", "Path": "C:\\Program Files (x86)\\Steam\\Steam.exe" }
  ]
}
```

## Apps de Microsoft Store

El selector de Store guarda rutas tipo:

```
shell:AppsFolder\<AppId>
```

Ejemplo:

```
shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App
```

## Efecto glass y opacidad

- El blur depende de DWM (si no está activo, se omite).
- El blur se aplica solo si "Usar transparencia" está activado y la opacidad < 1.0.
- La opacidad controla el tinte del fondo; íconos y texto no se transparentan.

## Accesos por comando o URI

Puedes agregar accesos por URI, por ejemplo:

```
com.epicgames.launcher://apps/fn%3A...&action=launch
```

## Auto-ocultamiento

- Se oculta con animación y deja 1-2px visibles para detectar hover.
- Usa eventos de mouse y DispatcherTimer (sin polling agresivo).
- En modo edición no se auto-oculta.

## Drag and drop y acciones

- Arrastrar archivos: .lnk, .exe o carpetas.
- Accesos Store: se guardan como shell:AppsFolder y se resuelven con íconos de shell.
- Accesos URI: se ejecutan vía UseShellExecute.

## Rendimiento

- Sin loops agresivos; usa timers y eventos.
- Íconos se cargan con size alto para evitar pixelado.
- El efecto glass depende de DWM (si no está disponible, se omite).

## Empaquetado

### Opción A: MSIX (recomendado)

1) Instala Visual Studio 2022 con "Windows Application Packaging Project".
2) Agrega un proyecto de empaquetado MSIX a la solución.
3) Configura el Appx Manifest (Nombre, Versión, Logo).
4) Establece DockBar como aplicación principal.
5) Compila en Release y genera el paquete.

### Opción B: NSIS (EXE instalador)

1) Publica la app:

```
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

2) Genera el instalador:

```
makensis DockBar.nsi
```

3) Se crea `DockBarSetup.exe`.

## Solución de problemas

- No compila: asegúrate de cerrar DockBar.exe.
- El blur no aparece: DWM debe estar habilitado.
- No aparece en Alt+Tab: es normal, la ventana se oculta a propósito.
- El JSON se regenera: revisa permisos en AppData o corrige el archivo corrupto.

## Estructura principal

- MainWindow.xaml(.cs): UI y lógica del dock.
- SettingsWindow.xaml(.cs): ajustes y selector de color.
- Services/ConfigService.cs: carga/guardado JSON.
- Services/IconService.cs: carga de íconos.

## Arquitectura y servicios

- Models: datos de configuración y accesos directos.
- Services: carga/guardado de config, resolución de íconos y apps Store.
- Windows: ventanas de UI y flujos (agregar, renombrar, ajustes).

## Notas de seguridad y privacidad

- La app no envía datos ni usa red para telemetría.
- Todo se guarda localmente en AppData.

---

Si quieres más documentación (XML docs o README extendido), se puede agregar.
