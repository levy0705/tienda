using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("cash_sessions")]
public sealed class CashSession
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Status { get; set; } = CashSessionStatuses.Open;
    public double OpeningAmount { get; set; }
    public double ClosingAmount { get; set; }
    public double ExpectedAmount { get; set; }
    public double CountedAmount { get; set; }
    public double Difference { get; set; }
    public string ClosingObservations { get; set; } = string.Empty;
    public bool PinConfirmed { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    public string? UserId { get; set; }

    [Ignore]
    public string StatusLabel => Status;

    [Ignore]
    public string UserName { get; set; } = string.Empty;
}
