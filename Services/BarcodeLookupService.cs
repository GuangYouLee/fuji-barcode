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

        return await cmd.ExecuteScalarAsync() as string;
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

    public async Task UpsertMappingAsync(string logId, string recipeName)
    {
        await using var conn = await OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO barcode_recipe_mappings (log_id, recipe_name, updated_at)
            VALUES (@logId, @recipeName, strftime('%Y-%m-%dT%H:%M:%f', 'now'))
            ON CONFLICT(log_id) DO UPDATE
            SET recipe_name = @recipeName,
                updated_at = strftime('%Y-%m-%dT%H:%M:%f', 'now')
            """;
        cmd.Parameters.AddWithValue("@logId", logId);
        cmd.Parameters.AddWithValue("@recipeName", recipeName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DeleteMappingAsync(string logId)
    {
        await using var conn = await OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM barcode_recipe_mappings WHERE log_id = @logId";
        cmd.Parameters.AddWithValue("@logId", logId);
        return await cmd.ExecuteNonQueryAsync() > 0;
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
