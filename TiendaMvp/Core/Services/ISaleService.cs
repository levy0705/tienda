using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface ISaleService
{
    IReadOnlyList<Sale> GetSales(string? customerId = null);
    IReadOnlyList<SaleItem> GetItems(string saleId);
    IReadOnlyList<SalePayment> GetPayments(string saleId);
    Sale? GetSale(string id);
    Sale RegisterSale(Sale sale, IEnumerable<SaleItem> items, DateTime? dueDate = null);
    Sale RegisterSale(Sale sale, IEnumerable<SaleItem> items, IEnumerable<SalePayment> payments, DateTime? dueDate = null, double cashTendered = 0);
    void CancelSale(string saleId);
    void ReturnSale(string saleId);
    string CreateSaleReceipt(Sale sale);
}
