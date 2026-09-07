using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("purchase_items")]
public sealed class PurchaseItem
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string PurchaseId { get; set; } = string.Empty;

    [NotNull]
    public string ProductId { get; set; } = string.Empty;

    public double Quantity { get; set; }
    public double UnitCost { get; set; }
    public double Discount { get; set; }
    public double LineTotal { get; set; }

    [Ignore]
    public string ProductName { get; set; } = string.Empty;

    [Ignore]
    public string LineLabel => $"{Quantity:0.##} × ${UnitCost:N0} = ${LineTotal:N0}";
}
