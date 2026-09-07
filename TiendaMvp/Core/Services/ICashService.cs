using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface ICashService
{
    CashSession? GetOpenSession();
    CashSession Open(double openingAmount);
    CashSession Close(double closingAmount, string pin, string observations = "");
    IReadOnlyList<CashSession> GetClosedSessions();
    CashCloseSummary GetCloseSummary(string sessionId);
    IReadOnlyList<CashMovement> GetMovements(string? sessionId = null);
    double GetExpectedAmount(string sessionId);
    CashMovement RegisterIncome(double amount, string movementType, string? referenceType = null, string? referenceId = null, string notes = "", string paymentMethod = "");
    CashMovement RegisterOutcome(double amount, string movementType, string? referenceType = null, string? referenceId = null, string notes = "", string paymentMethod = "");
    CashMovement RegisterExtraordinaryIncome(double amount, string notes = "");
    CashMovement RegisterWithdrawal(double amount, string notes = "");
}
