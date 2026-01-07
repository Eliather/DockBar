# DockBar
A Simple Dockbar for Windows, Minimal and Practical

# DockBar
<<<<<<< HEAD
A Simple Dockbar for Windows, Minimal and Practical
=======

DockBar es una barra lateral tipo dock para Windows (WPF) con accesos directos, auto-ocultamiento suave y configuracion persistente en AppData. Disenada para ser liviana, sin polling agresivo y con enfoque en rendimiento.

## Caracteristicas

- Barra lateral anclada a izquierda o derecha con ventana sin bordes y TopMost.
- Auto-ocultamiento estilo Windows 8 (deslizamiento con un borde minimo visible).
- Accesos directos con iconos: arrastrar y soltar .lnk, .exe o carpetas.
- Soporta accesos por URI/comando y apps de Microsoft Store.
- Modo edicion: reordenar por arrastre, renombrar, cambiar icono y eliminar.
- Paginacion en modo normal si excede el alto visible.
- Selector de color tipo HTML (HEX + area HSV + swatches).
- Ajustes persistentes en %AppData%\DockBar\shortcuts.json.
- Icono de bandeja con menu (Abrir, Ajustes, Salir).
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

## Uso rapido

- Arrastra archivos .lnk/.exe/carpetas a la barra para agregar accesos.
- Boton "...": abre Ajustes.
- Boton lapiz: activa modo edicion (no se auto-oculta).
- En modo edicion puedes arrastrar para reordenar.

## Funcionamiento general

- Inicio: carga la configuracion desde AppData; si no existe o esta corrupta, crea un JSON predeterminado y avisa.
- Ventana: sin bordes, anclada a un lado y TopMost; se oculta en Alt+Tab y Win+Tab.
- Bandeja: crea el icono en el tray para abrir, cambiar lado, ajustes y salir.
- Monitor: usa el monitor mas cercano para calcular alto completo y posicion.

## Ajustes

La ventana de ajustes permite:

- Ancho de barra y tamano de iconos.
- Retardo de ocultamiento y velocidad de animacion.
- Transparencia y opacidad (afecta solo el efecto glass y el tinte del fondo).
- Color de fondo con selector HSV y HEX.
- Color de texto (claro u oscuro).

Nota: el color seleccionado se guarda al presionar Guardar.

## Selector de color (HEX + HSV)

- Barra vertical de tono (Hue).
- Area cuadrada de saturacion/valor (S/V).
- Campo HEX (#RRGGBB) y swatches basicos.
- No usa librerias externas; es un control WPF implementado en el proyecto.

## Modo edicion

- Desactiva el auto-ocultamiento y ensancha temporalmente la barra para editar.
- Permite reordenar por arrastre y muestra indicadores visuales.
- Botones por item: renombrar, cambiar icono y eliminar.
- El orden se guarda al soltar.

## Paginacion

- En modo normal, calcula items por pagina segun alto del monitor e IconSize.
- Si hay mas de una pagina, se muestran flechas de navegacion.
- En modo edicion la lista usa scroll vertical.

## Persistencia y ubicacion del JSON

Archivo de configuracion:

```
%AppData%\DockBar\shortcuts.json
```

Si el JSON no existe o esta corrupto, la app muestra un mensaje y crea uno predeterminado.

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

- El blur depende de DWM (si no esta activo, se omite).
- El blur se aplica solo si "Usar transparencia" esta activado y la opacidad < 1.0.
- La opacidad controla el tinte del fondo; iconos y texto no se transparentan.

## Accesos por comando o URI

Puedes agregar accesos por URI, por ejemplo:

```
com.epicgames.launcher://apps/fn%3A...&action=launch
```

## Auto-ocultamiento

- Se oculta con animacion y deja 1-2px visibles para detectar hover.
- Usa eventos de mouse y DispatcherTimer (sin polling agresivo).
- En modo edicion no se auto-oculta.

## Drag and drop y acciones

- Arrastrar archivos: .lnk, .exe o carpetas.
- Accesos Store: se guardan como shell:AppsFolder y se resuelven con iconos de shell.
- Accesos URI: se ejecutan via UseShellExecute.

## Rendimiento

- Sin loops agresivos; usa timers y eventos.
- Iconos se cargan con size alto para evitar pixelado.
- El efecto glass depende de DWM (si no esta disponible, se omite).

## Empaquetado

### Opcion A: MSIX (recomendado)

1) Instala Visual Studio 2022 con "Windows Application Packaging Project".
2) Agrega un proyecto de empaquetado MSIX a la solucion.
3) Configura el Appx Manifest (Nombre, Version, Logo).
4) Establece DockBar como aplicacion principal.
5) Compila en Release y genera el paquete.

### Opcion B: Inno Setup (MSI/EXE)

1) Publica la app:

```
 dotnet publish -c Release -r win-x64 --self-contained false
```

2) Crea un script .iss basico apuntando a la carpeta de publish.
3) Genera instalador con Inno Setup.

## Solucion de problemas

- No compila: asegurate de cerrar DockBar.exe.
- El blur no aparece: DWM debe estar habilitado.
- No aparece en Alt+Tab: es normal, la ventana se oculta a proposito.
- El JSON se regenera: revisa permisos en AppData o corrige el archivo corrupto.

## Estructura principal

- MainWindow.xaml(.cs): UI y logica del dock.
- SettingsWindow.xaml(.cs): ajustes y selector de color.
- Services/ConfigService.cs: carga/guardado JSON.
- Services/IconService.cs: carga de iconos.

## Arquitectura y servicios

- Models: datos de configuracion y accesos directos.
- Services: carga/guardado de config, resolucion de iconos y apps Store.
- Windows: ventanas de UI y flujos (agregar, renombrar, ajustes).

## Notas de seguridad y privacidad

- La app no envia datos ni usa red para telemetria.
- Todo se guarda localmente en AppData.

---

Si quieres mas documentacion (XML docs o README extendido), se puede agregar.
>>>>>>> 74f093c (Initial release)
