using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class SupplierService : ISupplierService
{
    private readonly ILocalDatabase _database;
    private readonly IAuditService _audit;

    public SupplierService(ILocalDatabase database, IAuditService audit)
    {
        _database = database;
        _audit = audit;
    }

    public IReadOnlyList<Supplier> GetSuppliers(string? query = null, bool includeInactive = true)
    {
        var suppliers = _database.Connection.Table<Supplier>().ToList();
        var purchases = _database.Connection.Table<Purchase>().ToList();
        var items = _database.Connection.Table<PurchaseItem>().ToList();
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id, product => product.Name);
        var normalized = query?.Trim();

        return suppliers
            .Where(supplier => includeInactive || supplier.IsActive)
            .Where(supplier => string.IsNullOrWhiteSpace(normalized)
                || supplier.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || supplier.TaxId.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || supplier.Phone.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Select(supplier =>
            {
                var supplierPurchases = purchases.Where(purchase => purchase.SupplierId == supplier.Id && purchase.Status != PurchaseStatuses.Cancelled && purchase.Status != PurchaseStatuses.Draft).ToList();
                supplier.PurchaseCount = supplierPurchases.Count;
                supplier.BalanceDue = supplierPurchases.Sum(purchase => Math.Max(0, purchase.BalanceDue));
                var relatedIds = items.Where(item => supplierPurchases.Any(purchase => purchase.Id == item.PurchaseId)).Select(item => item.ProductId).Distinct().ToList();
                supplier.RelatedProductCount = relatedIds.Count;
                supplier.RelatedProductNames = string.Join(", ", relatedIds.Select(id => products.TryGetValue(id, out var productName) ? productName : "Producto eliminado").Take(3));
                return supplier;
            })
            .OrderBy(supplier => supplier.Name)
            .ToList();
    }

    public Supplier? GetSupplier(string id) => _database.Connection.Find<Supplier>(id);

    public void SaveSupplier(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        supplier.Name = supplier.Name.Trim();
        supplier.TaxId = supplier.TaxId.Trim();
        supplier.Phone = supplier.Phone.Trim();
        supplier.Email = supplier.Email.Trim();
        supplier.Address = supplier.Address.Trim();
        supplier.Notes = supplier.Notes.Trim();
        if (string.IsNullOrWhiteSpace(supplier.Name))
            throw new InvalidOperationException("El nombre del proveedor es obligatorio.");

        var duplicate = _database.Connection.Table<Supplier>().ToList().FirstOrDefault(existing =>
            existing.Id != supplier.Id
            && ((string.IsNullOrWhiteSpace(supplier.TaxId) == false && string.Equals(existing.TaxId, supplier.TaxId, StringComparison.OrdinalIgnoreCase))
                || string.Equals(existing.Name, supplier.Name, StringComparison.OrdinalIgnoreCase)));
        if (duplicate is not null)
            throw new InvalidOperationException("Ya existe un proveedor con ese nombre o identificación fiscal.");

        var isNew = _database.Connection.Find<Supplier>(supplier.Id) is null;
        supplier.UpdatedAtUtc = DateTime.UtcNow;
        if (isNew)
        {
            supplier.CreatedAtUtc = supplier.UpdatedAtUtc;
            _database.Connection.Insert(supplier);
        }
        else
        {
            _database.Connection.Update(supplier);
        }

        _audit.Record(isNew ? "Crear" : "Editar", nameof(Supplier), supplier.Id, supplier.Name);
    }

    public void SetSupplierActive(string id, bool isActive)
    {
        var supplier = GetSupplier(id) ?? throw new InvalidOperationException("Proveedor no encontrado.");
        supplier.IsActive = isActive;
        supplier.UpdatedAtUtc = DateTime.UtcNow;
        _database.Connection.Update(supplier);
        _audit.Record(isActive ? "Activar" : "Desactivar", nameof(Supplier), id, supplier.Name);
    }

    public double GetBalanceDue(string supplierId) =>
        _database.Connection.Table<Purchase>().ToList()
            .Where(purchase => purchase.SupplierId == supplierId && purchase.Status != PurchaseStatuses.Cancelled && purchase.Status != PurchaseStatuses.Draft)
            .Sum(purchase => Math.Max(0, purchase.BalanceDue));

    public IReadOnlyList<string> GetRelatedProductIds(string supplierId)
    {
        var purchaseIds = _database.Connection.Table<Purchase>().ToList()
            .Where(purchase => purchase.SupplierId == supplierId && purchase.Status != PurchaseStatuses.Cancelled && purchase.Status != PurchaseStatuses.Draft)
            .Select(purchase => purchase.Id)
            .ToHashSet();
        return _database.Connection.Table<PurchaseItem>().ToList()
            .Where(item => purchaseIds.Contains(item.PurchaseId))
            .Select(item => item.ProductId)
            .Distinct()
            .ToList();
    }
}
