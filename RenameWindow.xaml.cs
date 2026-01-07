using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

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
