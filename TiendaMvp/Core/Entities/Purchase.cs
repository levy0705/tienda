using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("purchases")]
public sealed class Purchase
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string SupplierId { get; set; } = string.Empty;

    [NotNull]
    public string Status { get; set; } = PurchaseStatuses.Draft;

    public string DocumentNumber { get; set; } = string.Empty;
    public double Subtotal { get; set; }
    public double Discount { get; set; }
    public double AdditionalCosts { get; set; }
    public double Total { get; set; }
    public double PaidAmount { get; set; }
    public double BalanceDue { get; set; }
    public DateTime PurchaseDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string SupplierName { get; set; } = string.Empty;

    [Ignore]
    public string TotalLabel => $"${Total:N0}";
}
