using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("customers")]
public sealed class Customer
{
    public const string GeneralId = "customer-general";

    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string Document { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsGeneral { get; set; }
    public bool IsBlocked { get; set; }
    public double CreditLimit { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string Status => IsBlocked ? "Bloqueado" : "Activo";

    [Ignore]
    public double BalanceDue { get; set; }

    [Ignore]
    public int PurchaseCount { get; set; }
}
