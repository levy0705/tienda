using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Utilities;

namespace TiendaMvp.Core.Services;

public sealed class ProductCatalogService : IProductCatalogService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<Category> _categories;
    private readonly IQrCodeService _qrCodes;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;

    public ProductCatalogService(
        IRepository<Product> products,
        IRepository<Category> categories,
        IQrCodeService qrCodes,
        IAuditService audit,
        IPermissionService permissions)
    {
        _products = products;
        _categories = categories;
        _qrCodes = qrCodes;
        _audit = audit;
        _permissions = permissions;
    }

    public IReadOnlyList<Product> GetProducts(string? query = null, string? categoryId = null, bool includeInactive = false)
    {
        var categoryNames = _categories.GetAll().ToDictionary(category => category.Id, category => category.Name);
        return _products.GetAll()
            .Where(product => includeInactive || product.IsActive)
            .Where(product => string.IsNullOrWhiteSpace(categoryId) || product.CategoryId == categoryId)
            .Where(product => TextSearch.Contains(product.Name, query) || TextSearch.Contains(product.InternalCode, query) || TextSearch.Contains(product.QrValue, query))
            .OrderBy(product => TextSearch.Normalize(product.Name))
            .Select(product =>
            {
                product.CategoryName = product.CategoryId is not null && categoryNames.TryGetValue(product.CategoryId, out var name) ? name : "Sin categoría";
                return product;
            })
            .ToList();
    }

    public IReadOnlyList<Category> GetCategories(bool includeInactive = false) =>
        _categories.GetAll()
            .Where(category => includeInactive || category.IsActive)
            .OrderBy(category => TextSearch.Normalize(category.Name))
            .ToList();

    public Product? GetProduct(string id) => _products.GetById(id);

    public Product? FindByQr(string qrValue) =>
        _products.GetAll().FirstOrDefault(product => product.IsActive && product.QrValue.Equals(qrValue, StringComparison.Ordinal));

    public void SaveProduct(Product product)
    {
        ValidateProduct(product);
        var existing = _products.GetById(product.Id);
        var isNew = existing is null;

        if (existing is not null
            && (Math.Abs(existing.PurchaseCost - product.PurchaseCost) > 0.0001
                || Math.Abs(existing.SalePrice - product.SalePrice) > 0.0001))
            _permissions.Require(AppPermission.ChangePrices);

        if (string.IsNullOrWhiteSpace(product.QrValue))
            product.QrValue = _qrCodes.CreateValue(product.Id);

        if (_products.GetAll().Any(item => item.Id != product.Id && item.InternalCode.Equals(product.InternalCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Ya existe un producto con ese código interno.");

        if (_products.GetAll().Any(item => item.Id != product.Id && item.QrValue.Equals(product.QrValue, StringComparison.Ordinal)))
            throw new InvalidOperationException("El identificador QR ya está asociado a otro producto.");

        product.UpdatedAtUtc = DateTime.UtcNow;
        if (isNew)
        {
            product.CreatedAtUtc = DateTime.UtcNow;
            // El saldo inicial se registra mediante InventoryService para conservar trazabilidad.
            product.Stock = 0;
            _products.Insert(product);
        }
        else
        {
            // Los cambios de existencias pasan exclusivamente por InventoryService.
            product.Stock = existing!.Stock;
            _products.Update(product);
        }

        _audit.Record(isNew ? "Crear" : "Actualizar", nameof(Product), product.Id, product.Name);
    }

    public void SetProductActive(string id, bool isActive)
    {
        var product = _products.GetById(id) ?? throw new InvalidOperationException("Producto no encontrado.");
        product.IsActive = isActive;
        product.UpdatedAtUtc = DateTime.UtcNow;
        _products.Update(product);
        _audit.Record(isActive ? "Activar" : "Desactivar", nameof(Product), product.Id, product.Name);
    }

    public void SaveCategory(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
            throw new InvalidOperationException("El nombre de la categoría es obligatorio.");

        category.Name = category.Name.Trim();
        if (_categories.GetAll().Any(item => item.Id != category.Id && item.Name.Equals(category.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Ya existe una categoría con ese nombre.");

        var existing = _categories.GetById(category.Id);
        category.UpdatedAtUtc = DateTime.UtcNow;
        if (existing is null)
        {
            category.CreatedAtUtc = DateTime.UtcNow;
            _categories.Insert(category);
        }
        else
        {
            _categories.Update(category);
        }

        _audit.Record(existing is null ? "Crear" : "Actualizar", nameof(Category), category.Id, category.Name);
    }

    public void SetCategoryActive(string id, bool isActive)
    {
        var category = _categories.GetById(id) ?? throw new InvalidOperationException("Categoría no encontrada.");
        category.IsActive = isActive;
        category.UpdatedAtUtc = DateTime.UtcNow;
        _categories.Update(category);
        _audit.Record(isActive ? "Activar" : "Desactivar", nameof(Category), category.Id, category.Name);
    }

    public void DeleteCategory(string id)
    {
        if (_products.GetAll().Any(product => product.CategoryId == id))
            throw new InvalidOperationException("No se puede eliminar una categoría que tiene productos asociados. Puedes desactivarla.");

        var category = _categories.GetById(id) ?? throw new InvalidOperationException("Categoría no encontrada.");
        _categories.Delete(category);
        _audit.Record("Eliminar", nameof(Category), category.Id, category.Name);
    }

    private static void ValidateProduct(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            throw new InvalidOperationException("El nombre del producto es obligatorio.");
        if (string.IsNullOrWhiteSpace(product.InternalCode))
            throw new InvalidOperationException("El código interno es obligatorio.");
        if (product.PurchaseCost < 0 || product.SalePrice < 0 || product.Stock < 0 || product.MinimumStock < 0)
            throw new InvalidOperationException("Los valores de precios y existencias no pueden ser negativos.");
        if (string.IsNullOrWhiteSpace(product.Unit))
            throw new InvalidOperationException("La unidad de medida es obligatoria.");

        product.Name = product.Name.Trim();
        product.InternalCode = product.InternalCode.Trim();
        product.Unit = product.Unit.Trim();
    }
}
