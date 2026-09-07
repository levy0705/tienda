using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("suppliers")]
public sealed class Supplier
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string Status => IsActive ? "Activo" : "Inactivo";

    [Ignore]
    public double BalanceDue { get; set; }

    [Ignore]
    public int PurchaseCount { get; set; }

    [Ignore]
    public int RelatedProductCount { get; set; }

    [Ignore]
    public string RelatedProductNames { get; set; } = string.Empty;
}
