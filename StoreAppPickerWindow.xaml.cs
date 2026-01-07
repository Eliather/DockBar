using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DockBar.Models;
using DockBar.Services;

namespace DockBar;

public partial class StoreAppPickerWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<StoreAppInfo> Apps { get; } = new();
    public ICollectionView AppsView { get; }
    public StoreAppInfo? SelectedApp { get; private set; }
    public SolidColorBrush BackgroundBrush { get; }
    public SolidColorBrush ForegroundBrush { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public StoreAppPickerWindow(SolidColorBrush background, SolidColorBrush foreground)
    {
        BackgroundBrush = background;
        ForegroundBrush = foreground;
        InitializeComponent();
        DataContext = this;
        AppsView = CollectionViewSource.GetDefaultView(Apps);
        Loaded += (_, _) => LoadApps();
    }

    private void LoadApps()
    {
        Apps.Clear();
        foreach (var app in StoreAppService.GetInstalledApps())
        {
            Apps.Add(app);
        }
        AppsView.Refresh();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var text = SearchBox.Text?.Trim() ?? string.Empty;
        AppsView.Filter = o =>
        {
            if (o is not StoreAppInfo app) return false;
            if (string.IsNullOrWhiteSpace(text)) return true;
            return app.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(app.FriendlyName) && app.FriendlyName.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                   app.PackageFamilyName.Contains(text, StringComparison.OrdinalIgnoreCase);
        };
        AppsView.Refresh();
    }

    private void AppsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CommitSelection();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        CommitSelection();
    }

    private void CommitSelection()
    {
        if (AppsList.SelectedItem is StoreAppInfo app)
        {
            SelectedApp = app;
            DialogResult = true;
            Close();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
