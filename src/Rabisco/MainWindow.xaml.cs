using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rabisco.Interop;
using Rabisco.Models;
using Rabisco.Services;
using Rabisco.Theming;
using Windows.Graphics;
using WinRT.Interop;

namespace Rabisco;

/// <summary>Lista de todas as notas, com busca.</summary>
public sealed partial class MainWindow : Window
{
    private const int HotKeyNewNote = 1;
    private const int HotKeyShowList = 2;

    private readonly NoteManager _manager;
    private readonly nint _hwnd;
    private readonly WindowSubclass _subclass;
    private readonly TrayIcon _tray;
    private string _filter = string.Empty;

    public MainWindow(NoteManager manager)
    {
        _manager = manager;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        _hwnd = WindowNative.GetWindowHandle(this);
        var scale = NativeMethods.GetDpiForWindow(_hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(380 * scale), (int)(600 * scale)));

        _manager.Notes.CollectionChanged += (_, _) => ApplyFilter();
        ApplyFilter();

        // O Rabisco vive só na área de notificação: sem botão na barra de tarefas.
        AppWindow.IsShownInSwitchers = false;

        _tray = new TrayIcon(_hwnd, "Rabisco") { IsStartupEnabled = () => StartupRegistration.IsEnabled };
        _tray.CommandInvoked += OnTrayCommand;
        _subclass = new WindowSubclass(_hwnd, OnWindowMessage);
        _tray.Add();
        RegisterHotKeys();

        RootGrid.Loaded += (_, _) => ApplyTitleBarColors();
        RootGrid.ActualThemeChanged += (_, _) => ApplyTitleBarColors();
        AppWindow.Closing += OnClosing;
        Closed += OnClosed;
    }

    public ObservableCollection<Note> FilteredNotes { get; } = new();

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
        NativeMethods.SetForegroundWindow(_hwnd);
    }

    private void ApplyTitleBarColors() =>
        TitleBarColors.Apply(AppWindow.TitleBar, RootGrid.ActualTheme == ElementTheme.Dark);

    private void ApplyFilter()
    {
        var matches = _manager.Notes.Where(Matches).ToList();
        if (!matches.SequenceEqual(FilteredNotes))
        {
            FilteredNotes.Clear();
            foreach (var note in matches)
            {
                FilteredNotes.Add(note);
            }
        }

        EmptyState.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Text = _filter.Length == 0
            ? "Nenhuma nota ainda.\nClique em + para começar a rabiscar."
            : "Nenhuma nota encontrada.";
    }

    // Busca sem diferenciar maiúsculas nem acentos ("acao" encontra "Ação").
    private bool Matches(Note note) =>
        _filter.Length == 0 ||
        CultureInfo.CurrentCulture.CompareInfo.IndexOf(
            note.Content, _filter, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _filter = sender.Text.Trim();
        ApplyFilter();
    }

    private void NewNote_Click(object sender, RoutedEventArgs e) => _manager.Create();

    private void NotesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Note note)
        {
            _manager.Open(note);
        }
    }

    private void OpenNote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Note note)
        {
            _manager.Open(note);
        }
    }

    private async void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Note note)
        {
            return;
        }

        if (!note.IsEmpty)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = "Excluir nota?",
                Content = "Esta nota será apagada permanentemente.",
                PrimaryButtonText = "Excluir",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _manager.Delete(note);
    }

    private void Quit_Click(object sender, RoutedEventArgs e) => App.Current.Quit();

    private void RegisterHotKeys()
    {
        const uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;

        // Se outro programa já usa o atalho, o registro falha e o Rabisco segue sem ele.
        NativeMethods.RegisterHotKey(_hwnd, HotKeyNewNote, modifiers, 'N');
        NativeMethods.RegisterHotKey(_hwnd, HotKeyShowList, modifiers, 'R');
    }

    private void OnTrayCommand(int command)
    {
        // Fora do loop de mensagens do menu/ícone.
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (command)
            {
                case TrayIcon.CommandShowList:
                    ShowAndActivate();
                    break;
                case TrayIcon.CommandNewNote:
                    _manager.Create();
                    break;
                case TrayIcon.CommandToggleStartup:
                    StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled);
                    break;
                case TrayIcon.CommandQuit:
                    App.Current.Quit();
                    break;
            }
        });
    }

    private nint? OnWindowMessage(uint message, nuint wParam, nint lParam)
    {
        if (_tray.HandleMessage(message, lParam))
        {
            return 0;
        }

        switch (message)
        {
            case NativeMethods.WM_HOTKEY:
                var id = (int)wParam;
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (id == HotKeyNewNote)
                    {
                        _manager.Create();
                    }
                    else if (id == HotKeyShowList)
                    {
                        ShowAndActivate();
                    }
                });
                return 0;

            case NativeMethods.WM_QUERYENDSESSION:
                // Logoff/desligamento: salva tudo e mantém as notas abertas para a próxima sessão.
                _manager.BeginShutdown();
                break;

            case NativeMethods.WM_ENDSESSION when wParam == 0:
                // Outro app cancelou o desligamento.
                _manager.CancelShutdown();
                break;
        }

        return null;
    }

    // Fechar a lista com notas abertas só a esconde; as notas continuam na tela.
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_manager.HasOpenNotes && !_manager.IsShuttingDown)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        NativeMethods.UnregisterHotKey(_hwnd, HotKeyNewNote);
        NativeMethods.UnregisterHotKey(_hwnd, HotKeyShowList);
        _tray.Dispose();
        _subclass.Dispose();
    }
}
