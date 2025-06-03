using System.Data.SQLite;
using Microsoft.Extensions.Logging;

namespace PrintHero.Core.Data;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService>? _logger;

    public DatabaseService(ILogger<DatabaseService>? logger = null)
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PrintHero");

        Directory.CreateDirectory(appDataPath);

        var dbPath = Path.Combine(appDataPath, "printhero.db");
        _connectionString = $"Data Source={dbPath};Version=3;";
        _logger = logger;

        InitializeDatabase();
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(_connectionString);
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, params SQLiteParameter[] parameters)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        using var command = new SQLiteCommand(sql, connection);
        command.Parameters.AddRange(parameters);

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ExecuteScalarAsync(string sql, params SQLiteParameter[] parameters)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        using var command = new SQLiteCommand(sql, connection);
        command.Parameters.AddRange(parameters);

        return await command.ExecuteScalarAsync();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();

            var createTables = @"
                CREATE TABLE IF NOT EXISTS MonitoredFolders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FolderPath TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    FilePattern TEXT NOT NULL DEFAULT '*.pdf',
                    IncludeSubfolders INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    LastActivity TEXT,
                    PostPrintAction INTEGER NOT NULL DEFAULT 0,
                    CustomMoveFolder TEXT
                );

                CREATE TABLE IF NOT EXISTS PrintJobs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    PrinterName TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    PrintedAt TEXT,
                    Status INTEGER NOT NULL DEFAULT 0,
                    ErrorMessage TEXT,
                    MonitoredFolderId INTEGER NOT NULL,
                    FileSizeBytes INTEGER NOT NULL DEFAULT 0,
                    PostPrintAction INTEGER NOT NULL DEFAULT 0,
                    MovedToPath TEXT,
                    FOREIGN KEY (MonitoredFolderId) REFERENCES MonitoredFolders(Id)
                );

                CREATE TABLE IF NOT EXISTS License (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    LicenseKey TEXT NOT NULL,
                    MachineFingerprint TEXT NOT NULL,
                    CustomerName TEXT,
                    ActivationDate TEXT NOT NULL,
                    ExpiryDate TEXT,
                    LicenseType INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    MaxDevices INTEGER NOT NULL DEFAULT 1,
                    Features TEXT
                );

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );";

            using var command = new SQLiteCommand(createTables, connection);
            command.ExecuteNonQuery();

            _logger?.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database");
            throw;
        }
    }
}
