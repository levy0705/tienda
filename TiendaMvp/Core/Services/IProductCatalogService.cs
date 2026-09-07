using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IProductCatalogService
{
    IReadOnlyList<Product> GetProducts(string? query = null, string? categoryId = null, bool includeInactive = false);
    IReadOnlyList<Category> GetCategories(bool includeInactive = false);
    Product? GetProduct(string id);
    Product? FindByQr(string qrValue);
    void SaveProduct(Product product);
    void SetProductActive(string id, bool isActive);
    void SaveCategory(Category category);
    void SetCategoryActive(string id, bool isActive);
    void DeleteCategory(string id);
}
