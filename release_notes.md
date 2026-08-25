# DockBar 1.7.0 Release Notes

## Novedades Principales (Highlights)

- **Efecto Glass y Transparencia Real (0% - 100%)**:
  - Implementación de composición DWM nativa por canal alfa (`ACCENT_ENABLE_TRANSPARENTGRADIENT`) con marco extendido `WindowChrome`.
  - Transparencia pura y cristalina al 0% y 5% de opacidad sin capas turbias ni veladuras grises.
  - Escalado suave y continuo de color de 0% (100% transparente) a 100% (sólido).
  - Estructura lateral rectangular nítida con borde separador de 1px.

- **Sombra Dinámica en Letras**:
  - Contraste automático inteligente: genera una sombra negra detrás de las letras en modo texto claro (blanco) y una sombra blanca en modo texto oscuro (negro), garantizando legibilidad óptima sobre cualquier fondo de pantalla.
  - Opción configurable en `Ajustes > Color y efecto Glass`.

- **Rediseño Espacioso de la Ventana de Ajustes**:
  - Arquitectura limpia con panel izquierdo para previsualización, selector HEX y resumen, y panel derecho amplio para lienzo HSV, controles de opacidad/color y paleta rápida de 12 muestras completamente visible.

## Validación y Empaquetado
- Compilación en Release para arquitectura `win-x64`.
- Instalador NSIS: `DockBarSetup.exe` (v1.7.0).
- Paquete MSIX: `DockBar.msix` (v1.7.0.0).
