using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Rabisco.Interop;

namespace Rabisco;

public static class Program
{
    private const string InstanceKey = "Rabisco.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (TryRedirectToRunningInstance())
        {
            return 0;
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
        return 0;
    }

    // Se o Rabisco já estiver aberto, entrega a ativação para ele (que mostra a lista de notas)
    // e encerra este processo. Evita dois processos escrevendo no mesmo banco.
    private static bool TryRedirectToRunningInstance()
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (mainInstance.IsCurrent)
        {
            return false;
        }

        NativeMethods.AllowSetForegroundWindow(mainInstance.ProcessId);
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        Task.Run(() => mainInstance.RedirectActivationToAsync(activationArgs).AsTask()).Wait();
        return true;
    }
}
