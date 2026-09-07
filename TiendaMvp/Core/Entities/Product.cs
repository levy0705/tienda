using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("products")]
public sealed class Product
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull, Unique]
    public string InternalCode { get; set; } = string.Empty;

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string Unit { get; set; } = "unidad";
    // SQLite almacena estos valores como REAL para admitir unidades fraccionarias
    // y evitar problemas de mapeo de Decimal en el MVP local.
    public double PurchaseCost { get; set; }
    public double LastPurchaseCost { get; set; }
    public double SalePrice { get; set; }
    public double Stock { get; set; }
    public double MinimumStock { get; set; }
    public double TaxRate { get; set; }
    public string QrValue { get; set; } = string.Empty;
    public string? ImagePath { get; set; }

    [Ignore]
    public string CategoryName { get; set; } = string.Empty;

    [Ignore]
    public string Status => IsActive ? "Activo" : "Inactivo";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
