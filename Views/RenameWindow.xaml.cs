using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using DockBar.Services;

namespace DockBar;

public partial class RenameWindow : Window, INotifyPropertyChanged
{
    public string NewName { get; set; }
    public SolidColorBrush BackgroundBrush { get; }
    public SolidColorBrush ForegroundBrush { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RenameWindow(string currentName, SolidColorBrush background, SolidColorBrush foreground)
    {
        NewName = currentName;
        BackgroundBrush = background;
        ForegroundBrush = foreground;
        InitializeComponent();
        DataContext = this;
        SourceInitialized += (_, _) =>
        {
            WindowSwitcherHelper.HideFromWindowSwitchers(this);
            ThemeService.ApplyWindowBackdrop(this);
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
