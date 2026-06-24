using Microsoft.Extensions.Configuration;
using Npgsql;
using fuji_barcode.Models;

namespace fuji_barcode.Services;

public class BarcodeLookupService
{
    private readonly string _connectionString;

    public BarcodeLookupService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BarcodeDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BarcodeDb is not configured");
    }

    public async Task<string?> LookupRecipeAsync(string objectId)
    {
        await using var conn = await OpenConnectionAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT recipe_name FROM barcode_recipe_mappings WHERE object_id = @objectId LIMIT 1",
            conn);
        cmd.Parameters.AddWithValue("objectId", objectId);

        return await cmd.ExecuteScalarAsync() as string;
    }

    public async Task<List<BarcodeRecipeMapping>> ListMappingsAsync()
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT object_id, recipe_name, updated_at
            FROM barcode_recipe_mappings
            ORDER BY updated_at DESC, object_id ASC
            """,
            conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        var mappings = new List<BarcodeRecipeMapping>();

        while (await reader.ReadAsync())
        {
            mappings.Add(new BarcodeRecipeMapping
            {
                ObjectId = reader.GetString(0),
                RecipeName = reader.GetString(1),
                UpdatedAt = reader.GetDateTime(2)
            });
        }

        return mappings;
    }

    public async Task UpsertMappingAsync(string objectId, string recipeName)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO barcode_recipe_mappings (object_id, recipe_name)
            VALUES (@objectId, @recipeName)
            ON CONFLICT (object_id) DO UPDATE
            SET recipe_name = EXCLUDED.recipe_name,
                updated_at = NOW()
            """,
            conn);
        cmd.Parameters.AddWithValue("objectId", objectId);
        cmd.Parameters.AddWithValue("recipeName", recipeName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DeleteMappingAsync(string objectId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM barcode_recipe_mappings WHERE object_id = @objectId",
            conn);
        cmd.Parameters.AddWithValue("objectId", objectId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
