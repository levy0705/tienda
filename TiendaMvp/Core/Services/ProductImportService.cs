using System.Globalization;
using System.Text;
using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class ProductImportService : IProductImportService
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly ILocalDatabase _database;
    private readonly IProductCatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IPermissionService _permissions;

    public ProductImportService(
        ILocalDatabase database,
        IProductCatalogService catalog,
        IInventoryService inventory,
        IPermissionService permissions)
    {
        _database = database;
        _catalog = catalog;
        _inventory = inventory;
        _permissions = permissions;
    }

    public ProductImportDocument ParseCsv(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ProductImportDocument { Errors = new[] { "El archivo está vacío." } };

        var records = ParseRecords(content);
        if (records.Count == 0)
            return new ProductImportDocument { Errors = new[] { "No se encontraron filas en el archivo." } };

        var headers = records[0].Select(NormalizeHeader).ToList();
        var headerIndexes = headers
            .Select((header, index) => new { header, index })
            .GroupBy(item => item.header)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();
        if (!headerIndexes.ContainsKey("nombre"))
            errors.Add("Falta la columna obligatoria 'Nombre'.");
        if (!headerIndexes.ContainsKey("precioventa"))
            errors.Add("Falta la columna obligatoria 'PrecioVenta'.");
        if (errors.Count > 0)
            return new ProductImportDocument { Errors = errors };

        var rows = new List<ProductImportRow>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.Skip(1))
        {
            if (record.All(string.IsNullOrWhiteSpace))
                continue;

            var row = new ProductImportRow { LineNumber = rows.Count + 2 };
            row.Name = GetValue(record, headerIndexes, "nombre").Trim();
            row.CategoryName = GetValue(record, headerIndexes, "categoria", "category").Trim();
            if (string.IsNullOrWhiteSpace(row.CategoryName))
                row.CategoryName = "General";
            row.Description = GetValue(record, headerIndexes, "descripcion", "description").Trim();
            row.Unit = GetValue(record, headerIndexes, "unidad", "unit").Trim();
            if (string.IsNullOrWhiteSpace(row.Unit))
                row.Unit = "unidad";
            row.InternalCode = GetValue(record, headerIndexes, "codigointerno", "codigo", "code").Trim();

            ParseNumber(GetValue(record, headerIndexes, "costocompra", "costo", "purchasecost"), row, "CostoCompra", value => row.PurchaseCost = value);
            ParseNumber(GetValue(record, headerIndexes, "precioventa", "precio", "saleprice"), row, "PrecioVenta", value => row.SalePrice = value);
            ParseNumber(GetValue(record, headerIndexes, "existenciainicial", "existencia", "stock", "initialstock"), row, "ExistenciaInicial", value => row.InitialStock = value);
            ParseNumber(GetValue(record, headerIndexes, "existenciaminima", "minimo", "minimumstock"), row, "ExistenciaMinima", value => row.MinimumStock = value);
            ParseNumber(GetValue(record, headerIndexes, "impuesto", "impuestoporcentaje", "tax", "taxrate"), row, "Impuesto", value => row.TaxRate = value);

            var activeValue = GetValue(record, headerIndexes, "activo", "estado", "active");
            if (!string.IsNullOrWhiteSpace(activeValue))
            {
                if (TryParseBoolean(activeValue, out var isActive))
                    row.IsActive = isActive;
                else
                    row.Errors.Add("Activo debe ser Si/No.");
            }

            ValidateRow(row, usedCodes);
            if (!string.IsNullOrWhiteSpace(row.InternalCode))
                usedCodes.Add(row.InternalCode);
            rows.Add(row);
        }

        if (rows.Count == 0)
            errors.Add("El archivo no contiene productos.");

        return new ProductImportDocument { Rows = rows, Errors = errors };
    }

    public ProductImportResult Import(IReadOnlyList<ProductImportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
            throw new InvalidOperationException("No hay productos para importar.");
        if (rows.Any(row => !row.IsValid))
            throw new InvalidOperationException("Corrige los errores del archivo antes de importar.");

        _permissions.Require(AppPermission.AdjustInventory);

        var existingCodes = _database.Connection.Table<Product>()
            .ToList()
            .Select(product => product.InternalCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(row => !string.IsNullOrWhiteSpace(row.InternalCode)))
        {
            if (!existingCodes.Add(row.InternalCode))
                throw new InvalidOperationException($"La fila {row.LineNumber} usa un código interno que ya existe: {row.InternalCode}.");
        }

        var categories = _catalog.GetCategories(includeInactive: true)
            .ToDictionary(category => category.Name, StringComparer.OrdinalIgnoreCase);
        var generatedCodes = new List<string>();
        var createdCount = 0;
        var initialInventoryCount = 0;

        _database.RunInTransaction(() =>
        {
            foreach (var row in rows)
            {
                if (!categories.TryGetValue(row.CategoryName, out var category))
                {
                    category = new Category { Name = row.CategoryName };
                    _catalog.SaveCategory(category);
                    categories[category.Name] = category;
                }

                var product = new Product
                {
                    InternalCode = row.InternalCode,
                    Name = row.Name,
                    Description = row.Description,
                    CategoryId = category.Id,
                    Unit = row.Unit,
                    PurchaseCost = row.PurchaseCost,
                    SalePrice = row.SalePrice,
                    MinimumStock = row.MinimumStock,
                    TaxRate = row.TaxRate,
                    IsActive = row.IsActive
                };

                _catalog.SaveProduct(product);
                generatedCodes.Add(product.InternalCode);
                createdCount++;

                if (row.InitialStock > 0)
                {
                    _inventory.RegisterInitialInventory(product.Id, row.InitialStock);
                    initialInventoryCount++;
                }
            }
        });

        return new ProductImportResult
        {
            CreatedCount = createdCount,
            InitialInventoryCount = initialInventoryCount,
            GeneratedCodes = generatedCodes
        };
    }

    public string CreateTemplateCsv() =>
        "Nombre,Categoría,Descripción,Unidad,CostoCompra,PrecioVenta,ExistenciaInicial,ExistenciaMinima,Impuesto,CodigoInterno,Activo\r\n" +
        "Ejemplo de producto,General,Descripción opcional,unidad,3500,4800,20,5,0,,Si\r\n";

    private static void ValidateRow(ProductImportRow row, HashSet<string> usedCodes)
    {
        if (string.IsNullOrWhiteSpace(row.Name))
            row.Errors.Add("Nombre obligatorio.");
        if (row.SalePrice < 0)
            row.Errors.Add("PrecioVenta no puede ser negativo.");
        if (row.PurchaseCost < 0)
            row.Errors.Add("CostoCompra no puede ser negativo.");
        if (row.InitialStock < 0)
            row.Errors.Add("ExistenciaInicial no puede ser negativa.");
        if (row.MinimumStock < 0)
            row.Errors.Add("ExistenciaMinima no puede ser negativa.");
        if (row.TaxRate < 0)
            row.Errors.Add("Impuesto no puede ser negativo.");
        if (!string.IsNullOrWhiteSpace(row.InternalCode) && !usedCodes.Add(row.InternalCode))
            row.Errors.Add($"Código interno repetido: {row.InternalCode}.");
    }

    private static void ParseNumber(string value, ProductImportRow row, string fieldName, Action<double> assign)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (TryParseNumber(value, out var number))
        {
            assign(number);
            return;
        }
        row.Errors.Add($"{fieldName} no es válido.");
    }

    private static bool TryParseNumber(string value, out double number)
    {
        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.CurrentCulture, out number)
            || double.TryParse(normalized, NumberStyles.Any, Invariant, out number))
            return true;

        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            normalized = normalized.LastIndexOf(',') > normalized.LastIndexOf('.')
                ? normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : normalized.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (normalized.Contains(','))
        {
            normalized = normalized.Replace(',', '.');
        }

        return double.TryParse(normalized, NumberStyles.Any, Invariant, out number);
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "si" or "sí" or "yes" or "true" or "1" or "activo")
        {
            result = true;
            return true;
        }
        if (normalized is "no" or "false" or "0" or "inactivo")
        {
            result = false;
            return true;
        }
        result = true;
        return false;
    }

    private static string GetValue(IReadOnlyList<string> record, IReadOnlyDictionary<string, int> indexes, params string[] names)
    {
        foreach (var name in names)
        {
            if (indexes.TryGetValue(name, out var index) && index < record.Count)
                return record[index];
        }
        return string.Empty;
    }

    private static string NormalizeHeader(string value)
    {
        var normalized = value.Trim().Trim('\ufeff').ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                builder.Append(character);
        }
        return builder.ToString();
    }

    private static List<List<string>> ParseRecords(string content)
    {
        var firstLine = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var delimiter = firstLine.Count(character => character == ';') > firstLine.Count(character => character == ',') ? ';' : ',';
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (quoted && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == delimiter && !quoted)
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                    index++;
                record.Add(field.ToString());
                field.Clear();
                if (record.Any(value => !string.IsNullOrWhiteSpace(value)))
                    records.Add(record);
                record = new List<string>();
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            if (record.Any(value => !string.IsNullOrWhiteSpace(value)))
                records.Add(record);
        }

        return records;
    }
}
