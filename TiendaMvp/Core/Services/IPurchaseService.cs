using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IPurchaseService
{
    IReadOnlyList<Purchase> GetPurchases(string? supplierId = null, string? status = null, string? query = null);
    Purchase? GetPurchase(string id);
    IReadOnlyList<PurchaseItem> GetItems(string purchaseId);
    void SaveDraft(Purchase purchase, IEnumerable<PurchaseItem> items);
    Purchase ReceivePurchase(string purchaseId, double paidAmount = 0);
    Purchase RegisterPayment(string purchaseId, double amount);
    void CancelPurchase(string purchaseId);
}
