using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("inventory_movements")]
public sealed class InventoryMovement
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string ProductId { get; set; } = string.Empty;

    [NotNull]
    public string MovementType { get; set; } = string.Empty;

    // Positive values enter inventory; negative values leave inventory.
    public double Quantity { get; set; }
    public double PreviousStock { get; set; }
    public double NewStock { get; set; }

    [NotNull]
    public string Reason { get; set; } = string.Empty;

    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string ProductName { get; set; } = string.Empty;

    [Ignore]
    public string QuantityLabel => Quantity >= 0 ? $"+{Quantity:0.##}" : $"{Quantity:0.##}";
}
