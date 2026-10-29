using System.Globalization;
using Microsoft.Data.Sqlite;
using Rabisco.Models;

namespace Rabisco.Data;

public sealed class NoteRepository
{
    private const int SchemaVersion = 1;

    private readonly string _connectionString;

    public NoteRepository(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public static string DefaultDatabasePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Rabisco",
        "rabisco.db");

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (version < 1)
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS notes (
                    id          TEXT PRIMARY KEY,
                    content     TEXT    NOT NULL DEFAULT '',
                    color       TEXT    NOT NULL DEFAULT 'Yellow',
                    x           INTEGER NULL,
                    y           INTEGER NULL,
                    width       INTEGER NOT NULL DEFAULT 0,
                    height      INTEGER NOT NULL DEFAULT 0,
                    is_open     INTEGER NOT NULL DEFAULT 1,
                    is_pinned   INTEGER NOT NULL DEFAULT 0,
                    created_at  TEXT    NOT NULL,
                    updated_at  TEXT    NOT NULL
                );
                PRAGMA user_version = {SchemaVersion};
                """;
            command.ExecuteNonQuery();
        }

        command.CommandText = "PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();
    }

    public List<Note> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, content, color, x, y, width, height, is_open, is_pinned, created_at, updated_at
            FROM notes
            ORDER BY updated_at DESC;
            """;

        var notes = new List<Note>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            notes.Add(new Note
            {
                Id = reader.GetString(0),
                Content = reader.GetString(1),
                Color = Enum.TryParse<NoteColor>(reader.GetString(2), out var color) ? color : NoteColor.Yellow,
                X = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Y = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Width = reader.GetInt32(5),
                Height = reader.GetInt32(6),
                IsOpen = reader.GetBoolean(7),
                IsPinned = reader.GetBoolean(8),
                CreatedAt = ParseDate(reader.GetString(9)),
                UpdatedAt = ParseDate(reader.GetString(10)),
            });
        }

        return notes;
    }

    public void Upsert(Note note)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO notes (id, content, color, x, y, width, height, is_open, is_pinned, created_at, updated_at)
            VALUES ($id, $content, $color, $x, $y, $width, $height, $isOpen, $isPinned, $createdAt, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                content    = excluded.content,
                color      = excluded.color,
                x          = excluded.x,
                y          = excluded.y,
                width      = excluded.width,
                height     = excluded.height,
                is_open    = excluded.is_open,
                is_pinned  = excluded.is_pinned,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", note.Id);
        command.Parameters.AddWithValue("$content", note.Content);
        command.Parameters.AddWithValue("$color", note.Color.ToString());
        command.Parameters.AddWithValue("$x", (object?)note.X ?? DBNull.Value);
        command.Parameters.AddWithValue("$y", (object?)note.Y ?? DBNull.Value);
        command.Parameters.AddWithValue("$width", note.Width);
        command.Parameters.AddWithValue("$height", note.Height);
        command.Parameters.AddWithValue("$isOpen", note.IsOpen);
        command.Parameters.AddWithValue("$isPinned", note.IsPinned);
        command.Parameters.AddWithValue("$createdAt", FormatDate(note.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", FormatDate(note.UpdatedAt));
        command.ExecuteNonQuery();
    }

    public void Delete(string id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM notes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    // Sempre em UTC, para que a ordenação por texto no SQLite seja cronológica.
    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
