using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IInventoryService
{
    IReadOnlyList<InventoryMovement> GetMovements(string? productId = null, int? limit = null);
    IReadOnlyList<Product> GetLowStockProducts();
    double GetEstimatedValue();
    InventoryMovement RegisterInitialInventory(string productId, double quantity, string reason = "Inventario inicial");
    IReadOnlyList<InventoryMovement> RegisterPurchaseEntries(IEnumerable<PurchaseInventoryEntry> entries, string? referenceId, string reason = "Entrada por compra");
    IReadOnlyList<InventoryMovement> RegisterSaleExits(IEnumerable<InventoryQuantityEntry> entries, string? referenceId, string reason = "Salida por venta");
    InventoryMovement RegisterPurchaseEntry(string productId, double quantity, string? referenceId = null, string reason = "Entrada por compra");
    InventoryMovement RegisterSaleExit(string productId, double quantity, string? referenceId = null, string reason = "Salida por venta");
    InventoryMovement RegisterCustomerReturn(string productId, double quantity, string? referenceId = null, string reason = "Devolución de cliente");
    InventoryMovement RegisterSupplierReturn(string productId, double quantity, string? referenceId = null, string reason = "Devolución a proveedor");
    InventoryMovement RegisterAdjustment(string productId, double signedQuantity, string reason);
    InventoryMovement RegisterDamaged(string productId, double quantity, string reason);
    InventoryMovement RegisterLoss(string productId, double quantity, string reason);
    InventoryMovement RegisterPhysicalCount(string productId, double countedQuantity, string reason = "Corrección de conteo");
}
