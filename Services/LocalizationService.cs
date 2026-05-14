using System;
using System.Collections.Generic;
using System.Globalization;

namespace DockBar.Services;

public static class LocalizationService
{
    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["es"] = new Dictionary<string, string>
        {
            ["Common_Save"] = "Guardar",
            ["Common_Cancel"] = "Cancelar",
            ["Common_Close"] = "Cerrar",
            ["Common_Add"] = "Agregar",
            ["Common_About"] = "Acerca de",
            ["Common_Page"] = "Página",

            ["Settings_Title"] = "Ajustes",
            ["Settings_Subtitle"] = "Haz que el dock se sienta tuyo: ajusta tamaño, respuesta y acabado visual con vista previa en tiempo real.",
            ["Settings_Size"] = "Tamaño",
            ["Settings_SizeHint"] = "Define cuánto espacio ocupa la barra y qué protagonismo tienen los accesos.",
            ["Settings_DockWidth"] = "Ancho de barra (px)",
            ["Settings_IconSize"] = "Tamaño de ícono (px)",
            ["Settings_AutoHide"] = "Auto-ocultamiento y animación",
            ["Settings_BehaviorHint"] = "Ajusta el ritmo con el que aparece y se oculta para que se sienta suave, no torpe.",
            ["Settings_HideDelay"] = "Retardo al ocultar (segundos, 0 = inmediato estilo Win8)",
            ["Settings_AnimDuration"] = "Duración de la animación al ocultar y mostrar (ms)",
            ["Settings_AutoStart"] = "Iniciar con Windows",
            ["Settings_ColorTransparency"] = "Color y efecto Glass",
            ["Settings_AppearanceHint"] = "Ajusta el color base y decide si el dock usa un acabado Glass o un fondo sólido.",
            ["Settings_UseTransparency"] = "Activar efecto Glass",
            ["Settings_Opacity"] = "Opacidad",
            ["Settings_GlassEffect"] = "Efecto Glass",
            ["Settings_GlassOn"] = "Activo",
            ["Settings_GlassOff"] = "Inactivo",
            ["Settings_ColorPicker"] = "Color de fondo (HEX y selector)",
            ["Settings_ColorHint"] = "Elige un tono con el lienzo HSV o escribe el valor HEX manualmente.",
            ["Settings_Preview"] = "Vista previa",
            ["Settings_PreviewHint"] = "Una referencia rápida de cómo se verán el fondo, el texto y la densidad visual.",
            ["Settings_PreviewSummary"] = "Resumen actual",
            ["Settings_HexLabel"] = "Color HEX",
            ["Settings_ColorPalette"] = "Paleta rápida",
            ["Settings_ColorPaletteHint"] = "Puntos de partida equilibrados para no comenzar desde cero.",
            ["Settings_TextColor"] = "Color de texto:",
            ["Settings_TextLight"] = "Claro (blanco)",
            ["Settings_TextDark"] = "Oscuro (negro)",
            ["Settings_DefaultConfig"] = "Restaurar base",
            ["Settings_FooterHint"] = "La vista previa refleja los cambios al instante antes de guardarlos.",

            ["AddLink_Title"] = "Agregar app o acceso",
            ["AddLink_Subtitle"] = "Pega una ruta, un ejecutable, una carpeta o un comando y dale un nombre limpio para el dock.",
            ["AddLink_Target"] = "Ruta, ejecutable o comando",
            ["AddLink_TargetHint"] = "Puedes usar un .exe, una carpeta, una URI o un comando compatible con Windows.",
            ["AddLink_NameOptional"] = "Nombre visible (opcional)",
            ["AddLink_NameHint"] = "Si lo dejas vacío, DockBar intentará usar un nombre detectado automáticamente.",
            ["AddLink_ExamplesTitle"] = "Ejemplos útiles",
            ["AddLink_ExamplesBody"] = "Úsalo cuando quieras agregar una app externa, una carpeta frecuente o un acceso por comando.",
            ["AddLink_ExamplesFooter"] = "Para apps instaladas del sistema, usa el selector de la opción “Apps instaladas...”.",
            ["AddLink_ValidationEmpty"] = "Escribe una ruta, URI o comando antes de guardar.",
            ["AddLink_ValidationInvalid"] = "La ruta o comando no parece válido. Usa un archivo existente, una carpeta, una URI o un comando ejecutable de Windows.",

            ["Rename_Title"] = "Renombrar acceso",
            ["Rename_Subtitle"] = "Cambia el nombre visible del acceso sin modificar su destino.",
            ["Rename_NewName"] = "Nuevo nombre",

            ["Store_Title"] = "Apps instaladas",
            ["Store_Subtitle"] = "Explora las aplicaciones detectadas en Windows y agrégalas al dock con su ícono del sistema.",
            ["Store_Search"] = "Buscar app",
            ["Store_SearchHint"] = "Puedes buscar por nombre visible, identificador interno o parte del paquete.",
            ["Store_Loading"] = "Cargando apps instaladas...",
            ["Store_Empty"] = "No se encontraron apps instaladas.",
            ["Store_PanelTitle"] = "Selección rápida",
            ["Store_PanelBody"] = "Este panel está pensado para sumar apps instaladas sin tener que pegar rutas manualmente.",
            ["Store_PanelTipOneTitle"] = "Qué vas a encontrar",
            ["Store_PanelTipOneBody"] = "Apps UWP, accesos detectados por Windows y entradas que exponen un identificador ejecutable.",
            ["Store_PanelTipTwoTitle"] = "Cómo elegir mejor",
            ["Store_PanelTipTwoBody"] = "Si ves nombres parecidos, revisa el identificador inferior para distinguir la app correcta.",
            ["Store_PanelFooter"] = "Doble clic también agrega la app seleccionada.",

            ["Tray_Open"] = "Abrir",
            ["Tray_ToggleSide"] = "Cambiar lado (Izq./Der.)",
            ["Tray_Settings"] = "Ajustes...",
            ["Tray_ConfigFolder"] = "Configuración",
            ["Update_Menu"] = "Buscar actualizaciones...",
            ["Tray_Exit"] = "Salir",

            ["AddMenu_File"] = "Archivo / ejecutable...",
            ["AddMenu_Store"] = "Apps instaladas...",
            ["AddMenu_Uri"] = "Comando / URI...",
            ["Dock_TooltipAdd"] = "Agregar acceso",
            ["Dock_TooltipSettings"] = "Abrir ajustes",
            ["Dock_TooltipEditMode"] = "Activar modo edición",
            ["Dock_TooltipClose"] = "Cerrar DockBar",
            ["Dock_TooltipRename"] = "Renombrar",
            ["Dock_TooltipChangeIcon"] = "Cambiar ícono",
            ["Dock_TooltipRemove"] = "Eliminar acceso",

            ["Dialog_SelectShortcutTitle"] = "Selecciona acceso directo o ejecutable",
            ["Dialog_SelectShortcutFilter"] = "Accesos directos y apps|*.lnk;*.exe|Todos los archivos|*.*",
            ["Dialog_SelectIconTitle"] = "Selecciona ícono (ico/png/exe/lnk)",
            ["Dialog_SelectIconFilter"] = "Íconos (*.ico)|*.ico|Imágenes (*.png;*.jpg)|*.png;*.jpg|Ejecutables/Atajos (*.exe;*.lnk)|*.exe;*.lnk|Todos los archivos|*.*",

            ["Config_NotFound"] = "No existe una configuración previa. Se creará un archivo predeterminado.",
            ["Config_ReadError"] = "No se pudo leer el archivo de configuración (está dañado o inaccesible). Se creará uno predeterminado.",
            ["AutoStart_Prompt"] = "¿Deseas iniciar DockBar con Windows?",

            ["Update_Title"] = "Actualización",
            ["Update_Available"] = "Hay una nueva versión disponible: {0}. ¿Deseas descargarla e instalarla ahora?",
            ["Update_NoInstaller"] = "No se encontró el instalador en la publicación.",
            ["Update_DownloadFailed"] = "No se pudo descargar el instalador.",
            ["Update_UpToDate"] = "Ya tienes la versión más reciente.",
            ["Update_CheckFailed"] = "No se pudieron comprobar actualizaciones.",

            ["About_Title"] = "Acerca de DockBar",
            ["About_Subtitle"] = "Versión, autor y configuración de esta instalación.",
            ["About_Version"] = "Versión",
            ["About_UnknownVersion"] = "desconocida",
            ["About_DeveloperCaption"] = "Desarrollado por",
            ["About_DeveloperName"] = "Eliather",
            ["About_DescriptionText"] = "Barra lateral de accesos directos para Windows.",
            ["About_ConfigLabel"] = "Archivo de configuración",
            ["About_DevelopedBy"] = "Desarrollado por Eliather",
            ["About_Description"] = "Descripción: barra lateral de accesos directos para Windows.",
            ["About_ConfigPath"] = "Configuración: %AppData%\\DockBar\\shortcuts.json"
        },
        ["en"] = new Dictionary<string, string>
        {
            ["Common_Save"] = "Save",
            ["Common_Cancel"] = "Cancel",
            ["Common_Close"] = "Close",
            ["Common_Add"] = "Add",
            ["Common_About"] = "About",
            ["Common_Page"] = "Page",

            ["Settings_Title"] = "Settings",
            ["Settings_Subtitle"] = "Tune the dock's size, behavior, and visual style with a live preview.",
            ["Settings_Size"] = "Size",
            ["Settings_SizeHint"] = "Define how much space the bar occupies and how much presence each shortcut should have.",
            ["Settings_DockWidth"] = "Dock width (px)",
            ["Settings_IconSize"] = "Icon size (px)",
            ["Settings_AutoHide"] = "Auto-hide and animation",
            ["Settings_BehaviorHint"] = "Control how quickly the dock appears and disappears so it feels responsive instead of clunky.",
            ["Settings_HideDelay"] = "Hide delay (seconds, 0 = immediate Win8 style)",
            ["Settings_AnimDuration"] = "Hide/show animation duration (ms)",
            ["Settings_AutoStart"] = "Start with Windows",
            ["Settings_ColorTransparency"] = "Color and Glass effect",
            ["Settings_AppearanceHint"] = "Adjust the base color and decide whether the dock uses a Glass finish or a solid background.",
            ["Settings_UseTransparency"] = "Enable Glass effect",
            ["Settings_Opacity"] = "Opacity",
            ["Settings_GlassEffect"] = "Glass effect",
            ["Settings_GlassOn"] = "Enabled",
            ["Settings_GlassOff"] = "Disabled",
            ["Settings_ColorPicker"] = "Background color (HEX + picker)",
            ["Settings_ColorHint"] = "Choose a base color with the HSV canvas or type the HEX value manually.",
            ["Settings_Preview"] = "Preview",
            ["Settings_PreviewHint"] = "This is how the dock background, text, and density will look.",
            ["Settings_PreviewSummary"] = "Current summary",
            ["Settings_HexLabel"] = "HEX color",
            ["Settings_ColorPalette"] = "Quick palette",
            ["Settings_ColorPaletteHint"] = "Balanced colors to get started without hunting for a tone from scratch.",
            ["Settings_TextColor"] = "Text color:",
            ["Settings_TextLight"] = "Light (white)",
            ["Settings_TextDark"] = "Dark (black)",
            ["Settings_DefaultConfig"] = "Restore defaults",
            ["Settings_FooterHint"] = "The preview updates instantly before you save.",

            ["AddLink_Title"] = "Add app or shortcut",
            ["AddLink_Subtitle"] = "Paste a path, executable, folder, or command and give it a cleaner label for the dock.",
            ["AddLink_Target"] = "Path, executable, or command",
            ["AddLink_TargetHint"] = "You can use an .exe, folder, URI, or any Windows-compatible command.",
            ["AddLink_NameOptional"] = "Visible name (optional)",
            ["AddLink_NameHint"] = "If left empty, DockBar will try to derive a usable name automatically.",
            ["AddLink_ExamplesTitle"] = "Useful examples",
            ["AddLink_ExamplesBody"] = "Use this flow when you want to add an external app, a frequent folder, or a command-based shortcut.",
            ["AddLink_ExamplesFooter"] = "For detected system apps, use the “Installed apps...” picker.",
            ["AddLink_ValidationEmpty"] = "Enter a path, URI, or command before saving.",
            ["AddLink_ValidationInvalid"] = "That path or command does not look valid. Use an existing file, folder, URI, or a Windows-executable command.",

            ["Rename_Title"] = "Rename shortcut",
            ["Rename_Subtitle"] = "Change the visible shortcut name without modifying its target.",
            ["Rename_NewName"] = "New name",

            ["Store_Title"] = "Installed apps",
            ["Store_Subtitle"] = "Browse Windows-detected applications and add them to the dock with their system icon.",
            ["Store_Search"] = "Search app",
            ["Store_SearchHint"] = "Search by visible name, internal identifier, or package fragment.",
            ["Store_Loading"] = "Loading installed apps...",
            ["Store_Empty"] = "No installed apps were found.",
            ["Store_PanelTitle"] = "Quick selection",
            ["Store_PanelBody"] = "This panel is meant for adding installed apps without manually hunting for their file paths.",
            ["Store_PanelTipOneTitle"] = "What you will find",
            ["Store_PanelTipOneBody"] = "UWP apps, Windows-detected entries, and launchable items that expose an app identifier.",
            ["Store_PanelTipTwoTitle"] = "How to choose better",
            ["Store_PanelTipTwoBody"] = "If two entries look similar, compare the identifier line below the app name to pick the right one.",
            ["Store_PanelFooter"] = "Double-click also adds the selected app.",

            ["Tray_Open"] = "Open",
            ["Tray_ToggleSide"] = "Switch side (Left/Right)",
            ["Tray_Settings"] = "Settings...",
            ["Tray_ConfigFolder"] = "Configuration",
            ["Update_Menu"] = "Check for updates...",
            ["Tray_Exit"] = "Exit",

            ["AddMenu_File"] = "File / executable...",
            ["AddMenu_Store"] = "Installed apps...",
            ["AddMenu_Uri"] = "Command / URI...",
            ["Dock_TooltipAdd"] = "Add shortcut",
            ["Dock_TooltipSettings"] = "Open settings",
            ["Dock_TooltipEditMode"] = "Toggle edit mode",
            ["Dock_TooltipClose"] = "Close DockBar",
            ["Dock_TooltipRename"] = "Rename",
            ["Dock_TooltipChangeIcon"] = "Change icon",
            ["Dock_TooltipRemove"] = "Remove shortcut",

            ["Dialog_SelectShortcutTitle"] = "Select shortcut or executable",
            ["Dialog_SelectShortcutFilter"] = "Shortcuts and apps|*.lnk;*.exe|All files|*.*",
            ["Dialog_SelectIconTitle"] = "Select icon (ico/png/exe/lnk)",
            ["Dialog_SelectIconFilter"] = "Icons (*.ico)|*.ico|Images (*.png;*.jpg)|*.png;*.jpg|Executables/Shortcuts (*.exe;*.lnk)|*.exe;*.lnk|All files|*.*",

            ["Config_NotFound"] = "No configuration found. A default one will be created.",
            ["Config_ReadError"] = "Could not read the configuration file (corrupt or inaccessible). A default one will be created.",
            ["AutoStart_Prompt"] = "Do you want DockBar to start with Windows?",

            ["Update_Title"] = "Update",
            ["Update_Available"] = "A new version is available: {0}. Do you want to download and install it now?",
            ["Update_NoInstaller"] = "Installer not found in the release.",
            ["Update_DownloadFailed"] = "Could not download the installer.",
            ["Update_UpToDate"] = "You already have the latest version.",
            ["Update_CheckFailed"] = "Could not check for updates.",

            ["About_Title"] = "About DockBar",
            ["About_Subtitle"] = "Version, author, and configuration details for this installation.",
            ["About_Version"] = "Version",
            ["About_UnknownVersion"] = "unknown",
            ["About_DeveloperCaption"] = "Developed by",
            ["About_DeveloperName"] = "Eliather",
            ["About_DescriptionText"] = "Shortcut sidebar for Windows.",
            ["About_ConfigLabel"] = "Configuration file",
            ["About_DevelopedBy"] = "Developed by Eliather",
            ["About_Description"] = "Description: shortcut sidebar for Windows.",
            ["About_ConfigPath"] = "Configuration: %AppData%\\DockBar\\shortcuts.json"
        }
    };

    private static string _language = GetDefaultLanguage();

    public static void SetLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        _language = normalized;
    }

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var lang = _language;
        if (Strings.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Strings.TryGetValue("es", out var fallback) && fallback.TryGetValue(key, out var esValue))
        {
            return esValue;
        }

        return key;
    }

    private static string GetDefaultLanguage()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return NormalizeLanguage(lang);
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        return "es";
    }
}
