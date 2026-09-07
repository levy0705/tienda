using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface ISupplierService
{
    IReadOnlyList<Supplier> GetSuppliers(string? query = null, bool includeInactive = true);
    Supplier? GetSupplier(string id);
    void SaveSupplier(Supplier supplier);
    void SetSupplierActive(string id, bool isActive);
    double GetBalanceDue(string supplierId);
    IReadOnlyList<string> GetRelatedProductIds(string supplierId);
}
