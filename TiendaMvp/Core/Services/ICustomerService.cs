using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface ICustomerService
{
    IReadOnlyList<Customer> GetCustomers(string? query = null, bool includeBlocked = true);
    Customer GetGeneralCustomer();
    Customer? GetCustomer(string id);
    void SaveCustomer(Customer customer);
    void SetCustomerBlocked(string id, bool blocked);
    double GetBalanceDue(string customerId);
}
