namespace TiendaMvp.Core.Entities;

public sealed class ProductImportRow
{
    public int LineNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "unidad";
    public double PurchaseCost { get; set; }
    public double SalePrice { get; set; }
    public double InitialStock { get; set; }
    public double MinimumStock { get; set; }
    public double TaxRate { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
    public string StatusLabel => IsValid ? "Listo para importar" : string.Join(" ", Errors);
    public string AmountsLabel => $"Venta: ${SalePrice:0.##} · Inicial: {InitialStock:0.##}";
}

public sealed class ProductImportDocument
{
    public IReadOnlyList<ProductImportRow> Rows { get; init; } = Array.Empty<ProductImportRow>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public bool IsValid => Errors.Count == 0 && Rows.Count > 0 && Rows.All(row => row.IsValid);
    public int ValidRows => Rows.Count(row => row.IsValid);
}

public sealed class ProductImportResult
{
    public int CreatedCount { get; init; }
    public int InitialInventoryCount { get; init; }
    public IReadOnlyList<string> GeneratedCodes { get; init; } = Array.Empty<string>();
}
