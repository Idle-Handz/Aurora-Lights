using System;
using System.IO;
using System.Windows;
using MahApps.Metro.Controls;

namespace Builder.Presentation.Views.Sliders;

public partial class CompendiumSlider : Flyout
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "aurora-compendium-debug.log");

    public CompendiumSlider()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        IsOpenChanged += OnIsOpenChanged;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        Log($"Loaded Position={Position} Width={Width} Height={Height} Actual={ActualWidth}x{ActualHeight} Visibility={Visibility}");

    private void OnIsOpenChanged(object sender, RoutedEventArgs e)
    {
        var parent = Window.GetWindow(this);
        Log($"IsOpenChanged IsOpen={IsOpen} Position={Position} Width={Width} Height={Height} "
            + $"Actual={ActualWidth}x{ActualHeight} Visibility={Visibility} "
            + $"ParentActual={parent?.ActualWidth}x{parent?.ActualHeight}");
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        Log($"SizeChanged New={e.NewSize.Width}x{e.NewSize.Height} Prev={e.PreviousSize.Width}x{e.PreviousSize.Height} IsOpen={IsOpen}");

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
        try { File.AppendAllText(LogPath, line); } catch { }
        System.Diagnostics.Debug.WriteLine("CompendiumSlider: " + message);
    }
}
