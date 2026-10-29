using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Rabisco.Models;
using Windows.UI;

namespace Rabisco.Theming;

public static class NotePalette
{
    private sealed record Swatch(
        string Name,
        Color Accent,
        Color LightBody,
        Color LightHeader,
        Color DarkBody,
        Color DarkHeader);

    private static readonly Dictionary<NoteColor, Swatch> Swatches = new()
    {
        [NoteColor.Yellow] = new("Manteiga", Hex(0xE9B44C), Hex(0xFFF0C2), Hex(0xFAE29A), Hex(0x3B3220), Hex(0x5C4A1C)),
        [NoteColor.Green]  = new("Sálvia",   Hex(0x8FBF9F), Hex(0xE0EFE3), Hex(0xC8E2CE), Hex(0x24332A), Hex(0x2F5240)),
        [NoteColor.Pink]   = new("Rosé",     Hex(0xE67E9A), Hex(0xFBDDE5), Hex(0xF6C4D2), Hex(0x3A252C), Hex(0x612F42)),
        [NoteColor.Purple] = new("Lavanda",  Hex(0x9B8CF0), Hex(0xE8E3FF), Hex(0xD3CBFA), Hex(0x2D2943), Hex(0x4A3F80)),
        [NoteColor.Blue]   = new("Oceano",   Hex(0x5FB4D4), Hex(0xDAEFF7), Hex(0xBFE3F0), Hex(0x213440), Hex(0x275A70)),
        [NoteColor.Gray]   = new("Areia",    Hex(0xBFA990), Hex(0xF2EBE1), Hex(0xE5D9C8), Hex(0x2E2A26), Hex(0x4A4137)),
    };

    private static readonly Dictionary<NoteColor, SolidColorBrush> AccentBrushes = new();

    public static string DisplayName(NoteColor color) => Swatches[color].Name;

    public static Color Body(NoteColor color, bool dark) =>
        dark ? Swatches[color].DarkBody : Swatches[color].LightBody;

    public static Color Header(NoteColor color, bool dark) =>
        dark ? Swatches[color].DarkHeader : Swatches[color].LightHeader;

    // Cor do texto: tinta quente em vez de preto/branco puro.
    public static Color Ink(bool dark) => dark ? Hex(0xF3EEE6) : Hex(0x2B2622);

    // Usada via x:Bind na lista de notas e no seletor de cor.
    public static SolidColorBrush AccentBrush(NoteColor color)
    {
        if (!AccentBrushes.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush(Swatches[color].Accent);
            AccentBrushes[color] = brush;
        }

        return brush;
    }

    private static Color Hex(uint rgb) =>
        ColorHelper.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
