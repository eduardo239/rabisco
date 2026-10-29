using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Rabisco.Models;

public sealed class Note : INotifyPropertyChanged
{
    private const int PreviewMaxLength = 280;

    private string _content = string.Empty;
    private NoteColor _color = NoteColor.Yellow;
    private DateTimeOffset _updatedAt;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Sem `required`/`init`: o gerador de XAML (XamlTypeInfo.g.cs) cria Note por
    // reflexão-like activators e não suporta esses modificadores (CS9035/CS8852).
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string Content
    {
        get => _content;
        set
        {
            if (SetField(ref _content, value))
            {
                OnPropertyChanged(nameof(Preview));
            }
        }
    }

    public NoteColor Color
    {
        get => _color;
        set => SetField(ref _color, value);
    }

    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        set
        {
            if (SetField(ref _updatedAt, value))
            {
                OnPropertyChanged(nameof(UpdatedLabel));
            }
        }
    }

    public bool IsOpen { get; set; }

    public bool IsPinned { get; set; }

    // Posição e tamanho da janela em pixels físicos. X/Y nulos = o Windows escolhe a posição.
    public int? X { get; set; }
    public int? Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(_content);

    public string Preview
    {
        get
        {
            // O TextBox do WinUI usa '\r' como quebra de linha.
            var text = _content.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length == 0)
            {
                return "Nota vazia";
            }

            return text.Length > PreviewMaxLength ? text[..PreviewMaxLength] + "…" : text;
        }
    }

    public string UpdatedLabel
    {
        get
        {
            var local = _updatedAt.ToLocalTime();
            var culture = CultureInfo.CurrentCulture;
            return local.Date == DateTime.Today
                ? local.ToString("t", culture)
                : local.ToString("d MMM", culture);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
