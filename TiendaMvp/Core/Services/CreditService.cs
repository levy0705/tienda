using System.Globalization;
using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class CreditService : ICreditService
{
    private readonly ILocalDatabase _database;
    private readonly ICashService _cash;
    private readonly IUserSession _session;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;

    public CreditService(ILocalDatabase database, ICashService cash, IUserSession session, IAuditService audit, IPermissionService permissions)
    {
        _database = database;
        _cash = cash;
        _session = session;
        _audit = audit;
        _permissions = permissions;
    }

    public IReadOnlyList<Credit> GetCredits(string? customerId = null, string? status = null)
    {
        var customers = _database.Connection.Table<Customer>().ToList().ToDictionary(customer => customer.Id, customer => customer.Name);
        return _database.Connection.Table<Credit>().ToList()
            .Where(credit => string.IsNullOrWhiteSpace(customerId) || credit.CustomerId == customerId)
            .Where(credit => credit.Status != CreditStatuses.Cancelled)
            .Select(RefreshStatus)
            .Where(credit => string.IsNullOrWhiteSpace(status) || credit.Status == status)
            .Select(credit =>
            {
                credit.CustomerName = customers.TryGetValue(credit.CustomerId, out var name) ? name : "Cliente eliminado";
                return credit;
            })
            .OrderBy(credit => credit.Status == CreditStatuses.Overdue ? 0 : credit.Status == CreditStatuses.Active ? 1 : 2)
            .ThenBy(credit => credit.DueDateUtc)
            .ToList();
    }

    public Credit? GetCredit(string id)
    {
        var credit = _database.Connection.Find<Credit>(id);
        if (credit is not null)
        {
            RefreshStatus(credit);
            credit.CustomerName = _database.Connection.Find<Customer>(credit.CustomerId)?.Name ?? "Cliente eliminado";
        }
        return credit;
    }

    public IReadOnlyList<CreditMovement> GetMovements(string creditId) =>
        _database.Connection.Table<CreditMovement>().ToList()
            .Where(movement => movement.CreditId == creditId)
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ToList();

    public Credit RegisterPayment(string creditId, double amount, string notes = "", string? receiptId = null)
    {
        _permissions.Require(AppPermission.RegisterPayments);
        var credit = GetCredit(creditId) ?? throw new InvalidOperationException("Crédito no encontrado.");
        if (credit.BalanceDue <= 0)
            throw new InvalidOperationException("Este crédito ya está pagado.");
        if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
            throw new InvalidOperationException("El abono debe ser mayor que cero.");
        if (amount > credit.BalanceDue)
            throw new InvalidOperationException("El abono no puede superar el saldo pendiente.");
        var session = _cash.GetOpenSession() ?? throw new InvalidOperationException("Abre la caja antes de registrar un abono.");
        var receipt = string.IsNullOrWhiteSpace(receiptId) ? $"REC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}" : receiptId.Trim();
        var movement = new CreditMovement
        {
            CreditId = credit.Id,
            MovementType = CreditMovementTypes.Payment,
            Amount = -amount,
            BalanceBefore = credit.BalanceDue,
            BalanceAfter = Math.Max(0, credit.BalanceDue - amount),
            CashSessionId = session.Id,
            ReferenceId = receipt,
            Notes = notes.Trim(),
            UserId = _session.CurrentUser?.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        credit.BalanceDue = movement.BalanceAfter;
        credit.Status = credit.BalanceDue <= 0 ? CreditStatuses.Paid : CreditStatuses.Active;
        credit.UpdatedAtUtc = movement.CreatedAtUtc;
        _database.RunInTransaction(() =>
        {
            _database.Connection.Update(credit);
            _database.Connection.Insert(movement);
            _cash.RegisterIncome(amount, CashMovementTypes.CreditPayment, "Abono crédito", credit.Id, receipt, PaymentMethods.Cash);
            _audit.Record("Abono", nameof(Credit), credit.Id, $"{amount:0.##}; comprobante: {receipt}");
        });
        return credit;
    }

    public Credit Adjust(string creditId, double signedAmount, string reason)
    {
        if (_session.CurrentUser?.Role != "Administrador")
            throw new InvalidOperationException("Solo un administrador puede ajustar créditos.");
        if (signedAmount == 0 || double.IsNaN(signedAmount) || double.IsInfinity(signedAmount))
            throw new InvalidOperationException("El ajuste debe ser válido y diferente de cero.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("El motivo del ajuste es obligatorio.");
        var credit = GetCredit(creditId) ?? throw new InvalidOperationException("Crédito no encontrado.");
        var newBalance = credit.BalanceDue + signedAmount;
        if (newBalance < 0)
            throw new InvalidOperationException("El ajuste no puede dejar el saldo negativo.");
        var now = DateTime.UtcNow;
        var movement = new CreditMovement
        {
            CreditId = credit.Id,
            MovementType = CreditMovementTypes.Adjustment,
            Amount = signedAmount,
            BalanceBefore = credit.BalanceDue,
            BalanceAfter = newBalance,
            Notes = reason.Trim(),
            UserId = _session.CurrentUser?.Id,
            CreatedAtUtc = now
        };
        credit.BalanceDue = newBalance;
        credit.Status = newBalance <= 0 ? CreditStatuses.Paid : CreditStatuses.Active;
        credit.UpdatedAtUtc = now;
        _database.RunInTransaction(() =>
        {
            _database.Connection.Update(credit);
            _database.Connection.Insert(movement);
            _audit.Record("Ajustar", nameof(Credit), credit.Id, reason.Trim());
        });
        return credit;
    }

    public string CreatePaymentReceipt(Credit credit, CreditMovement payment)
    {
        ArgumentNullException.ThrowIfNull(credit);
        ArgumentNullException.ThrowIfNull(payment);
        var directory = Path.Combine(FileSystem.AppDataDirectory, "receipts");
        Directory.CreateDirectory(directory);
        var receiptId = string.IsNullOrWhiteSpace(payment.ReferenceId) ? payment.Id : payment.ReferenceId;
        var path = Path.Combine(directory, $"{receiptId}.txt");
        var customer = _database.Connection.Find<Customer>(credit.CustomerId);
        var content = $"COMPROBANTE DE ABONO\n" +
                      $"Número: {receiptId}\n" +
                      $"Fecha: {payment.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}\n" +
                      $"Cliente: {customer?.Name ?? "Cliente eliminado"}\n" +
                      $"Abono: {Math.Abs(payment.Amount).ToString("C", CultureInfo.CurrentCulture)}\n" +
                      $"Saldo restante: {credit.BalanceDue.ToString("C", CultureInfo.CurrentCulture)}\n";
        File.WriteAllText(path, content);
        return path;
    }

    private static Credit RefreshStatus(Credit credit)
    {
        if (credit.Status == CreditStatuses.Cancelled)
            return credit;
        credit.Status = credit.BalanceDue <= 0
            ? CreditStatuses.Paid
            : credit.DueDateUtc.Date < DateTime.UtcNow.Date ? CreditStatuses.Overdue : CreditStatuses.Active;
        return credit;
    }
}
