using System.Runtime.InteropServices;

namespace Rabisco.Interop;

/// <summary>
/// Ícone na área de notificação (perto do relógio). Usa a janela informada para receber
/// os cliques; o chamador repassa as mensagens para <see cref="HandleMessage"/>.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    public const uint CallbackMessage = NativeMethods.WM_APP + 1;

    public const int CommandShowList = 1;
    public const int CommandNewNote = 2;
    public const int CommandQuit = 3;
    public const int CommandToggleStartup = 4;

    private const uint IconId = 1;

    private readonly nint _hwnd;
    private readonly nint _icon;
    private readonly string _tooltip;
    private readonly uint _taskbarCreatedMessage;
    private bool _added;

    public TrayIcon(nint hwnd, string tooltip)
    {
        _hwnd = hwnd;
        _tooltip = tooltip;
        _icon = LoadAppIcon();
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    }

    /// <summary>Disparado com o comando escolhido no menu, ou ao clicar no ícone (<see cref="CommandShowList"/>).</summary>
    public event Action<int>? CommandInvoked;

    /// <summary>Informa se "Iniciar com o Windows" aparece marcado no menu.</summary>
    public Func<bool>? IsStartupEnabled { get; set; }

    public void Add()
    {
        var data = CreateData();
        _added = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
    }

    public void Dispose()
    {
        if (!_added)
        {
            return;
        }

        _added = false;
        var data = CreateData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
    }

    public bool HandleMessage(uint message, nint lParam)
    {
        if (message == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            // O Explorer reiniciou e perdeu os ícones.
            Add();
            return true;
        }

        if (message != CallbackMessage)
        {
            return false;
        }

        switch ((uint)lParam)
        {
            case NativeMethods.WM_LBUTTONUP:
                CommandInvoked?.Invoke(CommandShowList);
                break;
            case NativeMethods.WM_RBUTTONUP:
                ShowMenu();
                break;
        }

        return true;
    }

    private void ShowMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandShowList, "Abrir lista de notas");
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandNewNote, "Nova nota");
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, null);
            var startupFlags = NativeMethods.MF_STRING | (IsStartupEnabled?.Invoke() == true ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenu(menu, startupFlags, CommandToggleStartup, "Iniciar com o Windows");
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandQuit, "Sair");

            NativeMethods.GetCursorPos(out var point);
            // Sem isso o menu não fecha ao clicar fora dele.
            NativeMethods.SetForegroundWindow(_hwnd);
            var command = NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY | NativeMethods.TPM_BOTTOMALIGN,
                point.X, point.Y, 0, _hwnd, 0);
            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_NULL, 0, 0);

            if (command != 0)
            {
                CommandInvoked?.Invoke(command);
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private NativeMethods.NotifyIconData CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = _tooltip,
    };

    // Reaproveita o ícone do executável; se não houver, usa o ícone padrão de aplicativo.
    private static nint LoadAppIcon()
    {
        var path = Environment.ProcessPath;
        if (path is not null)
        {
            var icons = new nint[1];
            if (NativeMethods.ExtractIconEx(path, 0, null, icons, 1) > 0 && icons[0] != 0)
            {
                return icons[0];
            }
        }

        return NativeMethods.LoadIcon(0, NativeMethods.IDI_APPLICATION);
    }
}
