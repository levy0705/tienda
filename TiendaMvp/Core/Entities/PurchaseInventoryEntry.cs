namespace TiendaMvp.Core.Entities;

/// <summary>
/// Línea de inventario que se aplica al recibir una compra.
/// </summary>
public sealed record PurchaseInventoryEntry(string ProductId, double Quantity, double UnitCost);
