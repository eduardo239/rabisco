using System.Text.Json;
using Rabisco.Theming;

namespace Rabisco.Services;

/// <summary>Preferências globais, guardadas em settings.json ao lado do banco de notas.</summary>
public static class AppSettings
{
    private sealed record Data(string? Font, Dictionary<string, double>? FontSizes);

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Rabisco",
        "settings.json");

    private static readonly Data Loaded = Load();
    private static string _fontKey = NoteFonts.Get(Loaded.Font).Key;
    private static readonly Dictionary<string, double> FontSizes = Loaded.FontSizes ?? new();

    /// <summary>Disparado quando a fonte ativa ou o tamanho dela muda.</summary>
    public static event EventHandler? FontChanged;

    public static string FontKey
    {
        get => _fontKey;
        set
        {
            if (_fontKey == value)
            {
                return;
            }

            _fontKey = value;
            Save();
            FontChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Tamanho de uma fonte; cada fonte guarda o seu.</summary>
    public static double GetFontSize(string key) =>
        FontSizes.TryGetValue(key, out var size)
            ? Math.Clamp(size, NoteFonts.MinSize, NoteFonts.MaxSize)
            : NoteFonts.Get(key).DefaultSize;

    public static void SetFontSize(string key, double size)
    {
        size = Math.Clamp(size, NoteFonts.MinSize, NoteFonts.MaxSize);
        if (size == GetFontSize(key))
        {
            return;
        }

        FontSizes[key] = size;
        Save();
        FontChanged?.Invoke(null, EventArgs.Empty);
    }

    private static Data Load()
    {
        try
        {
            return JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data(null, null);
        }
        catch (Exception)
        {
            // Sem arquivo ou arquivo inválido: usa os padrões.
            return new Data(null, null);
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Data(_fontKey, FontSizes)));
        }
        catch (IOException)
        {
            // Preferência não salva não deve derrubar o app.
        }
    }
}
