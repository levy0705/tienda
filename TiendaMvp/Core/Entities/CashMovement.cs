using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("cash_movements")]
public sealed class CashMovement
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string CashSessionId { get; set; } = string.Empty;

    public string MovementType { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
