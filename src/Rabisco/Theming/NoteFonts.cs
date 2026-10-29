using Microsoft.UI.Xaml.Media;

namespace Rabisco.Theming;

/// <summary>Fontes disponíveis para o texto das notas (Google Fonts, licença OFL, em Assets/Fonts).</summary>
public static class NoteFonts
{
    public sealed record Option(string Key, string Name, FontFamily Family, double DefaultSize);

    public const string DefaultKey = "Kalam";
    public const double MinSize = 10;
    public const double MaxSize = 48;

    // O tamanho padrão compensa o tamanho visual de cada fonte (Caveat é bem menor que Libre Baskerville).
    public static IReadOnlyList<Option> All { get; } =
    [
        Create("Kalam", "Kalam", "Kalam-Regular.ttf#Kalam", 18),
        Create("Caveat", "Caveat", "Caveat.ttf#Caveat", 22),
        Create("GeistMono", "Geist Mono", "GeistMono.ttf#Geist Mono", 15),
        Create("LibreBaskerville", "Libre Baskerville", "LibreBaskerville.ttf#Libre Baskerville", 15),
        Create("Nunito", "Nunito", "Nunito.ttf#Nunito", 17),
    ];

    public static Option Get(string? key) =>
        All.FirstOrDefault(option => option.Key == key) ?? All[0];

    // Se a fonte não carregar, o WinUI cai para Segoe UI.
    private static Option Create(string key, string name, string resource, double defaultSize) =>
        new(key, name, new FontFamily($"ms-appx:///Assets/Fonts/{resource}, Segoe UI"), defaultSize);
}
