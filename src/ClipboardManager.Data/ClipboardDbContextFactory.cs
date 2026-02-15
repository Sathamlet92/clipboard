using Microsoft.Data.Sqlite;
using System.Threading;

namespace ClipboardManager.Data;

/// <summary>
/// Factory para crear conexiones SQLite thread-safe.
/// Usa una conexión compartida con locks para evitar problemas de WAL.
/// </summary>
public class ClipboardDbContextFactory : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _sharedConnection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;
    private bool _initialized;
    private readonly object _initLock = new();

    public ClipboardDbContextFactory(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        
        _connectionString = builder.ToString();
    }

    /// <summary>
    /// Obtiene la conexión compartida. Usar con await AcquireLockAsync() / ReleaseLock().
    /// </summary>
    public async Task<SqliteConnection> GetConnectionAsync()
    {
        await _connectionLock.WaitAsync();
        
        try
        {
            EnsureInitialized();
            
            if (_sharedConnection == null || _sharedConnection.State != System.Data.ConnectionState.Open)
            {
                _sharedConnection?.Dispose();
                _sharedConnection = new SqliteConnection(_connectionString);
                _sharedConnection.Open();
                
                // Configurar PRAGMAs
                using var cmd = _sharedConnection.CreateCommand();
                cmd.CommandText = "PRAGMA busy_timeout = 5000";
                cmd.ExecuteNonQuery();
            }
            
            return _sharedConnection;
        }
        catch
        {
            _connectionLock.Release();
            throw;
        }
    }

    public void ReleaseConnection()
    {
        _connectionLock.Release();
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            // Inicializar schema una sola vez
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            InitializeDatabase(connection);
            _initialized = true;
        }
    }

    private void InitializeDatabase(SqliteConnection connection)
    {
        Console.WriteLine("🔧 Inicializando base de datos...");
        
        // Configurar PRAGMAs
        ExecutePragmas(connection);
        
        // Cargar y ejecutar schema
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schema.sql");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException("Schema.sql not found", schemaPath);
        }

        var schema = File.ReadAllText(schemaPath);
        Console.WriteLine($"📄 Schema cargado: {schema.Length} caracteres");
        ExecuteSchema(connection, schema);
        Console.WriteLine("✅ Base de datos inicializada");
    }

    private void ExecutePragmas(SqliteConnection connection)
    {
        var pragmas = new[]
        {
            "PRAGMA journal_mode = WAL",
            "PRAGMA synchronous = NORMAL",
            "PRAGMA cache_size = -64000",
            "PRAGMA temp_store = MEMORY",
            "PRAGMA foreign_keys = ON",
            "PRAGMA busy_timeout = 5000"
        };

        foreach (var pragma in pragmas)
        {
            using var command = connection.CreateCommand();
            command.CommandText = pragma;
            command.ExecuteNonQuery();
        }
    }

    private void ExecuteSchema(SqliteConnection connection, string schema)
    {
        Console.WriteLine("🔨 Ejecutando schema SQL...");
        
        // Dividir en statements individuales
        var statements = new List<string>();
        var currentStatement = new System.Text.StringBuilder();
        var lines = schema.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var inTrigger = false;
        var triggerDepth = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Ignorar comentarios y PRAGMAs (los ejecutamos por separado)
            if (trimmed.StartsWith("--") || trimmed.StartsWith("PRAGMA"))
                continue;
            
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            currentStatement.AppendLine(trimmed);

            // Detectar triggers
            if (trimmed.Contains("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase))
            {
                inTrigger = true;
                triggerDepth = 0;
            }

            // Contar BEGIN/END
            if (trimmed.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                triggerDepth++;
            }

            if (trimmed.Contains("END;", StringComparison.OrdinalIgnoreCase))
            {
                triggerDepth--;
                if (inTrigger && triggerDepth == 0)
                {
                    statements.Add(currentStatement.ToString());
                    currentStatement.Clear();
                    inTrigger = false;
                }
            }
            else if (trimmed.EndsWith(";") && !inTrigger)
            {
                statements.Add(currentStatement.ToString());
                currentStatement.Clear();
            }
        }

        // Ejecutar cada statement
        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement))
                continue;

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = statement;
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"❌ Error ejecutando statement:");
                Console.WriteLine(statement.Substring(0, Math.Min(200, statement.Length)));
                Console.WriteLine($"SQLite Error Code: {ex.SqliteErrorCode}, Message: {ex.Message}");
                throw;
            }
        }
        
        Console.WriteLine($"✅ Schema ejecutado: {statements.Count} statements");
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _sharedConnection?.Dispose();
        _connectionLock.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
