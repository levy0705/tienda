using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class AuditService : IAuditService
{
    private readonly IRepository<AuditEntry> _repository;
    private readonly IUserSession _session;

    public AuditService(IRepository<AuditEntry> repository, IUserSession session)
    {
        _repository = repository;
        _session = session;
    }

    public void Record(string action, string entityName, string? entityId = null, string details = "")
    {
        _repository.Insert(new AuditEntry
        {
            UserId = _session.CurrentUser?.Id,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
