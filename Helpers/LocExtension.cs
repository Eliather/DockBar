using System;
using System.Windows.Markup;
using DockBar.Services;

namespace DockBar;

[MarkupExtensionReturnType(typeof(string))]
public class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return LocalizationService.Get(Key);
    }
}
