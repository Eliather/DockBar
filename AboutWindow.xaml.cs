using System;
using System.IO;
using System.Windows;
using DockBar.Services;

namespace DockBar;

public partial class AboutWindow : Window
{
    public string SubtitleText { get; }
    public string VersionValue { get; }
    public string DeveloperName { get; }
    public string DescriptionText { get; }
    public string ConfigPathLabel { get; }
    public string ConfigPathValue { get; }

    public AboutWindow(string version)
    {
        SubtitleText = LocalizationService.Get("About_Subtitle");
        VersionValue = version;
        DeveloperName = LocalizationService.Get("About_DeveloperName");
        DescriptionText = LocalizationService.Get("About_DescriptionText");
        ConfigPathLabel = LocalizationService.Get("About_ConfigLabel");
        ConfigPathValue = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DockBar",
            "shortcuts.json");

        InitializeComponent();
        DataContext = this;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
