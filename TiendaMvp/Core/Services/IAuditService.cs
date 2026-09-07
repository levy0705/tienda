namespace TiendaMvp.Core.Services;

public interface IAuditService
{
    void Record(string action, string entityName, string? entityId = null, string details = "");
}
