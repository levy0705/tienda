using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("credits")]
public sealed class Credit
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string SaleId { get; set; } = string.Empty;

    [NotNull]
    public string CustomerId { get; set; } = string.Empty;

    public double OriginalAmount { get; set; }
    public double BalanceDue { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    public string Status { get; set; } = CreditStatuses.Active;
    public string Notes { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string CustomerName { get; set; } = string.Empty;

    [Ignore]
    public string StatusLabel => BalanceDue <= 0 ? CreditStatuses.Paid : DueDateUtc.Date < DateTime.UtcNow.Date ? CreditStatuses.Overdue : CreditStatuses.Active;
}
