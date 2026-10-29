using Microsoft.Win32;

namespace Rabisco.Services;

/// <summary>
/// Início com o Windows via HKCU\...\Run (sem instalador, sem admin).
/// O valor guarda o caminho do exe atual; se a pasta portátil for movida, <see cref="Refresh"/> corrige.
/// </summary>
public static class StartupRegistration
{
    /// <summary>Argumento usado no início automático: sobe só na bandeja, sem abrir a lista.</summary>
    public const string BackgroundArgument = "--background";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Rabisco";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && CurrentCommand() is { } command)
        {
            key.SetValue(ValueName, command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    // Reaponta o registro para o exe atual se o app foi movido de pasta.
    public static void Refresh()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string registered && CurrentCommand() is { } command && registered != command)
        {
            key.SetValue(ValueName, command);
        }
    }

    private static string? CurrentCommand() =>
        Environment.ProcessPath is { } path ? $"\"{path}\" {BackgroundArgument}" : null;
}
