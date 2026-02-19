using Microsoft.Data.Sqlite;
using System.Reflection;

namespace ClipboardManager.Data;

public class ClipboardDbContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public ClipboardDbContext(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        
        InitializeDatabase();
    }

    public SqliteConnection Connection => _connection;

    private void InitializeDatabase()
    {
        // Primero configurar PRAGMAs
        ExecutePragmas();
        
        // Leer el schema SQL desde el archivo
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schema.sql");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException("Schema.sql not found", schemaPath);
        }

        var schema = File.ReadAllText(schemaPath);
        ExecuteSchema(schema);
    }

    private void ExecutePragmas()
    {
        var pragmas = new[]
        {
            "PRAGMA journal_mode = WAL",
            "PRAGMA synchronous = NORMAL",
            "PRAGMA cache_size = -64000",
            "PRAGMA temp_store = MEMORY",
            "PRAGMA foreign_keys = ON"
        };

        foreach (var pragma in pragmas)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = pragma;
            command.ExecuteNonQuery();
        }
    }

    private void ExecuteSchema(string schema)
    {
        // Remover comentarios
        var lines = schema.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line) && 
                          !line.TrimStart().StartsWith("--") &&
                          !line.TrimStart().StartsWith("PRAGMA"))
            .Select(line => line.Trim())
            .ToArray();

        var cleanSchema = string.Join(" ", lines);

        // Dividir por comandos, pero respetando BEGIN...END
        var commands = new List<string>();
        var currentCommand = new System.Text.StringBuilder();
        var inTrigger = false;

        foreach (var line in lines)
        {
            currentCommand.Append(line).Append(" ");

            if (line.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                inTrigger = true;
            }

            if (line.Contains("END", StringComparison.OrdinalIgnoreCase) && inTrigger)
            {
                inTrigger = false;
                commands.Add(currentCommand.ToString().Trim());
                currentCommand.Clear();
            }
            else if (line.EndsWith(";") && !inTrigger)
            {
                commands.Add(currentCommand.ToString().Trim());
                currentCommand.Clear();
            }
        }

        // Agregar último comando si existe
        if (currentCommand.Length > 0)
        {
            commands.Add(currentCommand.ToString().Trim());
        }

        // Ejecutar cada comando
        foreach (var commandText in commands)
        {
            var trimmed = commandText.Trim().TrimEnd(';');
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            using var command = _connection.CreateCommand();
            command.CommandText = trimmed;
            
            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error executing SQL: {trimmed}");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _connection?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
