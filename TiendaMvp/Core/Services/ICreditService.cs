using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface ICreditService
{
    IReadOnlyList<Credit> GetCredits(string? customerId = null, string? status = null);
    Credit? GetCredit(string id);
    IReadOnlyList<CreditMovement> GetMovements(string creditId);
    Credit RegisterPayment(string creditId, double amount, string notes = "", string? receiptId = null);
    Credit Adjust(string creditId, double signedAmount, string reason);
    string CreatePaymentReceipt(Credit credit, CreditMovement payment);
}
