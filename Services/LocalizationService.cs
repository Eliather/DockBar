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
            ["Common_Add"] = "Agregar",
            ["Common_About"] = "Acerca de",
            ["Common_Page"] = "Página",

            ["Settings_Title"] = "Ajustes",
            ["Settings_Size"] = "Tamaño",
            ["Settings_DockWidth"] = "Ancho de barra (px)",
            ["Settings_IconSize"] = "Tamaño de ícono (px)",
            ["Settings_AutoHide"] = "Auto-ocultamiento y animación",
            ["Settings_HideDelay"] = "Retardo al ocultar (segundos, 0 = inmediato estilo Win8)",
            ["Settings_AnimDuration"] = "Duración animación ocultar/mostrar (ms)",
            ["Settings_AutoStart"] = "Iniciar con Windows",
            ["Settings_ColorTransparency"] = "Color y transparencia",
            ["Settings_UseTransparency"] = "Usar transparencia",
            ["Settings_Opacity"] = "Opacidad",
            ["Settings_ColorPicker"] = "Color de fondo (HEX y selector)",
            ["Settings_TextColor"] = "Color de texto:",
            ["Settings_TextLight"] = "Claro (blanco)",
            ["Settings_TextDark"] = "Oscuro (negro)",
            ["Settings_DefaultConfig"] = "Configuración predeterminada",

            ["AddLink_Title"] = "Agregar acceso",
            ["AddLink_Target"] = "Destino (ruta o URI)",
            ["AddLink_NameOptional"] = "Nombre (opcional)",

            ["Rename_Title"] = "Renombrar acceso",
            ["Rename_NewName"] = "Nuevo nombre",

            ["Store_Title"] = "Apps de Microsoft Store",
            ["Store_Search"] = "Buscar app",

            ["Tray_Open"] = "Abrir",
            ["Tray_ToggleSide"] = "Cambiar lado (Izq/Der)",
            ["Tray_Settings"] = "Ajustes...",
            ["Tray_ConfigFolder"] = "Configuración",
            ["Update_Menu"] = "Buscar actualizaciones...",
            ["Tray_Exit"] = "Salir",

            ["AddMenu_File"] = "Archivo / ejecutable...",
            ["AddMenu_Store"] = "App de Microsoft Store...",
            ["AddMenu_Uri"] = "Comando / URI...",

            ["Dialog_SelectShortcutTitle"] = "Selecciona acceso directo o ejecutable",
            ["Dialog_SelectShortcutFilter"] = "Accesos directos y apps|*.lnk;*.exe|Todos los archivos|*.*",
            ["Dialog_SelectIconTitle"] = "Selecciona ícono (ico/png/exe/lnk)",
            ["Dialog_SelectIconFilter"] = "Íconos (*.ico)|*.ico|Imágenes (*.png;*.jpg)|*.png;*.jpg|Ejecutables/Atajos (*.exe;*.lnk)|*.exe;*.lnk|Todos los archivos|*.*",

            ["Config_NotFound"] = "No existe configuración previa. Se creará un archivo predeterminado.",
            ["Config_ReadError"] = "No se pudo leer el archivo de configuración (corrupto o inaccesible). Se creará uno predeterminado.",
            ["AutoStart_Prompt"] = "¿Deseas iniciar DockBar con Windows?",

            ["Update_Title"] = "Actualización",
            ["Update_Available"] = "Hay una nueva versión disponible: {0}. ¿Deseas descargar e instalar ahora?",
            ["Update_NoInstaller"] = "No se encontró el instalador en el release.",
            ["Update_DownloadFailed"] = "No se pudo descargar el instalador.",
            ["Update_UpToDate"] = "Ya tienes la versión más reciente.",
            ["Update_CheckFailed"] = "No se pudo comprobar actualizaciones.",

            ["About_Title"] = "Acerca de DockBar",
            ["About_Version"] = "Versión",
            ["About_UnknownVersion"] = "desconocida",
            ["About_DevelopedBy"] = "Desarrollado por Eliather",
            ["About_Description"] = "Descripción: barra lateral de accesos directos para Windows.",
            ["About_ConfigPath"] = "Configuración: %AppData%\\DockBar\\shortcuts.json"
        },
        ["en"] = new Dictionary<string, string>
        {
            ["Common_Save"] = "Save",
            ["Common_Cancel"] = "Cancel",
            ["Common_Add"] = "Add",
            ["Common_About"] = "About",
            ["Common_Page"] = "Page",

            ["Settings_Title"] = "Settings",
            ["Settings_Size"] = "Size",
            ["Settings_DockWidth"] = "Dock width (px)",
            ["Settings_IconSize"] = "Icon size (px)",
            ["Settings_AutoHide"] = "Auto-hide and animation",
            ["Settings_HideDelay"] = "Hide delay (seconds, 0 = immediate Win8 style)",
            ["Settings_AnimDuration"] = "Hide/show animation duration (ms)",
            ["Settings_AutoStart"] = "Start with Windows",
            ["Settings_ColorTransparency"] = "Color and transparency",
            ["Settings_UseTransparency"] = "Use transparency",
            ["Settings_Opacity"] = "Opacity",
            ["Settings_ColorPicker"] = "Background color (HEX + picker)",
            ["Settings_TextColor"] = "Text color:",
            ["Settings_TextLight"] = "Light (white)",
            ["Settings_TextDark"] = "Dark (black)",
            ["Settings_DefaultConfig"] = "Default configuration",

            ["AddLink_Title"] = "Add shortcut",
            ["AddLink_Target"] = "Target (path or URI)",
            ["AddLink_NameOptional"] = "Name (optional)",

            ["Rename_Title"] = "Rename shortcut",
            ["Rename_NewName"] = "New name",

            ["Store_Title"] = "Microsoft Store apps",
            ["Store_Search"] = "Search app",

            ["Tray_Open"] = "Open",
            ["Tray_ToggleSide"] = "Switch side (Left/Right)",
            ["Tray_Settings"] = "Settings...",
            ["Tray_ConfigFolder"] = "Configuration",
            ["Update_Menu"] = "Check for updates...",
            ["Tray_Exit"] = "Exit",

            ["AddMenu_File"] = "File / executable...",
            ["AddMenu_Store"] = "Microsoft Store app...",
            ["AddMenu_Uri"] = "Command / URI...",

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
            ["About_Version"] = "Version",
            ["About_UnknownVersion"] = "unknown",
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
