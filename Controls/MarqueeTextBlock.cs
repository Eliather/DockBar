using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using FontFamily = System.Windows.Media.FontFamily;

namespace DockBar.Controls;

public class MarqueeTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnTextOrFontChanged));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Brush),
            typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(double),
            typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnTextOrFontChanged));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(
            nameof(FontWeight),
            typeof(FontWeight),
            typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnTextOrFontChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(
            nameof(ScrollOffset),
            typeof(double),
            typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    private Storyboard? _storyboard;
    private double _textWidth;
    private double _textHeight;

    public MarqueeTextBlock()
    {
        ClipToBounds = true;
        SizeChanged += (_, _) => UpdateMarquee();
        Loaded += (_, _) => UpdateMarquee();
        Unloaded += (_, _) => StopAnimation();
    }

    private static void OnTextOrFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeTextBlock control)
        {
            control.UpdateMarquee();
        }
    }

    private FormattedText CreateFormattedText()
    {
        var text = string.IsNullOrEmpty(Text) ? " " : Text;
        var typeface = new Typeface(
            new FontFamily("Segoe UI, Inter, Arial"),
            FontStyles.Normal,
            FontWeight,
            FontStretches.Normal);

        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            Math.Max(8, FontSize),
            Foreground ?? Brushes.White,
            pixelsPerDip);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ft = CreateFormattedText();
        _textWidth = ft.Width;
        _textHeight = ft.Height;
        var desiredWidth = double.IsPositiveInfinity(availableSize.Width) ? _textWidth : availableSize.Width;
        return new Size(desiredWidth, Math.Max(_textHeight + 2, FontSize + 4));
    }

    private void UpdateMarquee()
    {
        StopAnimation();

        if (string.IsNullOrWhiteSpace(Text) || ActualWidth <= 1)
        {
            ScrollOffset = 0;
            return;
        }

        var ft = CreateFormattedText();
        _textWidth = ft.Width;
        _textHeight = ft.Height;

        var availableWidth = ActualWidth;

        if (_textWidth <= availableWidth)
        {
            // El texto cabe completamente en el contenedor: mantener estático y centrado
            ScrollOffset = 0;
            OpacityMask = null;
            return;
        }

        // El texto sobrepasa el contenedor: animar suavemente
        var overflow = _textWidth - availableWidth + 16;
        var scrollDurationSeconds = Math.Max(2.5, overflow / 24.0); // Velocidad de lectura óptima (~24px/s)
        var pauseStartSeconds = 1.6;
        var pauseEndSeconds = 1.4;
        var returnSeconds = 0.8;
        var totalCycleSeconds = pauseStartSeconds + scrollDurationSeconds + pauseEndSeconds + returnSeconds;

        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(totalCycleSeconds),
            RepeatBehavior = RepeatBehavior.Forever
        };

        // 0s: en posición inicial
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

        // Pausa inicial en 0 para leer el inicio
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseStartSeconds))));

        // Desplazamiento suave hasta el final del texto
        var scrollEndTime = TimeSpan.FromSeconds(pauseStartSeconds + scrollDurationSeconds);
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(
            -overflow,
            KeyTime.FromTimeSpan(scrollEndTime),
            new KeySpline(0.25, 0.1, 0.25, 1.0)));

        // Pausa al final para leer el cierre
        var pauseEndTime = TimeSpan.FromSeconds(pauseStartSeconds + scrollDurationSeconds + pauseEndSeconds);
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(pauseEndTime)));

        // Retorno fluido a la posición inicial
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(
            0.0,
            KeyTime.FromTimeSpan(TimeSpan.FromSeconds(totalCycleSeconds)),
            new KeySpline(0.25, 0.1, 0.25, 1.0)));

        Storyboard.SetTarget(animation, this);
        Storyboard.SetTargetProperty(animation, new PropertyPath(ScrollOffsetProperty));

        _storyboard = new Storyboard();
        _storyboard.Children.Add(animation);
        _storyboard.Begin();

        // Aplicar máscara sutil con desvanecimiento en los bordes para un aspecto premium
        ApplyEdgeFadingMask();
    }

    private void ApplyEdgeFadingMask()
    {
        var mask = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.04));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.96));
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
        mask.Freeze();
        OpacityMask = mask;
    }

    private void StopAnimation()
    {
        if (_storyboard != null)
        {
            _storyboard.Stop();
            _storyboard = null;
        }
        ScrollOffset = 0;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (string.IsNullOrEmpty(Text)) return;

        var ft = CreateFormattedText();
        double x;

        if (_textWidth <= ActualWidth)
        {
            // Centrado si cabe
            x = Math.Max(0, (ActualWidth - _textWidth) / 2.0);
        }
        else
        {
            // Con desplazamiento si no cabe
            x = ScrollOffset;
        }

        var y = Math.Max(0, (ActualHeight - ft.Height) / 2.0);

        // Sombra de texto sutil para legibilidad
        var shadowBrush = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
        shadowBrush.Freeze();
        var shadowFt = CreateFormattedText();
        shadowFt.SetForegroundBrush(shadowBrush);
        drawingContext.DrawText(shadowFt, new Point(x + 1, y + 1));

        // Texto principal
        drawingContext.DrawText(ft, new Point(x, y));
    }
}
