using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class PurchaseService : IPurchaseService
{
    private readonly ILocalDatabase _database;
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;

    public PurchaseService(ILocalDatabase database, IInventoryService inventory, IAuditService audit)
    {
        _database = database;
        _inventory = inventory;
        _audit = audit;
    }

    public IReadOnlyList<Purchase> GetPurchases(string? supplierId = null, string? status = null, string? query = null)
    {
        var suppliers = _database.Connection.Table<Supplier>().ToList().ToDictionary(supplier => supplier.Id, supplier => supplier.Name);
        var normalized = query?.Trim();
        return _database.Connection.Table<Purchase>().ToList()
            .Where(purchase => string.IsNullOrWhiteSpace(supplierId) || purchase.SupplierId == supplierId)
            .Where(purchase => string.IsNullOrWhiteSpace(status) || purchase.Status == status)
            .Where(purchase => string.IsNullOrWhiteSpace(normalized) || purchase.DocumentNumber.Contains(normalized, StringComparison.OrdinalIgnoreCase) || (suppliers.TryGetValue(purchase.SupplierId, out var name) && name.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
            .Select(purchase =>
            {
                purchase.SupplierName = suppliers.TryGetValue(purchase.SupplierId, out var name) ? name : "Proveedor eliminado";
                return purchase;
            })
            .OrderByDescending(purchase => purchase.PurchaseDateUtc)
            .ToList();
    }

    public Purchase? GetPurchase(string id)
    {
        var purchase = _database.Connection.Find<Purchase>(id);
        if (purchase is not null)
            purchase.SupplierName = _database.Connection.Find<Supplier>(purchase.SupplierId)?.Name ?? "Proveedor eliminado";
        return purchase;
    }

    public IReadOnlyList<PurchaseItem> GetItems(string purchaseId)
    {
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id, product => product.Name);
        return _database.Connection.Table<PurchaseItem>().ToList()
            .Where(item => item.PurchaseId == purchaseId)
            .Select(item =>
            {
                item.ProductName = products.TryGetValue(item.ProductId, out var name) ? name : "Producto eliminado";
                return item;
            })
            .ToList();
    }

    public void SaveDraft(Purchase purchase, IEnumerable<PurchaseItem> items)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        ArgumentNullException.ThrowIfNull(items);
        if (purchase.Status != PurchaseStatuses.Draft && _database.Connection.Find<Purchase>(purchase.Id) is not null)
            throw new InvalidOperationException("Solo se pueden editar compras en borrador.");

        var supplier = _database.Connection.Find<Supplier>(purchase.SupplierId);
        if (supplier is null || !supplier.IsActive)
            throw new InvalidOperationException("Selecciona un proveedor activo.");

        var normalizedItems = NormalizeItems(purchase.Id, items);
        if (purchase.Discount < 0 || purchase.AdditionalCosts < 0)
            throw new InvalidOperationException("Los descuentos y costos adicionales no pueden ser negativos.");

        purchase.Status = PurchaseStatuses.Draft;
        purchase.Subtotal = normalizedItems.Sum(item => item.LineTotal);
        purchase.Total = Math.Max(0, purchase.Subtotal - purchase.Discount + purchase.AdditionalCosts);
        purchase.PaidAmount = 0;
        purchase.BalanceDue = purchase.Total;
        purchase.DocumentNumber = purchase.DocumentNumber.Trim();
        purchase.Notes = purchase.Notes.Trim();
        purchase.PurchaseDateUtc = purchase.PurchaseDateUtc == default ? DateTime.UtcNow : purchase.PurchaseDateUtc;
        purchase.UpdatedAtUtc = DateTime.UtcNow;

        var existing = _database.Connection.Find<Purchase>(purchase.Id);
        _database.RunInTransaction(() =>
        {
            if (existing is null)
            {
                purchase.CreatedAtUtc = purchase.UpdatedAtUtc;
                _database.Connection.Insert(purchase);
            }
            else
            {
                _database.Connection.Update(purchase);
                foreach (var oldItem in _database.Connection.Table<PurchaseItem>().ToList().Where(item => item.PurchaseId == purchase.Id))
                    _database.Connection.Delete(oldItem);
            }

            foreach (var item in normalizedItems)
                _database.Connection.Insert(item);
        });
        _audit.Record(existing is null ? "Crear" : "Editar", nameof(Purchase), purchase.Id, "Borrador de compra");
    }

    public Purchase ReceivePurchase(string purchaseId, double paidAmount = 0)
    {
        var purchase = GetPurchase(purchaseId) ?? throw new InvalidOperationException("Compra no encontrada.");
        if (purchase.Status != PurchaseStatuses.Draft)
            throw new InvalidOperationException("Solo se puede recibir una compra en borrador.");
        var items = GetItems(purchaseId);
        if (items.Count == 0)
            throw new InvalidOperationException("La compra debe tener productos antes de recibirse.");
        if (paidAmount < 0 || paidAmount > purchase.Total)
            throw new InvalidOperationException("El pago debe estar entre cero y el total de la compra.");

        var subtotal = items.Sum(item => item.LineTotal);
        var totalQuantity = items.Sum(item => item.Quantity);
        _database.RunInTransaction(() =>
        {
            _inventory.RegisterPurchaseEntries(
                items.Select(item =>
                {
                    // Distribuir descuento global y costos adicionales entre las líneas para que
                    // el costo promedio del inventario refleje el costo real de la recepción.
                    var weight = subtotal > 0 ? item.LineTotal / subtotal : item.Quantity / totalQuantity;
                    var effectiveLineTotal = purchase.Total * weight;
                    var effectiveUnitCost = item.Quantity > 0 ? effectiveLineTotal / item.Quantity : item.UnitCost;
                    return new PurchaseInventoryEntry(item.ProductId, item.Quantity, effectiveUnitCost);
                }),
                purchase.Id);

            purchase.PaidAmount = paidAmount;
            purchase.BalanceDue = Math.Max(0, purchase.Total - paidAmount);
            purchase.Status = purchase.Total == 0
                ? PurchaseStatuses.Received
                : purchase.BalanceDue == 0 ? PurchaseStatuses.Paid : PurchaseStatuses.PendingPayment;
            purchase.ReceivedAtUtc = DateTime.UtcNow;
            purchase.UpdatedAtUtc = DateTime.UtcNow;
            _database.Connection.Update(purchase);
            _audit.Record("Recibir", nameof(Purchase), purchase.Id, $"Estado: {purchase.Status}");
        });
        return purchase;
    }

    public Purchase RegisterPayment(string purchaseId, double amount)
    {
        var purchase = GetPurchase(purchaseId) ?? throw new InvalidOperationException("Compra no encontrada.");
        if (purchase.Status is not (PurchaseStatuses.PendingPayment or PurchaseStatuses.Received))
            throw new InvalidOperationException("Esta compra no tiene un saldo pendiente.");
        if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
            throw new InvalidOperationException("El abono debe ser mayor que cero.");
        if (amount > purchase.BalanceDue)
            throw new InvalidOperationException("El abono no puede superar el saldo pendiente.");

        purchase.PaidAmount += amount;
        purchase.BalanceDue = Math.Max(0, purchase.Total - purchase.PaidAmount);
        purchase.Status = purchase.BalanceDue == 0 ? PurchaseStatuses.Paid : PurchaseStatuses.PendingPayment;
        purchase.UpdatedAtUtc = DateTime.UtcNow;
        _database.RunInTransaction(() =>
        {
            _database.Connection.Update(purchase);
            _audit.Record("Abono", nameof(Purchase), purchase.Id, $"Abono: {amount:0.##}");
        });
        return purchase;
    }

    public void CancelPurchase(string purchaseId)
    {
        var purchase = GetPurchase(purchaseId) ?? throw new InvalidOperationException("Compra no encontrada.");
        if (purchase.Status == PurchaseStatuses.Cancelled)
            throw new InvalidOperationException("La compra ya está anulada.");
        if (purchase.PaidAmount > 0)
            throw new InvalidOperationException("No se puede anular una compra con pagos registrados.");

        var items = GetItems(purchaseId);
        if (purchase.Status != PurchaseStatuses.Draft)
        {
            // Validar todas las líneas antes de aplicar la primera devolución para evitar una reversión parcial.
            foreach (var item in items)
            {
                var product = _database.Connection.Find<Product>(item.ProductId);
                if (product is null || !product.IsActive)
                    throw new InvalidOperationException("No se puede revertir la compra porque uno de sus productos no está activo.");
                if (product.Stock < item.Quantity)
                    throw new InvalidOperationException($"No hay existencia suficiente para devolver {item.ProductName}.");
            }

            _database.RunInTransaction(() =>
            {
                foreach (var item in items)
                    _inventory.RegisterSupplierReturn(item.ProductId, item.Quantity, purchase.Id, "Anulación de compra");

                purchase.Status = PurchaseStatuses.Cancelled;
                purchase.BalanceDue = 0;
                purchase.UpdatedAtUtc = DateTime.UtcNow;
                _database.Connection.Update(purchase);
                _audit.Record("Anular", nameof(Purchase), purchase.Id, "Compra anulada bajo reglas controladas");
            });
            return;
        }

        _database.RunInTransaction(() =>
        {
            purchase.Status = PurchaseStatuses.Cancelled;
            purchase.BalanceDue = 0;
            purchase.UpdatedAtUtc = DateTime.UtcNow;
            _database.Connection.Update(purchase);
            _audit.Record("Anular", nameof(Purchase), purchase.Id, "Compra anulada bajo reglas controladas");
        });
    }

    private List<PurchaseItem> NormalizeItems(string purchaseId, IEnumerable<PurchaseItem> items)
    {
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id);
        var normalized = new List<PurchaseItem>();
        foreach (var item in items)
        {
            if (!products.ContainsKey(item.ProductId))
                throw new InvalidOperationException("Uno de los productos de la compra no existe.");
            if (item.Quantity <= 0 || double.IsNaN(item.Quantity) || double.IsInfinity(item.Quantity))
                throw new InvalidOperationException("Las cantidades deben ser mayores que cero.");
            if (item.UnitCost < 0 || double.IsNaN(item.UnitCost) || double.IsInfinity(item.UnitCost))
                throw new InvalidOperationException("Los costos deben ser válidos y no negativos.");
            if (item.Discount < 0 || item.Discount > item.Quantity * item.UnitCost)
                throw new InvalidOperationException("El descuento de una línea no puede ser negativo ni superar su importe.");

            normalized.Add(new PurchaseItem
            {
                Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
                PurchaseId = purchaseId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                Discount = item.Discount,
                LineTotal = Math.Max(0, item.Quantity * item.UnitCost - item.Discount)
            });
        }

        return normalized
            .GroupBy(item => item.ProductId)
            .Select(group => new PurchaseItem
            {
                Id = group.First().Id,
                PurchaseId = purchaseId,
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                UnitCost = group.Sum(item => item.Quantity * item.UnitCost) / group.Sum(item => item.Quantity),
                Discount = group.Sum(item => item.Discount),
                LineTotal = group.Sum(item => item.LineTotal)
            })
            .ToList();
    }
}
