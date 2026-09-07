using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("sales")]
public sealed class Sale
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string CustomerId { get; set; } = Customer.GeneralId;

    public string Status { get; set; } = SaleStatuses.Completed;
    public double Subtotal { get; set; }
    public double Discount { get; set; }
    public double Total { get; set; }
    public double PaidAmount { get; set; }
    public double CreditAmount { get; set; }
    public double ReturnedAmount { get; set; }
    public double ChangeAmount { get; set; }
    public string? CashSessionId { get; set; }
    public DateTime SaleDateUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string CustomerName { get; set; } = string.Empty;

    [Ignore]
    public string TotalLabel => $"${Total:N0}";

    [Ignore]
    public string StatusLabel => Status;
}
