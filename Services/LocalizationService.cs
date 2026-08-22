using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DockBar.Services;

public static class LocalizationService
{
    private static readonly Dictionary<string, Dictionary<string, string>> RawStrings = new()
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
            ["Store_Refresh"] = "Actualizar",
            ["Store_RefreshTooltip"] = "Volver a escanear aplicaciones instaladas",
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
            ["Tray_ToggleSide"] = "Cambiar lado",
            ["Tray_Settings"] = "Ajustes...",
            ["Tray_ConfigFolder"] = "Configuración",
            ["Update_Menu"] = "Actualizar...",
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
            ["Dialog_ExecutableFilter"] = "Ejecutables y accesos (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url|Todos los archivos (*.*)|*.*",
            ["Dialog_SelectIconTitle"] = "Selecciona ícono o imagen",
            ["Dialog_ImageFilter"] = "Imágenes e íconos (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|Todos los archivos (*.*)|*.*",

            ["Config_NotFound"] = "No se encontró configuración previa. Se creó una nueva en AppData.",
            ["Config_ReadError"] = "El archivo de configuración estaba dañado o no se pudo leer. Se restauraron los valores por defecto.",

            ["AutoStart_Prompt"] = "¿Deseas que DockBar se inicie automáticamente al encender tu equipo?",
            ["AutoStart_Title"] = "Inicio automático",

            ["Update_Title"] = "Actualización de DockBar",
            ["Update_Subtitle"] = "Una nueva versión de DockBar está disponible con mejoras y correcciones.",
            ["Update_Checking"] = "Buscando actualizaciones...",
            ["Update_LatestTitle"] = "DockBar actualizado",
            ["Update_LatestBody"] = "Ya estás usando la versión más reciente ({0}).",
            ["Update_Available"] = "Hay una nueva versión disponible ({0}). ¿Deseas descargarla e instalarla ahora?",
            ["Update_AvailableTitle"] = "Nueva versión disponible",
            ["Update_AvailableBody"] = "Versión actual: {0}\nNueva versión: {1}\n\n¿Deseas actualizar ahora?",
            ["Update_CheckFailed"] = "No se pudo comprobar si hay actualizaciones disponibles.",
            ["Update_UpToDate"] = "DockBar ya está actualizado a la versión más reciente ({0}).",
            ["Update_NoInstaller"] = "No se encontró el instalador ejecutable para la versión {0}.",
            ["Update_CurrentVersion"] = "Versión actual",
            ["Update_NewVersion"] = "Nueva versión",
            ["Update_InstallNow"] = "Actualizar ahora",
            ["Update_Later"] = "Más tarde",
            ["Update_Changelog"] = "Novedades de la versión",
            ["Update_NoChangelog"] = "Esta versión incluye optimizaciones y mejoras generales de rendimiento.",
            ["Update_StatusReady"] = "Listo para descargar e instalar la actualización.",
            ["Update_StatusDownloading"] = "Descargando actualización...",
            ["Update_StatusDownloaded"] = "Descarga completada. Iniciando instalador...",
            ["Update_Downloading"] = "Descargando actualización...",
            ["Update_DownloadFailed"] = "No se pudo descargar la actualización.",
            ["Update_ErrorTitle"] = "Error al buscar actualizaciones",
            ["Update_ErrorBody"] = "Ocurrió un error al verificar actualizaciones.",
            ["Update_OpenReleasePage"] = "Ver en GitHub",

            ["About_Title"] = "Acerca de DockBar",
            ["About_Subtitle"] = "Información del sistema y versión actual",
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
            ["Settings_Subtitle"] = "Make the dock yours: adjust size, response, and visual finish with live preview.",
            ["Settings_Size"] = "Size",
            ["Settings_SizeHint"] = "Set dock width and shortcut prominence.",
            ["Settings_DockWidth"] = "Dock width (px)",
            ["Settings_IconSize"] = "Icon size (px)",
            ["Settings_AutoHide"] = "Auto-hide & animation",
            ["Settings_BehaviorHint"] = "Tune reveal and hide speed for a smooth interaction.",
            ["Settings_HideDelay"] = "Hide delay (seconds, 0 = immediate Win8 style)",
            ["Settings_AnimDuration"] = "Hide/show animation duration (ms)",
            ["Settings_AutoStart"] = "Start with Windows",
            ["Settings_ColorTransparency"] = "Color & Glass effect",
            ["Settings_AppearanceHint"] = "Adjust base color and toggle solid or Glass background.",
            ["Settings_UseTransparency"] = "Enable Glass effect",
            ["Settings_Opacity"] = "Opacity",
            ["Settings_GlassEffect"] = "Glass effect",
            ["Settings_GlassOn"] = "Active",
            ["Settings_GlassOff"] = "Inactive",
            ["Settings_ColorPicker"] = "Background color (HEX & picker)",
            ["Settings_ColorHint"] = "Pick a hue using HSV or enter a HEX value.",
            ["Settings_Preview"] = "Preview",
            ["Settings_PreviewHint"] = "Quick reference for background, text, and visual balance.",
            ["Settings_PreviewSummary"] = "Current summary",
            ["Settings_HexLabel"] = "HEX color",
            ["Settings_ColorPalette"] = "Quick palette",
            ["Settings_ColorPaletteHint"] = "Balanced presets so you don't start from scratch.",
            ["Settings_TextColor"] = "Text color:",
            ["Settings_TextLight"] = "Light (white)",
            ["Settings_TextDark"] = "Dark (black)",
            ["Settings_DefaultConfig"] = "Restore defaults",
            ["Settings_FooterHint"] = "Preview reflects changes instantly before saving.",

