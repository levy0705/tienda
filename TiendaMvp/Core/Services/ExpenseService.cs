using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class ExpenseService : IExpenseService
{
    private readonly ILocalDatabase _database;
    private readonly ICashService _cash;
    private readonly IUserSession _session;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;

    public ExpenseService(ILocalDatabase database, ICashService cash, IUserSession session, IAuditService audit, IPermissionService permissions)
    {
        _database = database;
        _cash = cash;
        _session = session;
        _audit = audit;
        _permissions = permissions;
    }

    public IReadOnlyList<Expense> GetExpenses(string? query = null, string? category = null, string? paymentMethod = null)
    {
        var users = _database.Connection.Table<User>().ToList().ToDictionary(user => user.Id, user => user.DisplayName);
        var normalizedQuery = query?.Trim();
        var normalizedCategory = category?.Trim();
        var normalizedMethod = paymentMethod?.Trim();

        return _database.Connection.Table<Expense>().ToList()
            .Where(expense => string.IsNullOrWhiteSpace(normalizedQuery)
                || expense.Concept.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || expense.Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Where(expense => string.IsNullOrWhiteSpace(normalizedCategory) || string.Equals(expense.Category, normalizedCategory, StringComparison.OrdinalIgnoreCase))
            .Where(expense => string.IsNullOrWhiteSpace(normalizedMethod) || string.Equals(expense.PaymentMethod, normalizedMethod, StringComparison.OrdinalIgnoreCase))
            .Select(expense =>
            {
                expense.UserName = expense.UserId is not null && users.TryGetValue(expense.UserId, out var name) ? name : "Usuario desconocido";
                return expense;
            })
            .OrderByDescending(expense => expense.ExpenseDateUtc)
            .ThenByDescending(expense => expense.CreatedAtUtc)
            .ToList();
    }

    public Expense RegisterExpense(Expense expense)
    {
        _permissions.Require(AppPermission.RegisterExpenses);
        ArgumentNullException.ThrowIfNull(expense);
        var user = _session.CurrentUser ?? throw new InvalidOperationException("No hay un usuario autenticado.");
        var session = _cash.GetOpenSession() ?? throw new InvalidOperationException("Abre la caja antes de registrar un gasto.");

        expense.Concept = expense.Concept.Trim();
        expense.Category = string.IsNullOrWhiteSpace(expense.Category) ? "General" : expense.Category.Trim();
        expense.PaymentMethod = expense.PaymentMethod.Trim();
        if (string.IsNullOrWhiteSpace(expense.Concept))
            throw new InvalidOperationException("El concepto del gasto es obligatorio.");
        if (expense.Amount <= 0 || double.IsNaN(expense.Amount) || double.IsInfinity(expense.Amount))
            throw new InvalidOperationException("El valor del gasto debe ser mayor que cero.");
        if (!PaymentMethods.All.Contains(expense.PaymentMethod, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selecciona un medio de pago válido.");

        expense.UserId = user.Id;
        expense.CashSessionId = session.Id;
        expense.ExpenseDateUtc = expense.ExpenseDateUtc == default ? DateTime.UtcNow : expense.ExpenseDateUtc.ToUniversalTime();
        expense.CreatedAtUtc = DateTime.UtcNow;
        _database.RunInTransaction(() =>
        {
            _database.Connection.Insert(expense);
            _cash.RegisterOutcome(expense.Amount, CashMovementTypes.Expense, nameof(Expense), expense.Id, expense.Concept, expense.PaymentMethod);
            _audit.Record("Registrar", nameof(Expense), expense.Id, $"{expense.Concept}: {expense.Amount:0.##}; medio: {expense.PaymentMethod}");
        });
        return expense;
    }
}
