using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IProductImportService
{
    ProductImportDocument ParseCsv(string content);
    ProductImportResult Import(IReadOnlyList<ProductImportRow> rows);
    string CreateTemplateCsv();
}
