using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class CashService : ICashService
{
    private readonly ILocalDatabase _database;
    private readonly IUserSession _session;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;

    public CashService(ILocalDatabase database, IUserSession session, IAuditService audit, IPermissionService permissions)
    {
        _database = database;
        _session = session;
        _audit = audit;
        _permissions = permissions;
    }

    public CashSession? GetOpenSession()
    {
        var userId = _session.CurrentUser?.Id;
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return _database.Connection.Table<CashSession>().ToList()
            .Where(session => session.Status == CashSessionStatuses.Open && session.UserId == userId)
            .OrderByDescending(session => session.OpenedAtUtc)
            .FirstOrDefault();
    }

    public CashSession Open(double openingAmount)
    {
        _permissions.Require(AppPermission.OpenCloseCash);
        if (openingAmount < 0 || double.IsNaN(openingAmount) || double.IsInfinity(openingAmount))
            throw new InvalidOperationException("El monto inicial debe ser válido y no negativo.");
        var user = _session.CurrentUser ?? throw new InvalidOperationException("No hay un usuario autenticado.");
        if (GetOpenSession() is not null)
            throw new InvalidOperationException("Ya existe una caja abierta.");

        var session = new CashSession
        {
            OpeningAmount = openingAmount,
            UserId = user.Id,
            OpenedAtUtc = DateTime.UtcNow
        };
        _database.RunInTransaction(() =>
        {
            _database.Connection.Insert(session);
            _audit.Record("Abrir", nameof(CashSession), session.Id, $"Monto inicial: {openingAmount:0.##}");
        });
        return session;
    }

    public CashSession Close(double closingAmount, string pin, string observations = "")
    {
        _permissions.Require(AppPermission.OpenCloseCash);
        if (closingAmount < 0 || double.IsNaN(closingAmount) || double.IsInfinity(closingAmount))
            throw new InvalidOperationException("El monto de cierre debe ser válido y no negativo.");
        var session = GetOpenSession() ?? throw new InvalidOperationException("No hay una caja abierta.");
        var user = _session.CurrentUser ?? throw new InvalidOperationException("No hay un usuario autenticado.");
        if (string.IsNullOrWhiteSpace(pin))
            throw new InvalidOperationException("Confirma el cierre con el PIN del usuario.");
        try
        {
            if (!PinHasher.Verify(pin.Trim(), user.PinHash))
                throw new InvalidOperationException("El PIN no es correcto.");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("El PIN configurado no es válido.");
        }

        var expected = GetExpectedAmount(session.Id);
        session.ExpectedAmount = expected;
        session.ClosingAmount = closingAmount;
        session.CountedAmount = closingAmount;
        session.Difference = closingAmount - expected;
        session.ClosingObservations = observations?.Trim() ?? string.Empty;
        session.PinConfirmed = true;
        session.Status = CashSessionStatuses.Closed;
        session.ClosedAtUtc = DateTime.UtcNow;
        _database.RunInTransaction(() =>
        {
            _database.Connection.Update(session);
            _audit.Record("Cerrar", nameof(CashSession), session.Id, $"Esperado: {session.ExpectedAmount:0.##}; contado: {closingAmount:0.##}; diferencia: {session.Difference:0.##}");
        });
        return session;
    }

    public IReadOnlyList<CashSession> GetClosedSessions()
    {
        var user = _session.CurrentUser;
        if (user is null)
            return Array.Empty<CashSession>();

        var users = _database.Connection.Table<User>().ToList().ToDictionary(item => item.Id, item => item.DisplayName);
        return _database.Connection.Table<CashSession>().ToList()
            .Where(session => session.Status == CashSessionStatuses.Closed
                && (user.Role == "Administrador" || session.UserId == user.Id))
            .Select(session =>
            {
                session.UserName = session.UserId is not null && users.TryGetValue(session.UserId, out var name) ? name : "Usuario desconocido";
                return session;
            })
            .OrderByDescending(session => session.ClosedAtUtc ?? session.OpenedAtUtc)
            .ToList();
    }

    public IReadOnlyList<CashMovement> GetMovements(string? sessionId = null) =>
        _database.Connection.Table<CashMovement>().ToList()
            .Where(movement => string.IsNullOrWhiteSpace(sessionId) || movement.CashSessionId == sessionId)
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ToList();

    public double GetExpectedAmount(string sessionId)
    {
        var session = _database.Connection.Find<CashSession>(sessionId) ?? throw new InvalidOperationException("Caja no encontrada.");
        return session.OpeningAmount + GetMovements(sessionId)
            .Where(IsCashMovement)
            .Sum(movement => movement.Amount);
    }

    public CashCloseSummary GetCloseSummary(string sessionId)
    {
        var session = _database.Connection.Find<CashSession>(sessionId) ?? throw new InvalidOperationException("Caja no encontrada.");
        var movements = GetMovements(sessionId);
        var sales = movements.Where(movement => movement.MovementType == CashMovementTypes.Sale && movement.Amount > 0).ToList();
        var creditPayments = movements.Where(movement => movement.MovementType == CashMovementTypes.CreditPayment && movement.Amount > 0).ToList();
        var expenses = movements.Where(movement => movement.MovementType == CashMovementTypes.Expense && movement.Amount < 0).Sum(movement => -movement.Amount);
        var withdrawals = movements.Where(movement => movement.MovementType == CashMovementTypes.Withdrawal && movement.Amount < 0).Sum(movement => -movement.Amount);
        var extraordinary = movements.Where(movement => movement.MovementType == CashMovementTypes.ExtraordinaryIncome && movement.Amount > 0).Sum(movement => movement.Amount);
        var refunds = movements.Where(movement => movement.MovementType == CashMovementTypes.Refund && movement.Amount < 0).Sum(movement => -movement.Amount);

        var totals = sales.Concat(creditPayments)
            .GroupBy(movement => string.IsNullOrWhiteSpace(movement.PaymentMethod) ? PaymentMethods.Cash : movement.PaymentMethod)
            .ToDictionary(group => group.Key, group => group.Sum(movement => movement.Amount));
        var expected = GetExpectedAmount(sessionId);
        var counted = session.CountedAmount > 0 || session.Status == CashSessionStatuses.Closed ? session.CountedAmount : session.ClosingAmount;

        return new CashCloseSummary
        {
            SessionId = session.Id,
            OpeningAmount = session.OpeningAmount,
            ExpectedAmount = expected,
            CountedAmount = counted,
            Difference = counted - expected,
            SalesCollected = sales.Sum(movement => movement.Amount),
            VoidedSales = refunds,
            Expenses = expenses,
            Withdrawals = withdrawals,
            ExtraordinaryIncome = extraordinary,
            CreditPayments = creditPayments.Sum(movement => movement.Amount),
            Observations = session.ClosingObservations,
            TotalsByPaymentMethod = totals
        };
    }

    public CashMovement RegisterIncome(double amount, string movementType, string? referenceType = null, string? referenceId = null, string notes = "", string paymentMethod = "")
    {
        if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
            throw new InvalidOperationException("El ingreso de caja debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(movementType))
            throw new InvalidOperationException("El tipo de movimiento de caja es obligatorio.");
        var session = GetOpenSession() ?? throw new InvalidOperationException("Abre la caja antes de registrar cobros.");
        var movement = new CashMovement
        {
            CashSessionId = session.Id,
            MovementType = movementType.Trim(),
            Amount = amount,
            PaymentMethod = NormalizePaymentMethod(paymentMethod),
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Notes = notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _database.RunInTransaction(() =>
        {
            _database.Connection.Insert(movement);
            _audit.Record("Ingreso", nameof(CashMovement), movement.Id, $"{movement.MovementType}: {amount:0.##}");
        });
        return movement;
    }

    public CashMovement RegisterOutcome(double amount, string movementType, string? referenceType = null, string? referenceId = null, string notes = "", string paymentMethod = "")
    {
        if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
            throw new InvalidOperationException("El egreso de caja debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(movementType))
            throw new InvalidOperationException("El tipo de movimiento de caja es obligatorio.");
        var session = GetOpenSession() ?? throw new InvalidOperationException("Abre la caja antes de registrar una devolución.");
        var movement = new CashMovement
        {
            CashSessionId = session.Id,
            MovementType = movementType.Trim(),
            Amount = -amount,
            PaymentMethod = NormalizePaymentMethod(paymentMethod),
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Notes = notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _database.RunInTransaction(() =>
        {
            _database.Connection.Insert(movement);
            _audit.Record("Egreso", nameof(CashMovement), movement.Id, $"{movement.MovementType}: {amount:0.##}");
        });
        return movement;
    }

    public CashMovement RegisterExtraordinaryIncome(double amount, string notes = "")
    {
        _permissions.Require(AppPermission.OpenCloseCash);
        return RegisterIncome(amount, CashMovementTypes.ExtraordinaryIncome, "Caja", null, notes, PaymentMethods.Cash);
    }

    public CashMovement RegisterWithdrawal(double amount, string notes = "")
    {
        _permissions.Require(AppPermission.OpenCloseCash);
        return RegisterOutcome(amount, CashMovementTypes.Withdrawal, "Caja", null, notes, PaymentMethods.Cash);
    }

    private static string NormalizePaymentMethod(string paymentMethod) =>
        string.IsNullOrWhiteSpace(paymentMethod) ? PaymentMethods.Cash : paymentMethod.Trim();

    private static bool IsCashMovement(CashMovement movement) =>
        string.IsNullOrWhiteSpace(movement.PaymentMethod) || string.Equals(movement.PaymentMethod, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase);
}
