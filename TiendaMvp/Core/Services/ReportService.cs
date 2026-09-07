using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class ReportService : IReportService
{
    private readonly ILocalDatabase _database;
    private readonly ICashService _cash;
    private readonly IPermissionService _permissions;

    public ReportService(ILocalDatabase database, ICashService cash, IPermissionService permissions)
    {
        _database = database;
        _cash = cash;
        _permissions = permissions;
    }

    public DashboardSummary GetDashboardSummary()
    {
        var today = DateTime.Today;
        var sales = _database.Connection.Table<Sale>().ToList();
        var completedToday = sales.Where(sale => sale.Status == SaleStatuses.Completed && sale.SaleDateUtc.ToLocalTime().Date == today).ToList();
        var saleIds = completedToday.Select(sale => sale.Id).ToHashSet();
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id);
        var items = _database.Connection.Table<SaleItem>().ToList().Where(item => saleIds.Contains(item.SaleId)).ToList();
        var movementsToday = _database.Connection.Table<CashMovement>().ToList().Where(movement => movement.CreatedAtUtc.ToLocalTime().Date == today).ToList();
        var expensesToday = _database.Connection.Table<Expense>().ToList().Where(expense => expense.ExpenseDateUtc.ToLocalTime().Date == today).ToList();
        var credits = _database.Connection.Table<Credit>().ToList().Where(credit => credit.Status != CreditStatuses.Cancelled).ToList();
        var lowStock = _database.Connection.Table<Product>().ToList().Where(product => product.IsActive && product.MinimumStock > 0 && product.Stock <= product.MinimumStock).ToList();

        var topProducts = items.GroupBy(item => item.ProductId)
            .Select(group => new ReportRow
            {
                Label = products.TryGetValue(group.Key, out var product) ? product.Name : "Producto eliminado",
                Quantity = group.Sum(item => item.Quantity),
                Amount = group.Sum(item => item.LineTotal),
                AmountCaption = "Ventas",
                Detail = $"{group.Sum(item => item.Quantity):0.##} unidad(es) · ${group.Sum(item => item.LineTotal):N0}"
            })
            .OrderByDescending(row => row.Quantity)
            .Take(5)
            .ToList();

        var openSession = _cash.GetOpenSession();
        return new DashboardSummary
        {
            SalesToday = completedToday.Sum(sale => sale.Total),
            CollectionsToday = movementsToday.Where(movement => (movement.MovementType == CashMovementTypes.Sale || movement.MovementType == CashMovementTypes.CreditPayment) && movement.Amount > 0).Sum(movement => movement.Amount),
            ExpensesToday = expensesToday.Sum(expense => expense.Amount),
            ExpectedCash = openSession is null ? 0 : _cash.GetExpectedAmount(openSession.Id),
            PendingCredits = credits.Where(credit => credit.BalanceDue > 0).Sum(credit => credit.BalanceDue),
            OverdueCredits = credits.Where(IsOverdue).Sum(credit => credit.BalanceDue),
            OverdueCreditCount = credits.Count(IsOverdue),
            OutOfStockProducts = _database.Connection.Table<Product>().ToList().Count(product => product.IsActive && product.Stock <= 0),
            LowStockProducts = lowStock.Count,
            TopProducts = topProducts
        };
    }

    public ReportResult GetReport(string reportType, DateTime fromLocalDate, DateTime toLocalDate)
    {
        _permissions.Require(AppPermission.ConsultReports);
        if (!ReportTypes.All.Contains(reportType))
            throw new InvalidOperationException("El tipo de reporte no es válido.");
        if (reportType is ReportTypes.SalesByProduct or ReportTypes.GrossProfit or ReportTypes.CurrentInventory or ReportTypes.InventoryValue or ReportTypes.PurchasesBySupplier)
            _permissions.Require(AppPermission.ViewCostsAndProfit);
        var (fromUtc, toUtc) = NormalizeRange(fromLocalDate, toLocalDate);
        return reportType switch
        {
            ReportTypes.SalesByPeriod => SalesByPeriod(fromUtc, toUtc),
            ReportTypes.SalesByProduct => SalesByProduct(fromUtc, toUtc),
            ReportTypes.GrossProfit => GrossProfit(fromUtc, toUtc),
            ReportTypes.CurrentInventory => CurrentInventory(),
            ReportTypes.InventoryValue => InventoryValue(),
            ReportTypes.InventoryMovements => InventoryMovements(fromUtc, toUtc),
            ReportTypes.PurchasesBySupplier => PurchasesBySupplier(fromUtc, toUtc),
            ReportTypes.ExpensesByCategory => ExpensesByCategory(fromUtc, toUtc),
            ReportTypes.CashClosures => CashClosures(fromUtc, toUtc),
            ReportTypes.PendingCredits => Credits(fromUtc, toUtc, overdueOnly: false),
            ReportTypes.OverdueCredits => Credits(fromUtc, toUtc, overdueOnly: true),
            ReportTypes.CreditPayments => CreditPayments(fromUtc, toUtc),
            ReportTypes.LowStockProducts => LowStockProducts(),
            _ => throw new InvalidOperationException("El tipo de reporte no es válido.")
        };
    }

    private ReportResult SalesByPeriod(DateTime fromUtc, DateTime toUtc)
    {
        var customers = _database.Connection.Table<Customer>().ToList().ToDictionary(customer => customer.Id, customer => customer.Name);
        var rows = _database.Connection.Table<Sale>().ToList()
            .Where(sale => sale.Status == SaleStatuses.Completed && InRange(sale.SaleDateUtc, fromUtc, toUtc))
            .OrderByDescending(sale => sale.SaleDateUtc)
            .Select(sale => new ReportRow
            {
                Label = customers.TryGetValue(sale.CustomerId, out var customer) ? customer : "Cliente general",
                DateUtc = sale.SaleDateUtc,
                Amount = sale.Total,
                AmountCaption = "Total",
                Detail = $"{sale.SaleDateUtc.ToLocalTime():dd/MM/yyyy HH:mm} · Pagado ${sale.PaidAmount:N0} · Crédito ${sale.CreditAmount:N0}"
            })
            .ToList();
        return Result(ReportTypes.SalesByPeriod, $"{rows.Count} venta(s) · Total ${rows.Sum(row => row.Amount):N0}", rows);
    }

    private ReportResult SalesByProduct(DateTime fromUtc, DateTime toUtc)
    {
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id);
        var sales = _database.Connection.Table<Sale>().ToList().Where(sale => sale.Status == SaleStatuses.Completed && InRange(sale.SaleDateUtc, fromUtc, toUtc)).ToList();
        var saleIds = sales.Select(sale => sale.Id).ToHashSet();
        var items = _database.Connection.Table<SaleItem>().ToList().Where(item => saleIds.Contains(item.SaleId)).ToList();
        var saleLookup = sales.ToDictionary(sale => sale.Id);
        var netLines = items.Select(item =>
        {
            saleLookup.TryGetValue(item.SaleId, out var sale);
            var discountShare = sale is not null && sale.Subtotal > 0 ? sale.Discount * item.LineTotal / sale.Subtotal : 0;
            return new
            {
                item.ProductId,
                item.Quantity,
                Revenue = Math.Max(0, item.LineTotal - discountShare),
                Cost = item.Quantity * (products.TryGetValue(item.ProductId, out var product) ? product.PurchaseCost : 0)
            };
        }).ToList();
        var rows = netLines.GroupBy(item => item.ProductId).Select(group =>
        {
            var revenue = group.Sum(item => item.Revenue);
            var cost = group.Sum(item => item.Cost);
            var quantity = group.Sum(item => item.Quantity);
            return new ReportRow
            {
                Label = products.TryGetValue(group.Key, out var product) ? product.Name : "Producto eliminado",
                Quantity = quantity,
                Amount = revenue,
                SecondaryAmount = revenue - cost,
                AmountCaption = "Ventas",
                SecondaryCaption = "Utilidad",
                Detail = $"{quantity:0.##} unidad(es) · Utilidad estimada ${revenue - cost:N0}"
            };
        }).OrderByDescending(row => row.Amount).ToList();
        return Result(ReportTypes.SalesByProduct, $"${rows.Sum(row => row.Amount):N0} en ventas · Utilidad ${rows.Sum(row => row.SecondaryAmount):N0}", rows);
    }

    private ReportResult GrossProfit(DateTime fromUtc, DateTime toUtc)
    {
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id);
        var sales = _database.Connection.Table<Sale>().ToList().Where(sale => sale.Status == SaleStatuses.Completed && InRange(sale.SaleDateUtc, fromUtc, toUtc)).ToList();
        var items = _database.Connection.Table<SaleItem>().ToList().Where(item => sales.Any(sale => sale.Id == item.SaleId)).ToList();
        var gross = sales.Sum(sale => sale.Total) - items.Sum(item => item.Quantity * (products.TryGetValue(item.ProductId, out var product) ? product.PurchaseCost : 0));
        var rows = new List<ReportRow>
        {
            new() { Label = "Ventas netas", Amount = sales.Sum(sale => sale.Total), Detail = $"{sales.Count} venta(s) completada(s)" },
            new() { Label = "Costo estimado", Amount = items.Sum(item => item.Quantity * (products.TryGetValue(item.ProductId, out var product) ? product.PurchaseCost : 0)), Detail = "Según el costo actual registrado en cada producto" },
            new() { Label = "Utilidad bruta estimada", Amount = gross, Detail = "Ventas netas menos costo estimado" }
        };
        return Result(ReportTypes.GrossProfit, $"Utilidad bruta estimada ${gross:N0}", rows);
    }

    private ReportResult CurrentInventory()
    {
        var categories = _database.Connection.Table<Category>().ToList().ToDictionary(category => category.Id, category => category.Name);
        var products = _database.Connection.Table<Product>().ToList().Where(product => product.IsActive).OrderBy(product => product.Name).ToList();
        var rows = products.Select(product => new ReportRow
        {
            Label = product.Name,
            Quantity = product.Stock,
            Amount = product.Stock * product.PurchaseCost,
            AmountCaption = "Valor",
            Detail = $"{categories.GetValueOrDefault(product.CategoryId, "Sin categoría")} · {product.Stock:0.##} {product.Unit}(s) · mínimo {product.MinimumStock:0.##}"
        }).ToList();
        return Result(ReportTypes.CurrentInventory, $"{rows.Count} producto(s) · {rows.Sum(row => row.Quantity):0.##} unidades", rows);
    }

    private ReportResult InventoryValue()
    {
        var products = _database.Connection.Table<Product>().ToList().Where(product => product.IsActive).OrderByDescending(product => product.Stock * product.PurchaseCost).ToList();
        var rows = products.Select(product => new ReportRow
        {
            Label = product.Name,
            Quantity = product.Stock,
            Amount = product.Stock * product.PurchaseCost,
            AmountCaption = "Valor",
            Detail = $"{product.Stock:0.##} {product.Unit}(s) × costo ${product.PurchaseCost:N0}"
        }).ToList();
        return Result(ReportTypes.InventoryValue, $"Valor estimado ${rows.Sum(row => row.Amount):N0}", rows);
    }

    private ReportResult InventoryMovements(DateTime fromUtc, DateTime toUtc)
    {
        var products = _database.Connection.Table<Product>().ToList().ToDictionary(product => product.Id, product => product.Name);
        var rows = _database.Connection.Table<InventoryMovement>().ToList()
            .Where(movement => InRange(movement.CreatedAtUtc, fromUtc, toUtc))
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .Select(movement => new ReportRow
            {
                Label = products.GetValueOrDefault(movement.ProductId, "Producto eliminado"),
                DateUtc = movement.CreatedAtUtc,
                Quantity = movement.Quantity,
                Amount = movement.Quantity,
                AmountCaption = "Cantidad",
                Detail = $"{movement.MovementType} · {movement.Reason} · existencia {movement.NewStock:0.##}"
            }).ToList();
        return Result(ReportTypes.InventoryMovements, $"{rows.Count} movimiento(s) · Neto {rows.Sum(row => row.Quantity):0.##}", rows);
    }

    private ReportResult PurchasesBySupplier(DateTime fromUtc, DateTime toUtc)
    {
        var suppliers = _database.Connection.Table<Supplier>().ToList().ToDictionary(supplier => supplier.Id, supplier => supplier.Name);
        var rows = _database.Connection.Table<Purchase>().ToList()
            .Where(purchase => purchase.Status != PurchaseStatuses.Cancelled && purchase.Status != PurchaseStatuses.Draft && InRange(purchase.PurchaseDateUtc, fromUtc, toUtc))
            .GroupBy(purchase => purchase.SupplierId)
            .Select(group => new ReportRow
            {
                Label = suppliers.GetValueOrDefault(group.Key, "Proveedor eliminado"),
                Amount = group.Sum(purchase => purchase.Total),
                SecondaryAmount = group.Sum(purchase => purchase.BalanceDue),
                AmountCaption = "Compras",
                SecondaryCaption = "Pendiente",
                Detail = $"{group.Count()} compra(s) · Pagado ${group.Sum(purchase => purchase.PaidAmount):N0} · Pendiente ${group.Sum(purchase => purchase.BalanceDue):N0}"
            }).OrderByDescending(row => row.Amount).ToList();
        return Result(ReportTypes.PurchasesBySupplier, $"Total compras ${rows.Sum(row => row.Amount):N0}", rows);
    }

    private ReportResult ExpensesByCategory(DateTime fromUtc, DateTime toUtc)
    {
        var rows = _database.Connection.Table<Expense>().ToList()
            .Where(expense => InRange(expense.ExpenseDateUtc, fromUtc, toUtc))
            .GroupBy(expense => expense.Category)
            .Select(group => new ReportRow
            {
                Label = group.Key,
                Amount = group.Sum(expense => expense.Amount),
                AmountCaption = "Gastos",
                Detail = $"{group.Count()} gasto(s) · {string.Join(", ", group.GroupBy(expense => expense.PaymentMethod).Select(method => $"{method.Key} ${method.Sum(expense => expense.Amount):N0}"))}"
            }).OrderByDescending(row => row.Amount).ToList();
        return Result(ReportTypes.ExpensesByCategory, $"Total gastos ${rows.Sum(row => row.Amount):N0}", rows);
    }

    private ReportResult CashClosures(DateTime fromUtc, DateTime toUtc)
    {
        var rows = _cash.GetClosedSessions().Where(session => session.ClosedAtUtc is not null && InRange(session.ClosedAtUtc.Value, fromUtc, toUtc)).Select(session => new ReportRow
        {
            Label = session.UserName,
            DateUtc = session.ClosedAtUtc,
            Amount = session.Difference,
            AmountCaption = "Diferencia",
            Detail = $"Inicial ${session.OpeningAmount:N0} · Esperado ${session.ExpectedAmount:N0} · Contado ${session.CountedAmount:N0}"
        }).ToList();
        return Result(ReportTypes.CashClosures, $"{rows.Count} cierre(s)", rows);
    }

    private ReportResult Credits(DateTime fromUtc, DateTime toUtc, bool overdueOnly)
    {
        var customers = _database.Connection.Table<Customer>().ToList().ToDictionary(customer => customer.Id, customer => customer.Name);
        var rows = _database.Connection.Table<Credit>().ToList()
            .Where(credit => credit.Status != CreditStatuses.Cancelled && credit.BalanceDue > 0 && InRange(credit.CreatedAtUtc, fromUtc, toUtc) && (!overdueOnly || IsOverdue(credit)))
            .Select(credit => new ReportRow
            {
                Label = customers.GetValueOrDefault(credit.CustomerId, "Cliente eliminado"),
                DateUtc = credit.DueDateUtc,
                Amount = credit.BalanceDue,
                AmountCaption = "Saldo",
                Detail = $"Vence {credit.DueDateUtc.ToLocalTime():dd/MM/yyyy} · Original ${credit.OriginalAmount:N0} · {credit.StatusLabel}"
            }).OrderBy(row => row.DateUtc).ToList();
        var type = overdueOnly ? ReportTypes.OverdueCredits : ReportTypes.PendingCredits;
        return Result(type, $"{rows.Count} crédito(s) · Saldo ${rows.Sum(row => row.Amount):N0}", rows);
    }

    private ReportResult CreditPayments(DateTime fromUtc, DateTime toUtc)
    {
        var credits = _database.Connection.Table<Credit>().ToList().ToDictionary(credit => credit.Id);
        var customers = _database.Connection.Table<Customer>().ToList().ToDictionary(customer => customer.Id, customer => customer.Name);
        var rows = _database.Connection.Table<CreditMovement>().ToList()
            .Where(movement => movement.MovementType == CreditMovementTypes.Payment && InRange(movement.CreatedAtUtc, fromUtc, toUtc))
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .Select(movement =>
            {
                var credit = credits.GetValueOrDefault(movement.CreditId);
                return new ReportRow
                {
                    Label = credit is null ? "Crédito eliminado" : customers.GetValueOrDefault(credit.CustomerId, "Cliente eliminado"),
                    DateUtc = movement.CreatedAtUtc,
                    Amount = Math.Abs(movement.Amount),
                    AmountCaption = "Abono",
                    Detail = $"{movement.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} · Saldo posterior ${movement.BalanceAfter:N0}"
                };
            }).ToList();
        return Result(ReportTypes.CreditPayments, $"{rows.Count} abono(s) · Total ${rows.Sum(row => row.Amount):N0}", rows);
    }

    private ReportResult LowStockProducts()
    {
        var rows = _database.Connection.Table<Product>().ToList()
            .Where(product => product.IsActive && product.MinimumStock > 0 && product.Stock <= product.MinimumStock)
            .OrderBy(product => product.Stock)
            .Select(product => new ReportRow
            {
                Label = product.Name,
                Quantity = product.Stock,
                Amount = product.Stock,
                AmountCaption = "Existencia",
                Detail = $"Mínimo {product.MinimumStock:0.##} {product.Unit}(s) · {(product.Stock <= 0 ? "Agotado" : "Bajo") }"
            }).ToList();
        return Result(ReportTypes.LowStockProducts, $"{rows.Count} producto(s) requieren reposición", rows);
    }

    private static ReportResult Result(string title, string summary, IReadOnlyList<ReportRow> rows) =>
        new() { Title = title, Summary = summary, Rows = rows };

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime fromLocalDate, DateTime toLocalDate)
    {
        var from = DateTime.SpecifyKind(fromLocalDate.Date, DateTimeKind.Unspecified);
        var to = DateTime.SpecifyKind(toLocalDate.Date.AddDays(1), DateTimeKind.Unspecified);
        if (to <= from)
            throw new InvalidOperationException("El periodo final debe ser igual o posterior al inicial.");
        return (from.ToUniversalTime(), to.ToUniversalTime());
    }

    private static bool InRange(DateTime valueUtc, DateTime fromUtc, DateTime toUtc) => valueUtc >= fromUtc && valueUtc < toUtc;

    private static bool IsOverdue(Credit credit) => credit.BalanceDue > 0 && credit.DueDateUtc.Date < DateTime.UtcNow.Date;
}
