using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace DockBar;

public partial class AddLinkWindow : Window, INotifyPropertyChanged
{
    public string Target { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public SolidColorBrush BackgroundBrush { get; }
    public SolidColorBrush ForegroundBrush { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AddLinkWindow(SolidColorBrush background, SolidColorBrush foreground)
    {
        BackgroundBrush = background;
        ForegroundBrush = foreground;
        InitializeComponent();
        DataContext = this;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Target))
        {
            return;
        }
        DialogResult = true;
        Close();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
