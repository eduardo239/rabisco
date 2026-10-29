using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.UI;

namespace Rabisco.Theming;

/// <summary>Deixa os botões de sistema (fechar etc.) transparentes e legíveis no tema atual.</summary>
public static class TitleBarColors
{
    public static void Apply(AppWindowTitleBar titleBar, bool dark)
    {
        var foreground = dark ? Colors.White : Colors.Black;

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = WithAlpha(foreground, 0x80);
        titleBar.ButtonHoverBackgroundColor = WithAlpha(foreground, 0x18);
        titleBar.ButtonPressedBackgroundColor = WithAlpha(foreground, 0x30);
    }

    private static Color WithAlpha(Color color, byte alpha) =>
        ColorHelper.FromArgb(alpha, color.R, color.G, color.B);
}
