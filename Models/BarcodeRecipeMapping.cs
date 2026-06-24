namespace fuji_barcode.Models;

public sealed class BarcodeRecipeMapping
{
    public string ObjectId { get; init; } = "";
    public string RecipeName { get; init; } = "";
    public DateTime UpdatedAt { get; init; }
}
