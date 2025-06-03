using System.Data;
using System.Data.SQLite;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Data;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class MonitoredFolderService
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<MonitoredFolderService>? _logger;

    public MonitoredFolderService(DatabaseService databaseService, ILogger<MonitoredFolderService>? logger = null)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<List<MonitoredFolder>> GetAllFoldersAsync()
    {
        var folders = new List<MonitoredFolder>();

        try
        {
            using var connection = _databaseService.GetConnection();
            await connection.OpenAsync();

            var sql = "SELECT * FROM MonitoredFolders ORDER BY CreatedAt DESC";
            using var command = new SQLiteCommand(sql, connection);
            using var reader = (System.Data.SQLite.SQLiteDataReader)await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                folders.Add(MapReaderToFolder(reader));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get monitored folders");
        }

        return folders;
    }

    public async Task<MonitoredFolder?> GetFolderByIdAsync(int id)
    {
        try
        {
            using var connection = _databaseService.GetConnection();
            await connection.OpenAsync();

            var sql = "SELECT * FROM MonitoredFolders WHERE Id = @Id";
            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            using var reader = (System.Data.SQLite.SQLiteDataReader)await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToFolder(reader);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to get folder by ID: {id}");
        }

        return null;
    }

    public async Task<int> AddFolderAsync(MonitoredFolder folder)
    {
        try
        {
            var sql = @"INSERT INTO MonitoredFolders 
                       (FolderPath, IsActive, FilePattern, IncludeSubfolders, CreatedAt, PostPrintAction, CustomMoveFolder)
                       VALUES (@FolderPath, @IsActive, @FilePattern, @IncludeSubfolders, @CreatedAt, @PostPrintAction, @CustomMoveFolder);
                       SELECT last_insert_rowid();";

            var result = await _databaseService.ExecuteScalarAsync(sql,
                new SQLiteParameter("@FolderPath", folder.FolderPath),
                new SQLiteParameter("@IsActive", folder.IsActive),
                new SQLiteParameter("@FilePattern", folder.FilePattern),
                new SQLiteParameter("@IncludeSubfolders", folder.IncludeSubfolders),
                new SQLiteParameter("@CreatedAt", folder.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@PostPrintAction", (int)folder.PostPrintAction),
                new SQLiteParameter("@CustomMoveFolder", folder.CustomMoveFolder ?? (object)DBNull.Value));

            var folderId = Convert.ToInt32(result);
            _logger?.LogInformation($"Added monitored folder: {folder.FolderPath} (ID: {folderId})");

            return folderId;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to add monitored folder: {folder.FolderPath}");
            return -1;
        }
    }

    public async Task<bool> UpdateFolderAsync(MonitoredFolder folder)
    {
        try
        {
            var sql = @"UPDATE MonitoredFolders 
                       SET FolderPath = @FolderPath, IsActive = @IsActive, FilePattern = @FilePattern, 
                           IncludeSubfolders = @IncludeSubfolders, PostPrintAction = @PostPrintAction,
                           CustomMoveFolder = @CustomMoveFolder, LastActivity = @LastActivity
                       WHERE Id = @Id";

            var rowsAffected = await _databaseService.ExecuteNonQueryAsync(sql,
                new SQLiteParameter("@FolderPath", folder.FolderPath),
                new SQLiteParameter("@IsActive", folder.IsActive),
                new SQLiteParameter("@FilePattern", folder.FilePattern),
                new SQLiteParameter("@IncludeSubfolders", folder.IncludeSubfolders),
                new SQLiteParameter("@PostPrintAction", (int)folder.PostPrintAction),
                new SQLiteParameter("@CustomMoveFolder", folder.CustomMoveFolder ?? (object)DBNull.Value),
                new SQLiteParameter("@LastActivity", folder.LastActivity?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value),
                new SQLiteParameter("@Id", folder.Id));

            var success = rowsAffected > 0;
            if (success)
            {
                _logger?.LogInformation($"Updated monitored folder: {folder.FolderPath}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to update monitored folder: {folder.Id}");
            return false;
        }
    }

    public async Task<bool> DeleteFolderAsync(int id)
    {
        try
        {
            var sql = "DELETE FROM MonitoredFolders WHERE Id = @Id";
            var rowsAffected = await _databaseService.ExecuteNonQueryAsync(sql,
                new SQLiteParameter("@Id", id));

            var success = rowsAffected > 0;
            if (success)
            {
                _logger?.LogInformation($"Deleted monitored folder: {id}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to delete monitored folder: {id}");
            return false;
        }
    }

    public async Task<bool> SetFolderActiveStatusAsync(int id, bool isActive)
    {
        try
        {
            var sql = "UPDATE MonitoredFolders SET IsActive = @IsActive WHERE Id = @Id";
            var rowsAffected = await _databaseService.ExecuteNonQueryAsync(sql,
                new SQLiteParameter("@IsActive", isActive),
                new SQLiteParameter("@Id", id));

            var success = rowsAffected > 0;
            if (success)
            {
                _logger?.LogInformation($"Set folder {id} active status to: {isActive}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to set folder active status: {id}");
            return false;
        }
    }

    private static MonitoredFolder MapReaderToFolder(SQLiteDataReader reader)
    {
        return new MonitoredFolder
        {
            Id = reader.GetInt32("Id"),
            FolderPath = reader.GetString("FolderPath"),
            IsActive = reader.GetBoolean("IsActive"),
            FilePattern = reader.GetString("FilePattern"),
            IncludeSubfolders = reader.GetBoolean("IncludeSubfolders"),
            CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
            LastActivity = reader.IsDBNull("LastActivity") ? null : DateTime.Parse(reader.GetString("LastActivity")),
            PostPrintAction = (PostPrintAction)reader.GetInt32("PostPrintAction"),
            CustomMoveFolder = reader.IsDBNull("CustomMoveFolder") ? null : reader.GetString("CustomMoveFolder")
        };
    }
}