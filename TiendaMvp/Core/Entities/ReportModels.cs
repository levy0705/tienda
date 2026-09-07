namespace TiendaMvp.Core.Entities;

public static class ReportTypes
{
    public const string SalesByPeriod = "Ventas por periodo";
    public const string SalesByProduct = "Ventas por producto";
    public const string GrossProfit = "Utilidad bruta estimada";
    public const string CurrentInventory = "Inventario actual";
    public const string InventoryValue = "Valor del inventario";
    public const string InventoryMovements = "Movimientos de inventario";
    public const string PurchasesBySupplier = "Compras por proveedor";
    public const string ExpensesByCategory = "Gastos por categoría";
    public const string CashClosures = "Cierres de caja";
    public const string PendingCredits = "Créditos pendientes";
    public const string OverdueCredits = "Créditos vencidos";
    public const string CreditPayments = "Abonos recibidos";
    public const string LowStockProducts = "Productos con bajas existencias";

    public static readonly IReadOnlyList<string> All =
    [
        SalesByPeriod,
        SalesByProduct,
        GrossProfit,
        CurrentInventory,
        InventoryValue,
        InventoryMovements,
        PurchasesBySupplier,
        ExpensesByCategory,
        CashClosures,
        PendingCredits,
        OverdueCredits,
        CreditPayments,
        LowStockProducts
    ];
}

public sealed class ReportRow
{
    public string Label { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime? DateUtc { get; set; }
    public double Quantity { get; set; }
    public double Amount { get; set; }
    public double SecondaryAmount { get; set; }
    public string AmountCaption { get; set; } = "Valor";
    public string SecondaryCaption { get; set; } = "";
}

public sealed class ReportResult
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<ReportRow> Rows { get; set; } = Array.Empty<ReportRow>();
}

public sealed class DashboardSummary
{
    public double SalesToday { get; set; }
    public double CollectionsToday { get; set; }
    public double ExpensesToday { get; set; }
    public double ExpectedCash { get; set; }
    public double PendingCredits { get; set; }
    public double OverdueCredits { get; set; }
    public int OverdueCreditCount { get; set; }
    public int OutOfStockProducts { get; set; }
    public int LowStockProducts { get; set; }
    public IReadOnlyList<ReportRow> TopProducts { get; set; } = Array.Empty<ReportRow>();
}
