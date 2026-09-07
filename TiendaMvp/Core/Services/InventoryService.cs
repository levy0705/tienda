using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly ILocalDatabase _database;
    private readonly IAuditService _audit;
    private readonly IUserSession _session;
    private readonly IPermissionService _permissions;

    public InventoryService(ILocalDatabase database, IAuditService audit, IUserSession session, IPermissionService permissions)
    {
        _database = database;
        _audit = audit;
        _session = session;
        _permissions = permissions;
    }

    public IReadOnlyList<InventoryMovement> GetMovements(string? productId = null, int? limit = null)
    {
        var productNames = _database.Connection.Table<Product>()
            .ToList()
            .ToDictionary(product => product.Id, product => product.Name);

        IEnumerable<InventoryMovement> query = _database.Connection.Table<InventoryMovement>()
            .ToList()
            .Where(movement => string.IsNullOrWhiteSpace(productId) || movement.ProductId == productId)
            .OrderByDescending(movement => movement.CreatedAtUtc);

        if (limit is > 0)
            query = query.Take(limit.Value);

        return query
            .Select(movement =>
            {
                movement.ProductName = productNames.TryGetValue(movement.ProductId, out var name) ? name : "Producto eliminado";
                return movement;
            })
            .ToList();
    }

    public IReadOnlyList<Product> GetLowStockProducts() =>
        _database.Connection.Table<Product>()
            .ToList()
            .Where(product => product.IsActive && product.MinimumStock > 0 && product.Stock <= product.MinimumStock)
            .OrderBy(product => product.Stock)
            .ToList();

    public double GetEstimatedValue() =>
        _database.Connection.Table<Product>()
            .ToList()
            .Where(product => product.IsActive)
            .Sum(product => product.Stock * product.PurchaseCost);

    public InventoryMovement RegisterInitialInventory(string productId, double quantity, string reason = "Inventario inicial")
    {
        _permissions.Require(AppPermission.AdjustInventory);
        if (_database.Connection.Table<InventoryMovement>().ToList().Any(movement => movement.ProductId == productId))
            throw new InvalidOperationException("El inventario inicial de este producto ya fue registrado.");

        return ApplyMovement(productId, InventoryMovementTypes.Initial, quantity, reason);
    }

    public IReadOnlyList<InventoryMovement> RegisterPurchaseEntries(
        IEnumerable<PurchaseInventoryEntry> entries,
        string? referenceId,
        string reason = "Entrada por compra")
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("El motivo del movimiento es obligatorio.");

        var materialized = entries.ToList();
        if (materialized.Count == 0)
            throw new InvalidOperationException("La compra debe tener al menos un producto.");

        foreach (var entry in materialized)
        {
            PositiveQuantity(entry.Quantity);
            if (entry.UnitCost < 0 || double.IsNaN(entry.UnitCost) || double.IsInfinity(entry.UnitCost))
                throw new InvalidOperationException("El costo unitario debe ser válido y no negativo.");
        }

        // Si un producto aparece dos veces en el borrador, se consolida antes de mover existencias.
        var lines = materialized
            .GroupBy(entry => entry.ProductId)
            .Select(group => new PurchaseInventoryEntry(
                group.Key,
                group.Sum(entry => PositiveQuantity(entry.Quantity)),
                group.Sum(entry => entry.Quantity * entry.UnitCost) / group.Sum(entry => entry.Quantity)))
            .ToList();

        var products = lines.ToDictionary(line => line.ProductId, line => GetProduct(line.ProductId));
        foreach (var line in lines)
        {
            if (!products[line.ProductId].IsActive)
                throw new InvalidOperationException("No se pueden recibir productos inactivos.");
            if (products[line.ProductId].Stock + line.Quantity < 0)
                throw new InvalidOperationException("La existencia resultante no puede ser negativa.");
        }

        var movements = new List<InventoryMovement>(lines.Count);
        _database.RunInTransaction(() =>
        {
            foreach (var line in lines)
            {
                var product = products[line.ProductId];
                var previousStock = product.Stock;
                var newStock = previousStock + line.Quantity;
                var now = DateTime.UtcNow;
                var movement = new InventoryMovement
                {
                    ProductId = product.Id,
                    MovementType = InventoryMovementTypes.PurchaseEntry,
                    Quantity = line.Quantity,
                    PreviousStock = previousStock,
                    NewStock = newStock,
                    Reason = reason.Trim(),
                    ReferenceType = "Compra",
                    ReferenceId = referenceId,
                    UserId = _session.CurrentUser?.Id,
                    CreatedAtUtc = now
                };

                // Costo promedio ponderado para valorar existencias y conservar el último costo recibido.
                product.PurchaseCost = newStock > 0
                    ? ((previousStock * product.PurchaseCost) + (line.Quantity * line.UnitCost)) / newStock
                    : line.UnitCost;
                product.LastPurchaseCost = line.UnitCost;
                product.Stock = newStock;
                product.UpdatedAtUtc = now;
                _database.Connection.Update(product);
                _database.Connection.Insert(movement);
                movements.Add(movement);
            }
        });

        foreach (var movement in movements)
            _audit.Record("Movimiento", nameof(InventoryMovement), movement.Id, $"{movement.MovementType}: {movement.Quantity:0.##}");

        return movements;
    }

    public InventoryMovement RegisterPurchaseEntry(string productId, double quantity, string? referenceId = null, string reason = "Entrada por compra")
    {
        var movements = RegisterPurchaseEntries(
            new[] { new PurchaseInventoryEntry(productId, quantity, GetProduct(productId).PurchaseCost) },
            referenceId,
            reason);
        return movements[0];
    }

    public IReadOnlyList<InventoryMovement> RegisterSaleExits(
        IEnumerable<InventoryQuantityEntry> entries,
        string? referenceId,
        string reason = "Salida por venta")
    {
        ArgumentNullException.ThrowIfNull(entries);
        var materialized = entries.ToList();
        if (materialized.Count == 0)
            throw new InvalidOperationException("La venta debe tener al menos un producto.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("El motivo del movimiento es obligatorio.");

        var lines = materialized
            .GroupBy(entry => entry.ProductId)
            .Select(group => new InventoryQuantityEntry(group.Key, group.Sum(entry => PositiveQuantity(entry.Quantity))))
            .ToList();
        var products = lines.ToDictionary(line => line.ProductId, line => GetProduct(line.ProductId));
        foreach (var line in lines)
        {
            if (!products[line.ProductId].IsActive)
                throw new InvalidOperationException("No se pueden vender productos inactivos.");
            if (products[line.ProductId].Stock < line.Quantity)
                throw new InvalidOperationException($"Existencia insuficiente para {products[line.ProductId].Name}. Disponible: {products[line.ProductId].Stock:0.##}.");
        }

        var movements = new List<InventoryMovement>(lines.Count);
        _database.RunInTransaction(() =>
        {
            foreach (var line in lines)
            {
                var product = products[line.ProductId];
                var now = DateTime.UtcNow;
                var movement = new InventoryMovement
                {
                    ProductId = product.Id,
                    MovementType = InventoryMovementTypes.SaleExit,
                    Quantity = -line.Quantity,
                    PreviousStock = product.Stock,
                    NewStock = product.Stock - line.Quantity,
                    Reason = reason.Trim(),
                    ReferenceType = "Venta",
                    ReferenceId = referenceId,
                    UserId = _session.CurrentUser?.Id,
                    CreatedAtUtc = now
                };
                product.Stock = movement.NewStock;
                product.UpdatedAtUtc = now;
                _database.Connection.Update(product);
                _database.Connection.Insert(movement);
                movements.Add(movement);
            }
        });
        foreach (var movement in movements)
            _audit.Record("Movimiento", nameof(InventoryMovement), movement.Id, $"{movement.MovementType}: {movement.Quantity:0.##}");
        return movements;
    }

    public InventoryMovement RegisterSaleExit(string productId, double quantity, string? referenceId = null, string reason = "Salida por venta") =>
        RegisterSaleExits(new[] { new InventoryQuantityEntry(productId, quantity) }, referenceId, reason)[0];

    public InventoryMovement RegisterCustomerReturn(string productId, double quantity, string? referenceId = null, string reason = "Devolución de cliente") =>
        ApplyMovement(productId, InventoryMovementTypes.CustomerReturn, PositiveQuantity(quantity), reason, "Devolución", referenceId);

    public InventoryMovement RegisterSupplierReturn(string productId, double quantity, string? referenceId = null, string reason = "Devolución a proveedor") =>
        RegisterSupplierReturnAuthorized(productId, quantity, referenceId, reason);

    public InventoryMovement RegisterAdjustment(string productId, double signedQuantity, string reason)
    {
        _permissions.Require(AppPermission.AdjustInventory);
        if (signedQuantity == 0)
            throw new InvalidOperationException("El ajuste debe ser diferente de cero.");

        var type = signedQuantity > 0 ? InventoryMovementTypes.PositiveAdjustment : InventoryMovementTypes.NegativeAdjustment;
        return ApplyMovement(productId, type, signedQuantity, reason);
    }

    public InventoryMovement RegisterDamaged(string productId, double quantity, string reason)
    {
        _permissions.Require(AppPermission.AdjustInventory);
        return ApplyMovement(productId, InventoryMovementTypes.Damaged, -PositiveQuantity(quantity), reason);
    }

    public InventoryMovement RegisterLoss(string productId, double quantity, string reason)
    {
        _permissions.Require(AppPermission.AdjustInventory);
        return ApplyMovement(productId, InventoryMovementTypes.Loss, -PositiveQuantity(quantity), reason);
    }

    public InventoryMovement RegisterPhysicalCount(string productId, double countedQuantity, string reason = "Corrección de conteo")
    {
        _permissions.Require(AppPermission.AdjustInventory);
        if (countedQuantity < 0)
            throw new InvalidOperationException("El conteo no puede ser negativo.");

        var product = GetProduct(productId);
        var difference = countedQuantity - product.Stock;
        if (difference == 0)
            throw new InvalidOperationException("El conteo coincide con la existencia registrada; no hay ningún ajuste que guardar.");

        return ApplyMovement(productId, InventoryMovementTypes.CountCorrection, difference, reason);
    }

    private InventoryMovement RegisterSupplierReturnAuthorized(string productId, double quantity, string? referenceId, string reason)
    {
        _permissions.Require(AppPermission.AdjustInventory);
        return ApplyMovement(productId, InventoryMovementTypes.SupplierReturn, -PositiveQuantity(quantity), reason, "Devolución proveedor", referenceId);
    }

    private InventoryMovement ApplyMovement(
        string productId,
        string movementType,
        double quantity,
        string reason,
        string? referenceType = null,
        string? referenceId = null)
    {
        if (!InventoryMovementTypes.All.Contains(movementType))
            throw new InvalidOperationException("Tipo de movimiento no válido.");
        if (quantity == 0 || double.IsNaN(quantity) || double.IsInfinity(quantity))
            throw new InvalidOperationException("La cantidad del movimiento debe ser válida y diferente de cero.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("El motivo del movimiento es obligatorio.");
        if (InventoryMovementTypes.IsPositive(movementType) && quantity < 0)
            throw new InvalidOperationException("Este movimiento debe aumentar las existencias.");
        if (InventoryMovementTypes.IsNegative(movementType) && quantity > 0)
            throw new InvalidOperationException("Este movimiento debe disminuir las existencias.");

        var product = GetProduct(productId);
        if (!product.IsActive)
            throw new InvalidOperationException("No se pueden mover existencias de un producto inactivo.");

        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = movementType,
            Quantity = quantity,
            PreviousStock = product.Stock,
            NewStock = product.Stock + quantity,
            Reason = reason.Trim(),
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            UserId = _session.CurrentUser?.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (movement.NewStock < 0)
            throw new InvalidOperationException($"Movimiento inválido: la existencia no puede quedar negativa. Disponible: {product.Stock:0.##}.");

        _database.RunInTransaction(() =>
        {
            product.Stock = movement.NewStock;
            product.UpdatedAtUtc = DateTime.UtcNow;
            _database.Connection.Update(product);
            _database.Connection.Insert(movement);
        });

        _audit.Record("Movimiento", nameof(InventoryMovement), movement.Id, $"{movement.MovementType}: {movement.Quantity:0.##}");
        return movement;
    }

    private Product GetProduct(string productId) =>
        _database.Connection.Find<Product>(productId) ?? throw new InvalidOperationException("Producto no encontrado.");

    private static double PositiveQuantity(double quantity)
    {
        if (quantity <= 0 || double.IsNaN(quantity) || double.IsInfinity(quantity))
            throw new InvalidOperationException("La cantidad debe ser mayor que cero.");
        return quantity;
    }
}
