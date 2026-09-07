using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("store_settings")]
public sealed class StoreSettings
{
    [PrimaryKey]
    public string Id { get; set; } = "store";

    public string StoreName { get; set; } = "Mi tienda";
    public string CurrencyCode { get; set; } = "COP";
    public string CurrencySymbol { get; set; } = "$";
    public bool IsConfigured { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
