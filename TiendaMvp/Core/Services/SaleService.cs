using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class SaleService : ISaleService
{
    private readonly ILocalDatabase _database;
    private readonly IInventoryService _inventory;
    private readonly ICustomerService _customers;
    private readonly ICashService _cash;
    private readonly IAuditService _audit;
    private readonly IUserSession _session;
    private readonly IPermissionService _permissions;

    public SaleService(
        ILocalDatabase database,
        IInventoryService inventory,
        ICustomerService customers,
        ICashService cash,
        IAuditService audit,
        IUserSession session,
        IPermissionService permissions)
    {
        _database = database;
        _inventory = inventory;
        _customers = customers;
        _cash = cash;
        _audit = audit;
        _session = session;
        _permissions = permissions;
    }

    public IReadOnlyList<Sale> GetSales(string? customerId = null)
    {
        var customers = _database.Connection.Table<Customer>().ToList().ToDictionary(customer => customer.Id, customer => customer.Name);
        return _database.Connection.Table<Sale>().ToList()
            .Where(sale => string.IsNullOrWhiteSpace(customerId) || sale.CustomerId == customerId)
            .Select(sale =>
            {
                sale.CustomerName = customers.TryGetValue(sale.CustomerId, out var name) ? name : "Cliente eliminado";
                return sale;
            })
            .OrderByDescending(sale => sale.SaleDateUtc)
            .ToList();
    }

    public IReadOnlyList<SaleItem> GetItems(string saleId)
    {
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id, product => product.Name);
        return _database.Connection.Table<SaleItem>().ToList()
            .Where(item => item.SaleId == saleId)
            .Select(item =>
            {
                item.ProductName = products.TryGetValue(item.ProductId, out var name) ? name : "Producto eliminado";
                return item;
            })
            .ToList();
    }

    public IReadOnlyList<SalePayment> GetPayments(string saleId) =>
        _database.Connection.Table<SalePayment>().ToList()
            .Where(payment => payment.SaleId == saleId)
            .OrderBy(payment => payment.CreatedAtUtc)
            .ToList();

    public Sale? GetSale(string id)
    {
        var sale = _database.Connection.Find<Sale>(id);
        if (sale is not null)
            sale.CustomerName = _database.Connection.Find<Customer>(sale.CustomerId)?.Name ?? "Cliente eliminado";
        return sale;
    }

    public Sale RegisterSale(Sale sale, IEnumerable<SaleItem> items, DateTime? dueDate = null)
    {
        var payments = sale.PaidAmount > 0
            ? new[] { new SalePayment { PaymentMethod = PaymentMethods.Cash, Amount = sale.PaidAmount } }
            : Array.Empty<SalePayment>();
        return RegisterSale(sale, items, payments, dueDate, sale.ChangeAmount + sale.PaidAmount);
    }

    public Sale RegisterSale(Sale sale, IEnumerable<SaleItem> items, IEnumerable<SalePayment> payments, DateTime? dueDate = null, double cashTendered = 0)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(payments);
        if (_database.Connection.Find<Sale>(sale.Id) is not null)
            throw new InvalidOperationException("Esta venta ya fue confirmada y no puede registrarse dos veces.");
        var customer = _customers.GetCustomer(sale.CustomerId) ?? _customers.GetGeneralCustomer();
        sale.CustomerId = customer.Id;
        sale.CustomerName = customer.Name;
        var normalizedItems = NormalizeItems(sale.Id, items);
        if (normalizedItems.Count == 0)
            throw new InvalidOperationException("La venta debe tener al menos un producto.");
        if (sale.Discount < 0 || double.IsNaN(sale.Discount) || double.IsInfinity(sale.Discount))
            throw new InvalidOperationException("El descuento debe ser válido y no negativo.");

        sale.Subtotal = normalizedItems.Sum(item => item.LineTotal);
        var maxDiscount = _session.CurrentUser?.Role == "Administrador" ? sale.Subtotal : sale.Subtotal * 0.20;
        if (sale.Discount > maxDiscount + 0.001)
            throw new InvalidOperationException($"El descuento supera el límite autorizado ({maxDiscount:0.##}).");
        sale.Total = Math.Max(0, sale.Subtotal - sale.Discount);
        if (sale.Discount > 0)
            _permissions.Require(AppPermission.ApplyDiscounts);
        var normalizedPayments = NormalizePayments(sale.Id, payments);
        sale.PaidAmount = Math.Round(normalizedPayments.Sum(payment => payment.Amount), 2);
        if (sale.PaidAmount > sale.Total + 0.001)
            throw new InvalidOperationException("Los pagos no pueden superar el total de la venta.");
        sale.CreditAmount = Math.Max(0, sale.Total - sale.PaidAmount);
        if (sale.CreditAmount > 0)
        {
            _permissions.Require(AppPermission.GrantCredits);
            if (customer.IsGeneral)
                throw new InvalidOperationException("Las ventas a crédito requieren un cliente identificado.");
            if (customer.IsBlocked)
                throw new InvalidOperationException("El cliente está bloqueado para nuevas ventas a crédito.");
            var currentBalance = _customers.GetBalanceDue(customer.Id);
            if (customer.CreditLimit > 0 && currentBalance + sale.CreditAmount > customer.CreditLimit)
                throw new InvalidOperationException($"El crédito supera el límite disponible del cliente (${Math.Max(0, customer.CreditLimit - currentBalance):N0}).");
            var expiry = (dueDate ?? DateTime.UtcNow.Date.AddDays(30)).Date;
            if (expiry < DateTime.UtcNow.Date)
                throw new InvalidOperationException("La fecha de vencimiento no puede estar en el pasado.");
            sale.Notes = $"Vencimiento: {expiry:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(sale.Notes) ? string.Empty : $". {sale.Notes.Trim()}");
        }
        else if (sale.PaidAmount <= 0)
        {
            throw new InvalidOperationException("Una venta debe tener un pago o generar un crédito.");
        }

        var cashApplied = normalizedPayments.Where(payment => payment.PaymentMethod == PaymentMethods.Cash).Sum(payment => payment.Amount);
        if (cashTendered <= 0)
            cashTendered = cashApplied;
        if (cashTendered < cashApplied)
            throw new InvalidOperationException("El efectivo recibido no puede ser menor al efectivo aplicado.");
        if (sale.CreditAmount > 0 && cashTendered > cashApplied + 0.001)
            throw new InvalidOperationException("Una venta a crédito no puede registrar cambio; aplica el efectivo exacto.");
        sale.ChangeAmount = sale.CreditAmount == 0 ? Math.Max(0, cashTendered - cashApplied) : 0;

        var cashSession = _cash.GetOpenSession();
        if (cashSession is null)
            throw new InvalidOperationException("Abre la caja antes de registrar una venta.");

        sale.Status = SaleStatuses.Completed;
        sale.CashSessionId = cashSession.Id;
        sale.SaleDateUtc = DateTime.UtcNow;
        sale.UpdatedAtUtc = sale.SaleDateUtc;
        sale.Notes = sale.Notes.Trim();

        _database.RunInTransaction(() =>
        {
            _inventory.RegisterSaleExits(normalizedItems.Select(item => new InventoryQuantityEntry(item.ProductId, item.Quantity)), sale.Id);
            sale.CreatedAtUtc = sale.UpdatedAtUtc;
            _database.Connection.Insert(sale);
            foreach (var item in normalizedItems)
                _database.Connection.Insert(item);
            foreach (var payment in normalizedPayments)
                _database.Connection.Insert(payment);

            if (sale.CreditAmount > 0)
            {
                var credit = new Credit
                {
                    SaleId = sale.Id,
                    CustomerId = sale.CustomerId,
                    OriginalAmount = sale.CreditAmount,
                    BalanceDue = sale.CreditAmount,
                    DueDateUtc = dueDate?.Date ?? DateTime.UtcNow.Date.AddDays(30),
                    Status = CreditStatuses.Active,
                    Notes = sale.Notes,
                    CreatedAtUtc = sale.SaleDateUtc,
                    UpdatedAtUtc = sale.SaleDateUtc
                };
                _database.Connection.Insert(credit);
                _database.Connection.Insert(new CreditMovement
                {
                    CreditId = credit.Id,
                    MovementType = CreditMovementTypes.Origin,
                    Amount = credit.OriginalAmount,
                    BalanceBefore = 0,
                    BalanceAfter = credit.BalanceDue,
                    ReferenceId = sale.Id,
                    Notes = "Crédito originado por venta",
                    UserId = _session.CurrentUser?.Id,
                    CreatedAtUtc = sale.SaleDateUtc
                });
            }

            foreach (var payment in normalizedPayments)
                _cash.RegisterIncome(payment.Amount, CashMovementTypes.Sale, "Venta", sale.Id, payment.PaymentMethod, payment.PaymentMethod);
            _audit.Record("Crear", nameof(Sale), sale.Id, $"Total: {sale.Total:0.##}; crédito: {sale.CreditAmount:0.##}");
        });
        return sale;
    }

    public void CancelSale(string saleId)
    {
        _permissions.Require(AppPermission.CancelSales);
        var sale = GetSale(saleId) ?? throw new InvalidOperationException("Venta no encontrada.");
        if (sale.Status != SaleStatuses.Completed)
            throw new InvalidOperationException("La venta ya fue anulada o devuelta.");
        var credit = _database.Connection.Table<Credit>().ToList().FirstOrDefault(item => item.SaleId == sale.Id);
        if (credit is not null && credit.BalanceDue > 0)
            throw new InvalidOperationException("La venta con crédito tiene saldo pendiente; regularízalo desde cartera antes de anularla.");
        var items = GetItems(sale.Id);
        foreach (var item in items)
        {
            var product = _database.Connection.Find<Product>(item.ProductId);
            if (product is null || !product.IsActive)
                throw new InvalidOperationException("No se puede devolver un producto inactivo o inexistente.");
            // CustomerReturn aumenta existencias; la validación de producto activo se hace antes de la primera línea.
        }
        var refundAmount = credit is null ? sale.PaidAmount : sale.Total;
        if (refundAmount > 0 && _cash.GetOpenSession() is null)
            throw new InvalidOperationException("Abre la caja para registrar el reembolso.");
        _database.RunInTransaction(() =>
        {
            foreach (var item in items)
                _inventory.RegisterCustomerReturn(item.ProductId, item.Quantity, sale.Id, "Devolución de venta");
            if (refundAmount > 0)
                _cash.RegisterOutcome(refundAmount, CashMovementTypes.Refund, "Venta", sale.Id, "Reembolso por anulación", PaymentMethods.Cash);
            if (credit is not null)
            {
                credit.Status = CreditStatuses.Cancelled;
                credit.BalanceDue = 0;
                credit.UpdatedAtUtc = DateTime.UtcNow;
                _database.Connection.Update(credit);
                _database.Connection.Insert(new CreditMovement
                {
                    CreditId = credit.Id,
                    MovementType = CreditMovementTypes.Cancellation,
                    Amount = 0,
                    BalanceBefore = 0,
                    BalanceAfter = 0,
                    ReferenceId = sale.Id,
                    Notes = "Crédito anulado junto con la devolución de la venta",
                    UserId = _session.CurrentUser?.Id,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            sale.Status = SaleStatuses.Cancelled;
            sale.ReturnedAmount = sale.Total;
            sale.UpdatedAtUtc = DateTime.UtcNow;
            _database.Connection.Update(sale);
            _audit.Record("Anular", nameof(Sale), sale.Id, "Venta devuelta y reembolsada");
        });
    }

    public void ReturnSale(string saleId) => CancelSale(saleId);

    public string CreateSaleReceipt(Sale sale)
    {
        ArgumentNullException.ThrowIfNull(sale);
        var directory = Path.Combine(FileSystem.AppDataDirectory, "receipts");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"venta-{sale.Id}.txt");
        var items = GetItems(sale.Id);
        var payments = GetPayments(sale.Id);
        var content = $"COMPROBANTE DE VENTA\n" +
                      $"Fecha: {sale.SaleDateUtc.ToLocalTime():dd/MM/yyyy HH:mm}\n" +
                      $"Cliente: {sale.CustomerName}\n\n" +
                      string.Join("\n", items.Select(item => $"{item.ProductName} · {item.LineLabel}")) + "\n\n" +
                      $"Total: ${sale.Total:N0}\n" +
                      string.Join("\n", payments.Select(payment => payment.AmountLabel)) + "\n" +
                      $"Crédito: ${sale.CreditAmount:N0}\n" +
                      $"Cambio: ${sale.ChangeAmount:N0}\n";
        File.WriteAllText(path, content);
        return path;
    }

    private static List<SalePayment> NormalizePayments(string saleId, IEnumerable<SalePayment> payments)
    {
        var normalized = new List<SalePayment>();
        foreach (var payment in payments)
        {
            if (!PaymentMethods.All.Contains(payment.PaymentMethod))
                throw new InvalidOperationException("El método de pago no es válido.");
            if (payment.Amount <= 0 || double.IsNaN(payment.Amount) || double.IsInfinity(payment.Amount))
                throw new InvalidOperationException("Cada pago debe ser mayor que cero.");
            normalized.Add(new SalePayment
            {
                Id = string.IsNullOrWhiteSpace(payment.Id) ? Guid.NewGuid().ToString("N") : payment.Id,
                SaleId = saleId,
                PaymentMethod = payment.PaymentMethod,
                Amount = Math.Round(payment.Amount, 2),
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        return normalized;
    }

    private List<SaleItem> NormalizeItems(string saleId, IEnumerable<SaleItem> items)
    {
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id);
        var normalized = new List<SaleItem>();
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
                throw new InvalidOperationException("Uno de los productos no existe o está inactivo.");
            if (item.Quantity <= 0 || double.IsNaN(item.Quantity) || double.IsInfinity(item.Quantity))
                throw new InvalidOperationException("Las cantidades deben ser mayores que cero.");
            if (item.UnitPrice < 0 || double.IsNaN(item.UnitPrice) || double.IsInfinity(item.UnitPrice))
                throw new InvalidOperationException("Los precios deben ser válidos y no negativos.");
            if (item.Discount < 0 || item.Discount > item.Quantity * item.UnitPrice)
                throw new InvalidOperationException("El descuento de una línea no es válido.");
            normalized.Add(new SaleItem
            {
                Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
                SaleId = saleId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                LineTotal = Math.Max(0, item.Quantity * item.UnitPrice - item.Discount)
            });
        }
        return normalized;
    }
}
