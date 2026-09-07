using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ILocalDatabase _database;
    private readonly IAuditService _audit;

    public CustomerService(ILocalDatabase database, IAuditService audit)
    {
        _database = database;
        _audit = audit;
    }

    public IReadOnlyList<Customer> GetCustomers(string? query = null, bool includeBlocked = true)
    {
        var sales = _database.Connection.Table<Sale>().ToList();
        var credits = _database.Connection.Table<Credit>().ToList();
        var normalized = query?.Trim();
        return _database.Connection.Table<Customer>().ToList()
            .Where(customer => includeBlocked || !customer.IsBlocked)
            .Where(customer => string.IsNullOrWhiteSpace(normalized)
                || customer.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || customer.Document.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || customer.Phone.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Select(customer =>
            {
                customer.PurchaseCount = sales.Count(sale => sale.CustomerId == customer.Id && sale.Status != SaleStatuses.Cancelled);
                customer.BalanceDue = credits.Where(credit => credit.CustomerId == customer.Id && credit.Status != CreditStatuses.Cancelled).Sum(credit => Math.Max(0, credit.BalanceDue));
                return customer;
            })
            .OrderBy(customer => customer.IsGeneral ? 0 : 1)
            .ThenBy(customer => customer.Name)
            .ToList();
    }

    public Customer GetGeneralCustomer() =>
        _database.Connection.Find<Customer>(Customer.GeneralId)
        ?? throw new InvalidOperationException("No existe el cliente general.");

    public Customer? GetCustomer(string id) => _database.Connection.Find<Customer>(id);

    public void SaveCustomer(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        customer.Name = customer.Name.Trim();
        customer.Document = customer.Document.Trim();
        customer.Phone = customer.Phone.Trim();
        customer.Address = customer.Address.Trim();
        customer.Notes = customer.Notes.Trim();
        if (string.IsNullOrWhiteSpace(customer.Name))
            throw new InvalidOperationException("El nombre del cliente es obligatorio.");
        if (customer.CreditLimit < 0 || double.IsNaN(customer.CreditLimit) || double.IsInfinity(customer.CreditLimit))
            throw new InvalidOperationException("El límite de crédito debe ser válido y no negativo.");
        if (customer.IsGeneral)
            throw new InvalidOperationException("El cliente general no se puede reemplazar.");

        var duplicate = _database.Connection.Table<Customer>().ToList().FirstOrDefault(existing =>
            existing.Id != customer.Id && !string.IsNullOrWhiteSpace(customer.Document)
            && string.Equals(existing.Document, customer.Document, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
            throw new InvalidOperationException("Ya existe un cliente con ese documento.");

        var existingCustomer = _database.Connection.Find<Customer>(customer.Id);
        customer.UpdatedAtUtc = DateTime.UtcNow;
        if (existingCustomer is null)
        {
            customer.CreatedAtUtc = customer.UpdatedAtUtc;
            _database.Connection.Insert(customer);
        }
        else
        {
            _database.Connection.Update(customer);
        }
        _audit.Record(existingCustomer is null ? "Crear" : "Editar", nameof(Customer), customer.Id, customer.Name);
    }

    public void SetCustomerBlocked(string id, bool blocked)
    {
        var customer = GetCustomer(id) ?? throw new InvalidOperationException("Cliente no encontrado.");
        if (customer.IsGeneral)
            throw new InvalidOperationException("El cliente general siempre permanece activo.");
        customer.IsBlocked = blocked;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        _database.Connection.Update(customer);
        _audit.Record(blocked ? "Bloquear" : "Desbloquear", nameof(Customer), id, customer.Name);
    }

    public double GetBalanceDue(string customerId) =>
        _database.Connection.Table<Credit>().ToList()
            .Where(credit => credit.CustomerId == customerId && credit.Status != CreditStatuses.Cancelled)
            .Sum(credit => Math.Max(0, credit.BalanceDue));
}
