using System.Globalization;
using System.Security.Cryptography;
using fuji_barcode.Models;

namespace fuji_barcode.Services;

public sealed class TraceabilityService(BarcodeLookupService database)
{
    private const int Iterations = 210000;
    public const int CredentialLength = 4;
    public static bool ValidId(string value) => value.Length == CredentialLength && value.All(c => c is >= '0' and <= '9');
    public static bool ValidPin(string value) => ValidId(value);

    public async Task<OperatorAuthentication?> AuthenticateAsync(string id, string pin)
    {
        if (!ValidId(id) || !ValidPin(pin)) return null;
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pin_hash FROM operators WHERE operator_id = @id";
        command.Parameters.AddWithValue("@id", id);
        var encoded = await command.ExecuteScalarAsync() as string;
        if (encoded is null) return null;
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256-v1" ||
                !int.TryParse(parts[1], out var iterations) || iterations is < 100000 or > 2000000) return null;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 16 || expected.Length != 32) return null;
            var actual = await Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, 32));
            try
            {
                return CryptographicOperations.FixedTimeEquals(actual, expected)
                    ? new OperatorAuthentication(id, DateTimeOffset.UtcNow) : null;
            }
            finally { CryptographicOperations.ZeroMemory(actual); }
        }
        catch (FormatException) { return null; }
    }

    public async Task<List<string>> ListOperatorsAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT operator_id FROM operators ORDER BY operator_id";
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    public async Task SaveOperatorAsync(string id, string pin, bool replace)
    {
        if (!ValidId(id) || !ValidPin(pin)) throw new InvalidOperationException("Operator ID and PIN must each contain exactly 4 digits.");
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = await Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32));
        var encoded = $"pbkdf2-sha256-v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        CryptographicOperations.ZeroMemory(hash);
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = replace
            ? "UPDATE operators SET pin_hash = @hash WHERE operator_id = @id"
            : "INSERT INTO operators(operator_id, pin_hash) VALUES (@id, @hash)";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@hash", encoded);
        if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Operator no longer exists. Refresh the list.");
    }

    public async Task DeleteOperatorAsync(string id)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM operators WHERE operator_id = @id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<long> RecordAsync(OperatorAuthentication authentication, ScanMode mode, string value, string recipe, string? logId)
    {
        if (mode is not (ScanMode.LogId or ScanMode.Recipe)) throw new ArgumentException("Invalid scan mode.", nameof(mode));
        if (mode == ScanMode.LogId && string.IsNullOrWhiteSpace(logId))
            throw new ArgumentException("A matched database Log ID is required.", nameof(logId));
        await using var connection = await database.OpenConnectionAsync();
        var columns = await BarcodeLookupService.GetHistoryColumnsAsync(connection);
        await using var command = connection.CreateCommand();
        // Older preview schemas require this obsolete column; do not populate or expose it.
        var legacyColumn = columns.Contains("lot_id") ? ", lot_id" : "";
        var legacyValue = columns.Contains("lot_id") ? ", ''" : "";
        command.CommandText = $"""
            INSERT INTO scan_history(scanned_at, operator_id, scan_type, scanned_value, log_id, recipe_id, outcome{legacyColumn})
            VALUES (@time, @operator, @type, @value, @log, @recipe, 'pending'{legacyValue});
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@time", authentication.ScannedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@operator", authentication.OperatorId);
        command.Parameters.AddWithValue("@type", mode.ToString());
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@log", mode == ScanMode.LogId ? (object)logId! : DBNull.Value);
        command.Parameters.AddWithValue("@recipe", recipe);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    public async Task RecordOutcomeAsync(long scanId, string outcome)
    {
        if (outcome is not ("run_ok" or "run_failed" or "connection_error")) throw new ArgumentException("Invalid launch outcome.", nameof(outcome));
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO scan_outcomes(scan_id, outcome) VALUES (@id, @outcome)";
        command.Parameters.AddWithValue("@id", scanId);
        command.Parameters.AddWithValue("@outcome", outcome);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ScanHistoryEntry>> ReadHistoryAsync(int page, long maximumId, DateTimeOffset? month = null)
    {
        string? start = null;
        string? end = null;
        if (month.HasValue)
        {
            var firstDay = new DateTime(month.Value.Year, month.Value.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            start = TimeZoneInfo.ConvertTimeToUtc(firstDay, TimeZoneInfo.Local).ToString("O", CultureInfo.InvariantCulture);
            end = TimeZoneInfo.ConvertTimeToUtc(firstDay.AddMonths(1), TimeZoneInfo.Local).ToString("O", CultureInfo.InvariantCulture);
        }
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT h.id, h.scanned_at, h.operator_id, h.scan_type, h.scanned_value,
                   CASE
                     WHEN h.scan_type <> 'LogId' THEN h.log_id
                     WHEN h.log_id IS NOT NULL THEN h.log_id
                     ELSE COALESCE(
                       (SELECT m.log_id FROM barcode_recipe_mappings m WHERE m.log_id = h.scanned_value),
                       (SELECT CASE WHEN COUNT(*) = 1 THEN MIN(m.log_id) END
                        FROM barcode_recipe_mappings m
                        WHERE length(m.log_id) > 0 AND length(h.scanned_value) > 0
                          AND m.recipe_name = h.recipe_id
                          AND ((length(m.log_id) <= length(h.scanned_value)
                                AND substr(h.scanned_value, -length(m.log_id)) = m.log_id)
                            OR (length(m.log_id) >= length(h.scanned_value)
                                AND substr(m.log_id, -length(h.scanned_value)) = h.scanned_value))),
                       h.log_id, h.scanned_value)
                   END, h.recipe_id,
                   COALESCE(o.outcome, h.outcome)
            FROM scan_history h LEFT JOIN scan_outcomes o ON o.scan_id = h.id
            WHERE h.id <= @maximum
              AND (@start IS NULL OR (julianday(h.scanned_at) >= julianday(@start)
                                      AND julianday(h.scanned_at) < julianday(@end)))
            ORDER BY h.id DESC LIMIT 51 OFFSET @offset
            """;
        command.Parameters.AddWithValue("@offset", checked(page * 50));
        command.Parameters.AddWithValue("@maximum", maximumId);
        command.Parameters.AddWithValue("@start", (object?)start ?? DBNull.Value);
        command.Parameters.AddWithValue("@end", (object?)end ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<ScanHistoryEntry>();
        while (await reader.ReadAsync()) rows.Add(new ScanHistoryEntry(reader.GetInt64(0),
            DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"),
            reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7)));
        return rows;
    }
}