            ["AddLink_Title"] = "Add app or shortcut",
            ["AddLink_Subtitle"] = "Paste a path, executable, folder, or command and give it a clean name.",
            ["AddLink_Target"] = "Path, executable, or command",
            ["AddLink_TargetHint"] = "Use a .exe, folder, URI, or supported Windows command.",
            ["AddLink_NameOptional"] = "Display name (optional)",
            ["AddLink_NameHint"] = "If left empty, DockBar will auto-detect a name.",
            ["AddLink_ExamplesTitle"] = "Useful examples",
            ["AddLink_ExamplesBody"] = "Use this to add an external app, frequent folder, or command shortcut.",
            ["AddLink_ExamplesFooter"] = "For installed system apps, use 'Installed apps...' picker.",
            ["AddLink_ValidationEmpty"] = "Enter a path, URI, or command before saving.",
            ["AddLink_ValidationInvalid"] = "The path or command appears invalid. Use an existing file, folder, URI, or Windows command.",

            ["Rename_Title"] = "Rename shortcut",
            ["Rename_Subtitle"] = "Change the visible shortcut name without changing its target.",
            ["Rename_NewName"] = "New name",

            ["Store_Title"] = "Installed apps",
            ["Store_Subtitle"] = "Browse detected Windows apps and add them with system icons.",
            ["Store_Search"] = "Search app",
            ["Store_SearchHint"] = "Search by display name, internal ID, or package string.",
            ["Store_Refresh"] = "Refresh",
            ["Store_RefreshTooltip"] = "Re-scan installed applications",
            ["Store_Loading"] = "Loading installed apps...",
            ["Store_Empty"] = "No installed apps found.",
            ["Store_PanelTitle"] = "Quick selection",
            ["Store_PanelBody"] = "Designed to add system apps easily without manual paths.",
            ["Store_PanelTipOneTitle"] = "What you'll find",
            ["Store_PanelTipOneBody"] = "UWP apps, system shortcuts, and executable app entries.",
            ["Store_PanelTipTwoTitle"] = "Choosing the right app",
            ["Store_PanelTipTwoBody"] = "If names look similar, check the ID below the item.",
            ["Store_PanelFooter"] = "Double-click also adds the selected app.",

            ["Tray_Open"] = "Open",
            ["Tray_ToggleSide"] = "Toggle side",
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
            ["Dialog_ExecutableFilter"] = "Executables and shortcuts (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url|All files (*.*)|*.*",
            ["Dialog_SelectIconTitle"] = "Select icon or image",
            ["Dialog_ImageFilter"] = "Images and icons (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",

            ["Config_NotFound"] = "No previous config found. Created a default one in AppData.",
            ["Config_ReadError"] = "Config file was corrupted or unreadable. Restored default values.",

            ["AutoStart_Prompt"] = "Do you want DockBar to launch automatically when your computer starts?",
            ["AutoStart_Title"] = "Auto-Start",

            ["Update_Title"] = "DockBar Update",
            ["Update_Subtitle"] = "A new version of DockBar is available with improvements and fixes.",
            ["Update_Checking"] = "Checking for updates...",
            ["Update_LatestTitle"] = "DockBar up to date",
            ["Update_LatestBody"] = "You are running the latest version ({0}).",
            ["Update_Available"] = "A new version is available ({0}). Do you want to download and install it now?",
            ["Update_AvailableTitle"] = "New version available",
            ["Update_AvailableBody"] = "Current version: {0}\nNew version: {1}\n\nDo you want to update now?",
            ["Update_CheckFailed"] = "Could not check for updates at this time.",
            ["Update_UpToDate"] = "DockBar is up to date with the latest version ({0}).",
            ["Update_NoInstaller"] = "No installer executable was found for version {0}.",
            ["Update_CurrentVersion"] = "Current version",
            ["Update_NewVersion"] = "New version",
            ["Update_InstallNow"] = "Update now",
            ["Update_Later"] = "Later",
            ["Update_Changelog"] = "What's new in this version",
            ["Update_NoChangelog"] = "This release includes optimizations and general performance improvements.",
            ["Update_StatusReady"] = "Ready to download and install update.",
            ["Update_StatusDownloading"] = "Downloading update...",
            ["Update_StatusDownloaded"] = "Download complete. Starting installer...",
            ["Update_Downloading"] = "Downloading update...",
            ["Update_DownloadFailed"] = "Failed to download update.",
            ["Update_ErrorTitle"] = "Update check error",
            ["Update_ErrorBody"] = "An error occurred while checking for updates.",
            ["Update_OpenReleasePage"] = "View on GitHub",

            ["About_Title"] = "About DockBar",
            ["About_Subtitle"] = "System information and current version",
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

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings = BuildReadOnlyStrings();

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildReadOnlyStrings()
    {
        var dict = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var langKvp in RawStrings)
        {
            dict[langKvp.Key] = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(langKvp.Value, StringComparer.OrdinalIgnoreCase));
        }

        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(dict);
    }

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
