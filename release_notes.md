# DockBar 1.8.2 Release Notes

DockBar `v1.8.2` introduce un nuevo sistema de pestañas en el panel de Ajustes (Configuración básica y Experimental), un widget de reloj en tiempo real para aprovechar el espacio libre de la barra al estilo de Windows y un sistema de guardado experimental desacoplado y seguro contra corrupciones.

---

## ✨ Novedades y Mejoras Principales

### 1. 📑 Pestañas en el Menú de Ajustes
- **Navegación segmentada moderna**:
  - En la parte superior de la ventana de Ajustes ahora dispones de dos secciones organizadas con botones segmentados:
    - **Configuración básica**: Agrupa el tamaño de la barra, tamaño de iconos, auto-ocultamiento, inicio con Windows, opacidad y el sistema dual de colores Glass y Énfasis.
    - **Experimental**: Pestaña dedicada a características en desarrollo para expandir las capacidades del dock sin sobrecargar el menú principal.

### 2. 🕒 Reloj en Tiempo Real en la Barra (Dock)
- **Aprovechamiento del espacio libre**:
  - Diseñado especialmente para aprovechar el espacio vertical inferior que queda libre cuando los accesos directos no llenan la pantalla o no cabe un programa adicional.
  - Ubicado de forma elegante sobre la barra de paginación (`Página 1/2`) y los botones de acción (`+`, `=`, etc.), emulando la bandeja del sistema de Windows.
- **Personalización completa**:
  - **Formato 24 horas / 12 horas**: Elige entre notación estándar (ej. `14:25`) o AM/PM (ej. `02:25 PM`).
  - **Segundos en tiempo real**: Opción para mostrar el avance de los segundos (`:45`).
  - **Fecha bajo la hora**: Muestra el día de la semana y fecha abreviada (ej. `sáb., 5 sept.`).
  - **Tooltip con fecha completa**: Al colocar el cursor sobre el reloj, se despliega la fecha detallada del sistema.
- **Slider de tamaño de fuente (10 a 36 px)**:
  - Permite ajustar con un deslizador o caja numérica la escala exacta del texto del reloj.
  - La fecha escala proporcionalmente según el tamaño seleccionado.
- **Vista previa interactiva en vivo**:
  - En la pestaña *Experimental* puedes visualizar el reloj funcionando en tiempo real con los colores, opacidad y tipografía de tu dock antes de guardar.
- **Ajuste automático de paginación**:
  - La barra descuenta la altura del reloj de forma dinámica para que los accesos directos nunca se solapen ni se corten.

### 3. 🛡️ Sistema de Guardado Experimental Aislado y Seguro
- **Bloque de configuración desacoplado**:
  - Las opciones experimentales se serializan en su propio objeto `"Experimental": { ... }` en `shortcuts.json`, manteniendo intacto el esquema base.
- **Migración retrocompatible**:
  - Si existen versiones anteriores o valores en la raíz, el cargador los lee y migra sin fricción.
- **Mecanismo de rescate defensivo**:
  - Si un parámetro experimental tuviese un formato anómalo, el cargador extrae y preserva intactos todos los accesos directos (`Shortcuts`), impidiendo que el guardado se corrompa o que salte el diálogo de error de lectura.

---

## 📦 Archivos del Release / Downloads
- **Instalador clásico / Classic Installer**: `DockBarSetup.exe` (v1.8.2).
- **Paquete MSIX / MSIX Package**: `DockBar.msix` (v1.8.2.0).
- **Portable ZIP (x64)**: `DockBar-win-x64-v1.8.2.zip`.

---

## 💻 Requisitos
- Windows 10 (versión 19041+) o Windows 11 (64-bit).
- .NET Desktop Runtime 10.0 (x64).
