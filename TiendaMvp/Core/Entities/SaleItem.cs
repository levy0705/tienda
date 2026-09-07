using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("sale_items")]
public sealed class SaleItem
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string SaleId { get; set; } = string.Empty;

    [NotNull]
    public string ProductId { get; set; } = string.Empty;

    public double Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double Discount { get; set; }
    public double LineTotal { get; set; }

    [Ignore]
    public string ProductName { get; set; } = string.Empty;

    [Ignore]
    public string LineLabel => $"{Quantity:0.##} × ${UnitPrice:N0} = ${LineTotal:N0}";
}
