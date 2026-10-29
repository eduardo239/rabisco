using System.Collections.ObjectModel;
using Rabisco.Data;
using Rabisco.Models;

namespace Rabisco.Services;

/// <summary>
/// Fonte única das notas: mantém a coleção em memória, persiste no SQLite
/// e controla qual nota tem janela aberta.
/// </summary>
public sealed class NoteManager
{
    private const int CascadeOffset = 40;

    private readonly NoteRepository _repository;
    private readonly Dictionary<string, NoteWindow> _windows = new();

    public NoteManager(NoteRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Todas as notas, da editada mais recentemente para a mais antiga.</summary>
    public ObservableCollection<Note> Notes { get; } = new();

    public bool HasOpenNotes => _windows.Count > 0;

    /// <summary>
    /// Durante o encerramento (Sair, logoff, desligamento) as janelas fecham sem marcar
    /// as notas como fechadas, para que reabram na próxima execução.
    /// </summary>
    public bool IsShuttingDown { get; private set; }

    public event EventHandler? AllNotesClosed;

    public void Load()
    {
        foreach (var note in _repository.GetAll())
        {
            Notes.Add(note);
        }
    }

    public void RestoreOpenNotes()
    {
        foreach (var note in Notes.Where(n => n.IsOpen).ToList())
        {
            Open(note);
        }
    }

    public Note Create(string content = "", Note? near = null)
    {
        var now = DateTimeOffset.Now;
        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            UpdatedAt = now,
            Content = content,
            Color = near?.Color ?? NoteColor.Yellow,
        };

        if (near is { X: int x, Y: int y })
        {
            note.X = x + CascadeOffset;
            note.Y = y + CascadeOffset;
            note.Width = near.Width;
            note.Height = near.Height;
        }

        Notes.Insert(0, note);
        Open(note);
        return note;
    }

    public void Open(Note note)
    {
        if (_windows.TryGetValue(note.Id, out var existing))
        {
            existing.BringToFront();
            return;
        }

        note.IsOpen = true;
        _repository.Upsert(note);

        var window = new NoteWindow(note, this);
        _windows[note.Id] = window;
        window.Activate();
    }

    /// <summary>Salva uma alteração de conteúdo/cor e move a nota para o topo da lista.</summary>
    public void SaveContent(Note note)
    {
        var index = Notes.IndexOf(note);
        if (index < 0)
        {
            return;
        }

        note.UpdatedAt = DateTimeOffset.Now;
        _repository.Upsert(note);
        if (index > 0)
        {
            Notes.Move(index, 0);
        }
    }

    /// <summary>Salva posição, tamanho ou fixação sem alterar a data de edição.</summary>
    public void SaveLayout(Note note)
    {
        if (Notes.Contains(note))
        {
            _repository.Upsert(note);
        }
    }

    public void Delete(Note note)
    {
        _repository.Delete(note.Id);
        Notes.Remove(note);

        if (_windows.TryGetValue(note.Id, out var window))
        {
            window.Close();
        }
    }

    public void BeginShutdown()
    {
        if (IsShuttingDown)
        {
            return;
        }

        foreach (var window in _windows.Values.ToList())
        {
            window.FlushPendingChanges();
        }

        IsShuttingDown = true;
    }

    public void CancelShutdown() => IsShuttingDown = false;

    internal void OnWindowClosed(NoteWindow window)
    {
        var note = window.Note;
        _windows.Remove(note.Id);

        if (!IsShuttingDown && Notes.Contains(note))
        {
            if (note.IsEmpty)
            {
                // Nota fechada sem conteúdo não vale a pena guardar.
                _repository.Delete(note.Id);
                Notes.Remove(note);
            }
            else
            {
                note.IsOpen = false;
                _repository.Upsert(note);
            }
        }

        if (_windows.Count == 0 && !IsShuttingDown)
        {
            AllNotesClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}
