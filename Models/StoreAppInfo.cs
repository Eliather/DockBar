using System.Windows.Media;

namespace DockBar.Models;

public class StoreAppInfo
{
    public string Name { get; set; } = string.Empty;
    public string PackageFamilyName { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string? FriendlyName { get; set; }
    public ImageSource? Icon { get; set; }
}
