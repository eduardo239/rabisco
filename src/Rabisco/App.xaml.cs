using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Rabisco.Data;
using Rabisco.Services;

namespace Rabisco;

public partial class App : Application
{
    private const string WelcomeText =
        "Bem-vindo ao Rabisco! ✏️\n\n" +
        "• Ctrl+N cria uma nova nota\n" +
        "• Ctrl+Alt+N cria uma nota de qualquer lugar do Windows\n" +
        "• Ctrl+Alt+R abre a lista de notas\n" +
        "• O alfinete mantém a nota sempre no topo\n" +
        "• Tudo é salvo automaticamente";

    private NoteManager? _notes;
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
    }

    public static new App Current => (App)Application.Current;

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var repository = new NoteRepository(NoteRepository.DefaultDatabasePath);
        repository.Initialize();

        _notes = new NoteManager(repository);
        _notes.Load();

        StartupRegistration.Refresh();

        _mainWindow = new MainWindow(_notes);
        // No início automático com o Windows o app sobe só na bandeja, com as notas abertas.
        var background = Environment.GetCommandLineArgs().Contains(StartupRegistration.BackgroundArgument);
        if (!background)
        {
            _mainWindow.Activate();
        }

        if (_notes.Notes.Count == 0)
        {
            _notes.Create(WelcomeText);
        }
        else
        {
            _notes.RestoreOpenNotes();
        }

        // Uma segunda execução do Rabisco é redirecionada para cá (ver Program.cs).
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        AppInstance.GetCurrent().Activated += (_, _) => dispatcher.TryEnqueue(ShowMainWindow);
    }

    public void ShowMainWindow() => _mainWindow?.ShowAndActivate();

    // Encerra o app mantendo as notas abertas marcadas para reabrir na próxima execução.
    public void Quit()
    {
        _notes?.BeginShutdown();
        Exit();
    }
}
