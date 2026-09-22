using Microsoft.Data.Sqlite;
using fuji_barcode.Models;

namespace fuji_barcode.Services;

public class BarcodeLookupService
{
    private readonly string _dbPath;
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS barcode_recipe_mappings (
            log_id      TEXT PRIMARY KEY,
            recipe_name TEXT NOT NULL,
            updated_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%f', 'now'))
        );
        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT
        );
        """;

    public BarcodeLookupService(string? dbPath = null)
    {
        _dbPath = !string.IsNullOrWhiteSpace(dbPath)
            ? dbPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "fuji-barcode",
                "barcode.db");
    }

    public async Task<string?> LookupRecipeAsync(string logId)
    {
        await using var conn = await OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT recipe_name FROM barcode_recipe_mappings WHERE log_id = @logId LIMIT 1";
        cmd.Parameters.AddWithValue("@logId", logId);

        var recipe = await cmd.ExecuteScalarAsync() as string;
        if (recipe != null)
            return recipe;

        cmd.CommandText = """
            SELECT recipe_name
            FROM barcode_recipe_mappings
            WHERE length(log_id) > 0
              AND (
                  (length(log_id) <= length(@scannedLogId)
                   AND substr(@scannedLogId, -length(log_id)) = log_id)
                  OR
                  (length(log_id) >= length(@scannedLogId)
                   AND substr(log_id, -length(@scannedLogId)) = @scannedLogId)
              )
            LIMIT 2
            """;
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@scannedLogId", logId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var matchedRecipe = reader.GetString(0);
        if (await reader.ReadAsync())
        {
            throw new InvalidOperationException(
                $"Log ID '{logId}' matches multiple mappings. Scan a more complete barcode.");
        }

        return matchedRecipe;
    }

    public async Task<List<BarcodeRecipeMapping>> ListMappingsAsync()
    {
        await using var conn = await OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT log_id, recipe_name, updated_at
            FROM barcode_recipe_mappings
            ORDER BY updated_at DESC, log_id ASC
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        var mappings = new List<BarcodeRecipeMapping>();

        while (await reader.ReadAsync())
        {
            mappings.Add(new BarcodeRecipeMapping
            {
                LogId = reader.GetString(0),
                RecipeName = reader.GetString(1),
                UpdatedAt = DateTime.Parse(reader.GetString(2))
            });
        }

        return mappings;
    }

    public Task UpsertMappingAsync(string logId, string recipeName)
        => UpsertMappingsAsync([logId], recipeName);

    public async Task UpsertMappingsAsync(IEnumerable<string> logIds, string recipeName)
    {
        await using var conn = await OpenConnectionAsync();
        using var transaction = conn.BeginTransaction();

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO barcode_recipe_mappings (log_id, recipe_name, updated_at)
            VALUES (@logId, @recipeName, strftime('%Y-%m-%dT%H:%M:%f', 'now'))
            ON CONFLICT(log_id) DO UPDATE
            SET recipe_name = @recipeName,
                updated_at = strftime('%Y-%m-%dT%H:%M:%f', 'now')
            """;
        var logIdParameter = cmd.Parameters.Add("@logId", SqliteType.Text);
        var recipeNameParameter = cmd.Parameters.Add("@recipeName", SqliteType.Text);

        foreach (var logId in logIds)
        {
            logIdParameter.Value = logId;
            recipeNameParameter.Value = recipeName;
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task ReplaceMappingsAsync(
        IEnumerable<string> existingLogIds,
        IEnumerable<string> logIds,
        string recipeName)
    {
        var existingIds = existingLogIds.Distinct(StringComparer.Ordinal).ToArray();
        var desiredIds = logIds.Distinct(StringComparer.Ordinal).ToArray();
        var removedIds = existingIds.Except(desiredIds, StringComparer.Ordinal).ToArray();

        await using var conn = await OpenConnectionAsync();
        using var transaction = conn.BeginTransaction();

        if (removedIds.Length > 0)
        {
            await using var deleteCmd = conn.CreateCommand();
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM barcode_recipe_mappings WHERE log_id = @logId";
            var logIdParameter = deleteCmd.Parameters.Add("@logId", SqliteType.Text);

            foreach (var logId in removedIds)
            {
                logIdParameter.Value = logId;
                await deleteCmd.ExecuteNonQueryAsync();
            }
        }

        await using var upsertCmd = conn.CreateCommand();
        upsertCmd.Transaction = transaction;
        upsertCmd.CommandText = """
            INSERT INTO barcode_recipe_mappings (log_id, recipe_name, updated_at)
            VALUES (@logId, @recipeName, strftime('%Y-%m-%dT%H:%M:%f', 'now'))
            ON CONFLICT(log_id) DO UPDATE
            SET recipe_name = @recipeName,
                updated_at = strftime('%Y-%m-%dT%H:%M:%f', 'now')
            """;
        var desiredLogIdParameter = upsertCmd.Parameters.Add("@logId", SqliteType.Text);
        var recipeNameParameter = upsertCmd.Parameters.Add("@recipeName", SqliteType.Text);

        foreach (var logId in desiredIds)
        {
            desiredLogIdParameter.Value = logId;
            recipeNameParameter.Value = recipeName;
            await upsertCmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<bool> DeleteMappingAsync(string logId)
        => await DeleteMappingsAsync([logId]) > 0;

    public async Task<int> DeleteMappingsAsync(IEnumerable<string> logIds)
    {
        await using var conn = await OpenConnectionAsync();
        using var transaction = conn.BeginTransaction();

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "DELETE FROM barcode_recipe_mappings WHERE log_id = @logId";
        var logIdParameter = cmd.Parameters.Add("@logId", SqliteType.Text);
        var deletedCount = 0;

        foreach (var logId in logIds)
        {
            logIdParameter.Value = logId;
            deletedCount += await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return deletedCount;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key LIMIT 1";
        cmd.Parameters.AddWithValue("@key", key);
        return await cmd.ExecuteScalarAsync() as string;
    }

    public async Task SetSettingAsync(string key, string? value)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value)
            VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE
            SET value = @value
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task EnsureDatabaseReadyAsync()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await using var conn = await OpenConnectionAsync();
        await using var schemaCmd = conn.CreateCommand();
        schemaCmd.CommandText = SchemaSql;
        await schemaCmd.ExecuteNonQueryAsync();
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath
        }.ConnectionString;

        var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
