namespace Aurora.App;

/// <summary>
/// Native MAUI window that displays a PDF preview using WebView2's built-in
/// PDF renderer and offers a Save button to persist the file to Documents.
/// The <paramref name="tempPdfPath"/> file is deleted when the window closes.
/// </summary>
internal sealed class PdfPreviewPage : ContentPage
{
    private readonly string _tempPdfPath;

    public PdfPreviewPage(string tempPdfPath, string characterName, Func<Task<string?>> saveToDocuments)
    {
        _tempPdfPath    = tempPdfPath;
        Title           = $"Preview — {characterName}";
        BackgroundColor = Color.FromArgb("#1e1e2e");

        // ── Toolbar ──────────────────────────────────────────────────────────────

        var saveBtn = new Button
        {
            Text            = "Save to Documents",
            BackgroundColor = Color.FromArgb("#89b4fa"),
            TextColor       = Colors.Black,
            FontSize        = 13,
            CornerRadius    = 5,
            Padding         = new Thickness(14, 6),
            Margin          = new Thickness(8, 6),
        };

        var statusLabel = new Label
        {
            TextColor           = Color.FromArgb("#a6e3a1"),
            FontSize            = 12,
            VerticalOptions     = LayoutOptions.Center,
            Margin              = new Thickness(4, 0),
            IsVisible           = false,
        };

        saveBtn.Clicked += async (_, _) =>
        {
            saveBtn.IsEnabled = false;
            saveBtn.Text      = "Saving…";
            statusLabel.IsVisible = false;
            try
            {
                var savedPath = await saveToDocuments();
                if (savedPath != null)
                {
                    saveBtn.Text          = "Saved!";
                    statusLabel.Text      = $"Saved to {savedPath}";
                    statusLabel.IsVisible = true;
#if MACCATALYST
                    // Reveal the saved file in Finder — standard macOS UX.
                    System.Diagnostics.Process.Start("open", $"-R \"{savedPath}\"");
#endif
                }
                else
                {
                    // null = user cancelled the save dialog; restore the button.
                    saveBtn.Text      = "Save to Documents";
                    saveBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                saveBtn.Text      = "Save to Documents";
                saveBtn.IsEnabled = true;
                await DisplayAlertAsync("Save Failed", ex.Message, "OK");
            }
        };

        var closeBtn = new Button
        {
            Text            = "Close",
            BackgroundColor = Color.FromArgb("#313244"),
            TextColor       = Color.FromArgb("#cdd6f4"),
            FontSize        = 13,
            CornerRadius    = 5,
            Padding         = new Thickness(12, 6),
            Margin          = new Thickness(0, 6, 8, 6),
        };
        closeBtn.Clicked += (_, _) =>
        {
            if (Application.Current is { } app)
                app.CloseWindow(Window);
            TryDeleteTempFile();
        };

        var toolbar = new Grid
        {
            BackgroundColor = Color.FromArgb("#181825"),
            Padding         = new Thickness(0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto  }, // Save button
                new ColumnDefinition { Width = GridLength.Star  }, // Status label (fills gap)
                new ColumnDefinition { Width = GridLength.Auto  }, // Close button
            },
        };
        toolbar.Add(saveBtn,     column: 0);
        toolbar.Add(statusLabel, column: 1);
        toolbar.Add(closeBtn,    column: 2);

        // ── PDF WebView ──────────────────────────────────────────────────────────
        // WebView2 natively renders PDFs from file:// URIs (same engine as Edge).
        // Convert Windows path (C:\...) to a proper file:/// URL.
        var fileUrl = new Uri(tempPdfPath).AbsoluteUri;

        var webView = new WebView
        {
            Source              = new UrlWebViewSource { Url = fileUrl },
            HorizontalOptions   = LayoutOptions.Fill,
            VerticalOptions     = LayoutOptions.Fill,
            BackgroundColor     = Color.FromArgb("#1e1e2e"),
        };

        // ── Layout ───────────────────────────────────────────────────────────────

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
        };
        root.Add(toolbar, row: 0);
        root.Add(webView,  row: 1);

        Content = root;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window w)
            w.Content.KeyDown += OnWindowKeyDown;
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if WINDOWS
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window w)
            w.Content.KeyDown -= OnWindowKeyDown;
#endif
        TryDeleteTempFile();
    }

#if WINDOWS
    private void OnWindowKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            if (Application.Current is { } app)
                app.CloseWindow(Window);
            TryDeleteTempFile();
        }
    }
#endif

    private void TryDeleteTempFile()
    {
        if (!string.IsNullOrEmpty(_tempPdfPath))
            try { File.Delete(_tempPdfPath); } catch { }
    }
}
