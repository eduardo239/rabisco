namespace Rabisco.Interop;

/// <summary>
/// Intercepta mensagens Win32 de uma janela WinUI (atalhos globais, fim de sessão).
/// O handler devolve um resultado para tratar a mensagem, ou null para seguir o fluxo padrão.
/// </summary>
internal sealed class WindowSubclass : IDisposable
{
    private const nuint SubclassId = 1;

    private readonly nint _hwnd;
    private readonly Func<uint, nuint, nint, nint?> _handler;
    // Mantém o delegate vivo enquanto o subclass estiver instalado.
    private readonly NativeMethods.SubclassProc _proc;
    private bool _disposed;

    public WindowSubclass(nint hwnd, Func<uint, nuint, nint, nint?> handler)
    {
        _hwnd = hwnd;
        _handler = handler;
        _proc = WindowProc;

        if (!NativeMethods.SetWindowSubclass(hwnd, _proc, SubclassId, 0))
        {
            throw new InvalidOperationException("Não foi possível instalar o subclass da janela.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeMethods.RemoveWindowSubclass(_hwnd, _proc, SubclassId);
    }

    private nint WindowProc(nint hWnd, uint msg, nuint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData) =>
        _handler(msg, wParam, lParam) ?? NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
}
