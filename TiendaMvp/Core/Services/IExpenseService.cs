using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IExpenseService
{
    IReadOnlyList<Expense> GetExpenses(string? query = null, string? category = null, string? paymentMethod = null);
    Expense RegisterExpense(Expense expense);
}
