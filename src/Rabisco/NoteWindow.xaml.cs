using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Rabisco.Interop;
using Rabisco.Models;
using Rabisco.Services;
using Rabisco.Theming;
using Windows.Graphics;
using VirtualKey = Windows.System.VirtualKey;
using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;
using WinRT.Interop;

namespace Rabisco;

/// <summary>Janela de uma nota adesiva.</summary>
public sealed partial class NoteWindow : Window
{
    private const int DefaultSize = 320;
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly NoteManager _manager;
    private readonly nint _hwnd;
    private readonly DispatcherQueueTimer _contentTimer;
    private readonly DispatcherQueueTimer _layoutTimer;
    private readonly Dictionary<NoteColor, FontIcon> _swatchChecks = new();
    private bool _syncingFontPicker;
    private bool _contentDirty;
    private bool _layoutDirty;

    public NoteWindow(Note note, NoteManager manager)
    {
        Note = note;
        _manager = manager;
        InitializeComponent();

        // Barra de título nativa e fina, sobreposta ao papel: o sistema move a janela sem atraso.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);

        _hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = note.IsPinned;
        }

        RestoreBounds();

        _contentTimer = CreateDebounceTimer(FlushContent);
        _layoutTimer = CreateDebounceTimer(FlushLayout);

        ContentBox.Text = note.Content;
        BuildColorPicker();
        BuildFontPicker();
        ApplyFont();
        AddZoomAccelerators();
        AppSettings.FontChanged += OnFontChanged;
        UpdatePinButton();

