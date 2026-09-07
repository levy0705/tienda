using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("credit_movements")]
public sealed class CreditMovement
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string CreditId { get; set; } = string.Empty;

    public string MovementType { get; set; } = CreditMovementTypes.Origin;
    public double Amount { get; set; }
    public double BalanceBefore { get; set; }
    public double BalanceAfter { get; set; }
    public string? CashSessionId { get; set; }
    public string? ReferenceId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string AmountLabel => $"${Amount:N0}";
}
