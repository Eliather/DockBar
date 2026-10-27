using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DockBar.Services;

namespace DockBar;

public partial class AddLinkWindow : Window, INotifyPropertyChanged
{
    private string _target = string.Empty;
    private string? _displayName;
    private string _validationMessage = string.Empty;

    public string Target
    {
        get => _target;
        set
        {
            if (_target != value)
            {
                _target = value;
                OnPropertyChanged();
                ValidateTarget(showMessage: false);
            }
        }
    }

    public string? DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName != value)
            {
                _displayName = value;
                OnPropertyChanged();
            }
        }
    }

    public string ResolvedTarget { get; private set; } = string.Empty;
    public string? ResolvedArguments { get; private set; }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (_validationMessage != value)
            {
                _validationMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
    public SolidColorBrush BackgroundBrush { get; }
    public SolidColorBrush ForegroundBrush { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AddLinkWindow(SolidColorBrush background, SolidColorBrush foreground)
    {
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

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
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
        if (!ValidateTarget(showMessage: true))
        {
            TargetBox.Focus();
            TargetBox.SelectAll();
            return;
        }

        DialogResult = true;
        Close();
    }

    private bool ValidateTarget(bool showMessage)
    {
        var input = Target?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return SetValidationResult(false, LocalizationService.Get("AddLink_ValidationEmpty"), showMessage);
        }

        if (TryResolveLaunchTarget(input, out var resolvedTarget, out var resolvedArguments))
        {
            ResolvedTarget = resolvedTarget;
            ResolvedArguments = resolvedArguments;
            return SetValidationResult(true, string.Empty, showMessage);
        }

        ResolvedTarget = string.Empty;
        ResolvedArguments = null;
        return SetValidationResult(false, LocalizationService.Get("AddLink_ValidationInvalid"), showMessage);
    }

    private bool SetValidationResult(bool isValid, string message, bool showMessage)
    {
        ValidationMessage = !isValid && showMessage ? message : string.Empty;
        return isValid;
    }

    private static bool TryResolveLaunchTarget(string input, out string resolvedTarget, out string? resolvedArguments)
    {
        resolvedTarget = string.Empty;
        resolvedArguments = null;

        if (input.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            resolvedTarget = input;
            return true;
        }

        if (File.Exists(input) || Directory.Exists(input))
        {
            resolvedTarget = input;
            return true;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && !uri.IsFile && !string.IsNullOrWhiteSpace(uri.Scheme))
        {
            resolvedTarget = input;
            return true;
        }

        if (!TrySplitCommand(input, out var commandToken, out var arguments))
        {
            return false;
        }

        if (!TryResolveExecutable(commandToken, out var executablePath))
        {
            return false;
        }

        resolvedTarget = executablePath;
        resolvedArguments = string.IsNullOrWhiteSpace(arguments) ? null : arguments;
        return true;
    }

    private static bool TrySplitCommand(string input, out string commandToken, out string arguments)
    {
        commandToken = string.Empty;
        arguments = string.Empty;

        var text = input.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text[0] == '"')
        {
            var closingQuote = text.IndexOf('"', 1);
            if (closingQuote <= 1)
            {
                return false;
            }

            commandToken = text[1..closingQuote];
            arguments = text[(closingQuote + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(commandToken);
        }

        var firstSpace = text.IndexOf(' ');
        if (firstSpace < 0)
        {
            commandToken = text;
            return true;
        }

        commandToken = text[..firstSpace];
        arguments = text[(firstSpace + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(commandToken);
    }

    private static bool TryResolveExecutable(string commandToken, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(commandToken))
        {
            return false;
        }

        if (File.Exists(commandToken))
        {
            resolvedPath = commandToken;
            return true;
        }

        var hasDirectoryHint = commandToken.Contains('\\') || commandToken.Contains('/');
        if (hasDirectoryHint)
        {
            return false;
        }

        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pathExtEntries = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var candidateNames = commandToken.Contains('.')
            ? new[] { commandToken }
            : pathExtEntries.Select(ext => $"{commandToken}{ext.ToLowerInvariant()}").Prepend(commandToken);

        foreach (var dir in pathEntries)
        {
            foreach (var candidateName in candidateNames)
            {
                var candidatePath = Path.Combine(dir, candidateName);
                if (File.Exists(candidatePath))
                {
                    resolvedPath = candidatePath;
                    return true;
                }
            }
        }

        return false;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