        RootGrid.Loaded += OnLoaded;
        RootGrid.ActualThemeChanged += (_, _) => ApplyColors();
        AppWindow.Changed += OnAppWindowChanged;
        Closed += OnClosed;
    }

    public Note Note { get; }

    public void BringToFront()
    {
        AppWindow.Show();
        Activate();
        NativeMethods.SetForegroundWindow(_hwnd);
    }

    public void FlushPendingChanges()
    {
        FlushContent();
        FlushLayout();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyColors();
        UpdateCaptionInset();
        ContentBox.Focus(FocusState.Programmatic);
        ContentBox.Select(ContentBox.Text.Length, 0);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        AppSettings.FontChanged -= OnFontChanged;
        FlushPendingChanges();
        _manager.OnWindowClosed(this);
    }

    // ---- Posição e tamanho ----

    private void RestoreBounds()
    {
        var scale = NativeMethods.GetDpiForWindow(_hwnd) / 96.0;
        var width = Note.Width > 0 ? Note.Width : (int)(DefaultSize * scale);
        var height = Note.Height > 0 ? Note.Height : (int)(DefaultSize * scale);

        if (Note.X is int x && Note.Y is int y)
        {
            var bounds = new RectInt32(x, y, width, height);
            // Ignora a posição salva se o monitor onde a nota estava não existe mais.
            if (DisplayArea.GetFromRect(bounds, DisplayAreaFallback.None) is not null)
            {
                AppWindow.MoveAndResize(bounds);
                return;
            }
        }

        AppWindow.Resize(new SizeInt32(width, height));
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange)
        {
            UpdateCaptionInset();
        }

        if (!args.DidPositionChange && !args.DidSizeChange)
        {
            return;
        }

        Note.X = sender.Position.X;
        Note.Y = sender.Position.Y;
        Note.Width = sender.Size.Width;
        Note.Height = sender.Size.Height;
        _layoutDirty = true;
        Restart(_layoutTimer);
    }

    // Reserva espaço para o botão fechar desenhado pelo sistema.
    private void UpdateCaptionInset()
    {
        if (RootGrid.XamlRoot is { } root)
        {
            CaptionInsetColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / root.RasterizationScale);
        }
    }

    // ---- Fonte e tamanho ----

    private void ApplyFont()
    {
        ContentBox.FontFamily = NoteFonts.Get(AppSettings.FontKey).Family;
        ContentBox.FontSize = AppSettings.GetFontSize(AppSettings.FontKey);
        SizeText.Text = ((int)ContentBox.FontSize).ToString(CultureInfo.CurrentCulture);
    }

    private void ChangeFontSize(double delta)
    {
        var key = AppSettings.FontKey;
        AppSettings.SetFontSize(key, AppSettings.GetFontSize(key) + delta);
    }

    // Ctrl + / Ctrl - (teclado principal e numérico).
    private void AddZoomAccelerators()
    {
        foreach (var key in new[] { (VirtualKey)187, VirtualKey.Add, (VirtualKey)189, VirtualKey.Subtract })
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = VirtualKeyModifiers.Control };
            accelerator.Invoked += ZoomAccelerator_Invoked;
            RootGrid.KeyboardAccelerators.Add(accelerator);
        }
    }

    private void FontSmaller_Click(object sender, RoutedEventArgs e) => ChangeFontSize(-1);

    private void FontLarger_Click(object sender, RoutedEventArgs e) => ChangeFontSize(1);

    private void ZoomAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ChangeFontSize(sender.Key == VirtualKey.Subtract || (int)sender.Key == 189 ? -1 : 1);
        args.Handled = true;
    }

    private void BuildFontPicker()
    {
        foreach (var option in NoteFonts.All)
        {
            // Cada item aparece na própria fonte, como prévia.
            FontPicker.Items.Add(new ComboBoxItem { Content = option.Name, Tag = option.Key, FontFamily = option.Family });
        }

        SelectCurrentFont();
    }

    private void SelectCurrentFont()
    {
        _syncingFontPicker = true;
        FontPicker.SelectedItem = FontPicker.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => (string)item.Tag == AppSettings.FontKey);
        _syncingFontPicker = false;
    }

    private void FontPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingFontPicker && FontPicker.SelectedItem is ComboBoxItem { Tag: string key })
        {
            AppSettings.FontKey = key;
        }
    }

    // Disparado por qualquer nota; todas as janelas abertas acompanham.
    private void OnFontChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyFont();
            SelectCurrentFont();
        });
    }

    // ---- Conteúdo e salvamento ----

    private void ContentBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ContentBox.Text == Note.Content)
        {
            return;
        }

        Note.Content = ContentBox.Text;
        _contentDirty = true;
        Restart(_contentTimer);
    }

    private void FlushContent()
    {
        _contentTimer.Stop();
        if (!_contentDirty)
        {
            return;
        }

        _contentDirty = false;
        _manager.SaveContent(Note);
    }

    private void FlushLayout()
    {
        _layoutTimer.Stop();
        if (!_layoutDirty)
        {
            return;
        }

        _layoutDirty = false;
        _manager.SaveLayout(Note);
    }

    private DispatcherQueueTimer CreateDebounceTimer(Action action)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = SaveDelay;
        timer.IsRepeating = false;
        timer.Tick += (_, _) => action();
        return timer;
    }

    private static void Restart(DispatcherQueueTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    // ---- Cores ----

    private void ApplyColors()
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        RootGrid.Background = new SolidColorBrush(NotePalette.Body(Note.Color, dark));
        ContentBox.Foreground = new SolidColorBrush(NotePalette.Ink(dark));
        TitleBarColors.Apply(AppWindow.TitleBar, dark);
        UpdateSwatchChecks();
    }

    private void BuildColorPicker()
    {
        var checkBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1F, 0x1F, 0x1F));

        foreach (var color in Enum.GetValues<NoteColor>())
        {
            var check = new FontIcon { Glyph = "", FontSize = 12, Foreground = checkBrush };
            var button = new Button
            {
                Padding = new Thickness(2),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = NotePalette.AccentBrush(color),
                    Child = check,
                },
            };

            var name = NotePalette.DisplayName(color);
            ToolTipService.SetToolTip(button, name);
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => SetColor(color);

            _swatchChecks[color] = check;
            ColorSwatches.Children.Add(button);
        }
    }

    private void UpdateSwatchChecks()
    {
        foreach (var (color, check) in _swatchChecks)
        {
            check.Visibility = color == Note.Color ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SetColor(NoteColor color)
    {
        Note.Color = color;
        ApplyColors();
        _contentDirty = true;
        FlushContent();
    }

    // ---- Barra superior e menu ----

    private void NewNote_Click(object sender, RoutedEventArgs e) => _manager.Create(near: Note);

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Note.IsPinned = !Note.IsPinned;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = Note.IsPinned;
        }

        UpdatePinButton();
        _manager.SaveLayout(Note);
    }

    private void UpdatePinButton()
    {
        PinIcon.Glyph = Note.IsPinned ? "" : "";
        var label = Note.IsPinned ? "Desafixar do topo" : "Fixar sempre no topo";
        ToolTipService.SetToolTip(PinButton, label);
        AutomationProperties.SetName(PinButton, label);
    }

    private void MoreFlyout_Opening(object sender, object e)
    {
        DeleteButton.Visibility = Visibility.Visible;
        DeleteConfirmPanel.Visibility = Visibility.Collapsed;
        UpdateSwatchChecks();
    }

    private void ShowList_Click(object sender, RoutedEventArgs e)
    {
        MoreFlyout.Hide();
        App.Current.ShowMainWindow();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note.IsEmpty)
        {
            MoreFlyout.Hide();
            _manager.Delete(Note);
            return;
        }

        DeleteButton.Visibility = Visibility.Collapsed;
        DeleteConfirmPanel.Visibility = Visibility.Visible;
    }

    private void CancelDelete_Click(object sender, RoutedEventArgs e)
    {
        DeleteButton.Visibility = Visibility.Visible;
        DeleteConfirmPanel.Visibility = Visibility.Collapsed;
    }

    private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        MoreFlyout.Hide();
        _manager.Delete(Note);
    }
}
