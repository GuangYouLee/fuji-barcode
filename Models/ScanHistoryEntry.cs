namespace fuji_barcode.Models;

public sealed record OperatorAuthentication(string OperatorId, DateTimeOffset ScannedAt);

public sealed record ScanHistoryEntry(long Id, string ScannedAt, string OperatorId,
    string ScanType, string ScannedValue, string? LogId, string? RecipeId, string Outcome);
