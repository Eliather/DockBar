using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace DockBar.Models;

public class ShortcutItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string? _iconPath;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Path
    {
        get => _path;
        set
        {
            if (_path != value)
            {
                _path = value;
                OnPropertyChanged();
            }
        }
    }

    public string? IconPath
    {
        get => _iconPath;
        set
        {
            if (_iconPath != value)
            {
                _iconPath = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    private ImageSource? _icon;

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
