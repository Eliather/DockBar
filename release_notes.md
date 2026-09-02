# DockBar 1.8.1 Release Notes

DockBar `v1.8.1` consolida las novedades visuales de la versión 1.8.0 agregando correcciones críticas de estabilidad, persistencia y clonación de configuración en la paleta de énfasis secundaria.

---

## ✨ Novedades y Mejoras Principales

### 1. 🛠️ Corrección de Clonación y Persistencia de Color de Énfasis
- **Consistencia en el ciclo de vida de configuración (`CloneConfig`)**:
  - Se corrigió la omisión de las propiedades `AccentR`, `AccentG`, `AccentB` al clonar la configuración en `MainWindow.xaml.cs`.
  - Ahora las previsualizaciones, cancelaciones y confirmaciones en el panel de Ajustes retienen y restauran con total fidelidad los valores de color de acento elegidos por el usuario.

### 2. 🪟 Estética Unificada DWM Glass en Toda la Aplicación
- **Ventanas sin bordes opacos (`WindowChrome`)**:
  - Todas las ventanas secundarias (`SettingsWindow`, `AddLinkWindow`, `StoreAppPickerWindow`, `UpdateWindow`, `AboutWindow`, `RenameWindow`, `ThemedMessageDialogWindow` y `TrayMenuWindow`) cuentan con `WindowChrome` y composición Glass DWM nativa.
  - Se eliminaron las barras de título estándar del sistema operativo y los fondos sólidos oscuros/morados, logrando un acabado translúcido que respeta en tiempo real el color de fondo y la opacidad configurados.

### 3. ⚡ Paleta de Énfasis / Acento Secundaria (Accent Color)
- **Personalización de botones y deslizadores**:
  - Propiedad `AccentR`, `AccentG`, `AccentB` en la configuración persistente (`shortcuts.json`).
  - Permite modificar el color de los botones principales de acción (como *Guardar*), las pistas activas de los deslizadores (sliders), switches, cajas de selección y resaltados interactivos.
  - Paleta rápida de 12 tonos curados en la ventana de Ajustes (Azul, Índigo, Violeta, Púrpura, Magenta, Rosa, Coral, Naranja, Ámbar, Esmeralda, Cian y Pizarra).

### 4. 🎨 Selector de Color Dual (Fondo vs Énfasis) con Lienzo HSV y HEX
- **Libertad total de color**:
  - Selector segmentado `[ 🎨 Fondo del dock ]` / `[ ⚡ Énfasis / Botones ]` que permite reutilizar el lienzo interactivo de saturación/brillo (SatVal), el deslizador de tono arcoíris y el cuadro de entrada HEX para ambos objetivos.
  - Previsualización instantánea en vivo de los cambios antes de guardar.

### 5. 🔲 Controles y Menús Semiredondeados
- **Identidad visual consistente**:
  - Botones, campos de texto y elementos de selección estandarizados con esquinas semiredondeadas (`CornerRadius="4"` a `6"`), a juego con la botonera de acción de DockBar.
  - Menús contextuales flotantes ("Agregar acceso" `+`, clic derecho) con acabado Glass translúcido y efectos suaves al pasar el cursor.

### 6. 🔄 Detección Inteligente de Actualizaciones
- **Diferenciación de versión MSIX (Store) vs GitHub**:
  - El sistema de actualizaciones detecta si la aplicación fue descargada desde Microsoft Store o como ejecutable independiente de GitHub, guiando al usuario al canal de actualización correspondiente.

---

## 📦 Archivos del Release / Downloads
- **Instalador clásico / Classic Installer**: `DockBarSetup.exe` (v1.8.1).
- **Paquete MSIX / MSIX Package**: `DockBar.msix` (v1.8.1.0).
- **Portable ZIP (x64)**: `DockBar-win-x64-v1.8.1.zip`.

---

## 💻 Requisitos
- Windows 10 (versión 19041+) o Windows 11 (64-bit).
- .NET Desktop Runtime 10.0 (x64).
