using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace PrintHero.Core.Data;

public class SqliteDatabaseService : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDatabaseService> _logger;
    private readonly object _lockObject = new();

    public SqliteDatabaseService(ILogger<SqliteDatabaseService> logger)
    {
        _logger = logger;
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbDirectory = Path.Combine(appDataPath, "PrintHero");
        Directory.CreateDirectory(dbDirectory);
        
        var dbPath = Path.Combine(dbDirectory, "printhero.db");
        _connectionString = $"Data Source={dbPath}";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            CreateTables(connection);
            
            _logger.LogInformation("SQLite database initialized at: {DatabasePath}", 
                new SqliteConnectionStringBuilder(_connectionString).DataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite database");
            throw;
        }
    }

    private void CreateTables(SqliteConnection connection)
    {
        // Print Jobs table
        var createPrintJobsTable = @"
            CREATE TABLE IF NOT EXISTS PrintJobs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                FileSize INTEGER,
                FileType TEXT,
                PrinterName TEXT,
                Status INTEGER DEFAULT 0, -- 0=Pending, 1=Printing, 2=Completed, 3=Failed
                ErrorMessage TEXT,
                ProcessingStarted TEXT,
                ProcessingCompleted TEXT,
                MonitoredFolderId INTEGER,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            );";

        // Application Settings table (unified settings storage)
        var createAppSettingsTable = @"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Category TEXT NOT NULL DEFAULT 'General',
                Key TEXT NOT NULL,
                Value TEXT,
                DataType TEXT DEFAULT 'string',
                Description TEXT,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(Category, Key)
            );";

        // Monitored Folders table
        var createMonitoredFoldersTable = @"
            CREATE TABLE IF NOT EXISTS MonitoredFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderPath TEXT NOT NULL UNIQUE,
                FilePattern TEXT DEFAULT '*.*',
                IncludeSubfolders INTEGER DEFAULT 0,
                PostPrintAction INTEGER DEFAULT 0,
                CustomMoveFolder TEXT,
                IsActive INTEGER DEFAULT 1,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            );";

        // Execute table creation commands
        ExecuteNonQuery(connection, createPrintJobsTable);
        ExecuteNonQuery(connection, createAppSettingsTable);
        ExecuteNonQuery(connection, createMonitoredFoldersTable);

        // Create indexes for performance
        CreateIndexes(connection);
        
        // Insert default settings
        InsertDefaultSettings(connection);
        
    }

    private void CreateIndexes(SqliteConnection connection)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_print_jobs_created_at ON PrintJobs(CreatedAt);",
            "CREATE INDEX IF NOT EXISTS idx_print_jobs_status ON PrintJobs(Status);",
            "CREATE INDEX IF NOT EXISTS idx_print_jobs_printer_name ON PrintJobs(PrinterName);",
            "CREATE INDEX IF NOT EXISTS idx_app_settings_category_key ON AppSettings(Category, Key);",
            "CREATE INDEX IF NOT EXISTS idx_monitored_folders_active ON MonitoredFolders(IsActive);"
        };

        foreach (var index in indexes)
        {
            ExecuteNonQuery(connection, index);
        }
    }

    private void InsertDefaultSettings(SqliteConnection connection)
    {
        var defaultSettings = new[]
        {
            ("General", "DefaultPrinter", "", "string", "Default printer name"),
            ("General", "PaperSize", "A4", "string", "Default paper size"),
            ("General", "Orientation", "Portrait", "string", "Default page orientation"),
            ("General", "AutoStartService", "true", "boolean", "Auto-start service on login"),
            ("Statistics", "FilesProcessedToday", "0", "integer", "Files processed today"),
            ("Statistics", "PrintingErrors", "0", "integer", "Printing errors count"),
            ("Statistics", "LastResetDate", DateTime.Today.ToString("yyyy-MM-dd"), "date", "Last statistics reset date")
        };

        foreach (var (category, key, value, dataType, description) in defaultSettings)
        {
            var sql = @"INSERT OR IGNORE INTO AppSettings (Category, Key, Value, DataType, Description) 
                       VALUES (@Category, @Key, @Value, @DataType, @Description)";
            
            ExecuteNonQuery(connection, sql,
                new SqliteParameter("@Category", category),
                new SqliteParameter("@Key", key),
                new SqliteParameter("@Value", value),
                new SqliteParameter("@DataType", dataType),
                new SqliteParameter("@Description", description));
        }
    }


    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public void ExecuteNonQuery(string sql, params SqliteParameter[] parameters)
    {
        lock (_lockObject)
        {
            using var connection = GetConnection();
            connection.Open();
            ExecuteNonQuery(connection, sql, parameters);
        }
    }

    private void ExecuteNonQuery(SqliteConnection connection, string sql, params SqliteParameter[] parameters)
    {
        using var command = new SqliteCommand(sql, connection);
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }
        command.ExecuteNonQuery();
    }

    public T? ExecuteScalar<T>(string sql, params SqliteParameter[] parameters)
    {
        lock (_lockObject)
        {
            using var connection = GetConnection();
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            
            var result = command.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return default(T);
            
            return (T)Convert.ChangeType(result, typeof(T));
        }
    }

    public List<T> ExecuteQuery<T>(string sql, Func<SqliteDataReader, T> mapper, params SqliteParameter[] parameters)
    {
        var results = new List<T>();
        
        lock (_lockObject)
        {
            using var connection = GetConnection();
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(mapper(reader));
            }
        }
        
        return results;
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, params SqliteParameter[] parameters)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();
        using var command = new SqliteCommand(sql, connection);
        
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }
        
        var result = await command.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return default(T);
        
        return (T)Convert.ChangeType(result, typeof(T));
    }

    public async Task<List<T>> ExecuteQueryAsync<T>(string sql, Func<SqliteDataReader, T> mapper, params SqliteParameter[] parameters)
    {
        var results = new List<T>();
        
        using var connection = GetConnection();
        await connection.OpenAsync();
        using var command = new SqliteCommand(sql, connection);
        
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(mapper(reader));
        }
        
        return results;
    }

    public async Task ExecuteNonQueryAsync(string sql, params SqliteParameter[] parameters)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();
        using var command = new SqliteCommand(sql, connection);
        
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }
        
        await command.ExecuteNonQueryAsync();
    }

    public bool TestConnection()
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection test failed");
            return false;
        }
    }

    public void Dispose()
    {
        // SQLite connections are closed automatically when disposed
        // No persistent connections to clean up
    }
}