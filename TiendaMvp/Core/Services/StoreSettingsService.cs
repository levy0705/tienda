using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class StoreSettingsService : IStoreSettingsService
{
    private readonly IRepository<StoreSettings> _repository;
    private readonly IAuditService _audit;

    public StoreSettingsService(IRepository<StoreSettings> repository, IAuditService audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public StoreSettings Get() => _repository.GetById("store") ?? new StoreSettings();

    public void Save(StoreSettings settings)
    {
        settings.Id = "store";
        settings.UpdatedAtUtc = DateTime.UtcNow;
        if (_repository.GetById(settings.Id) is null)
        {
            settings.CreatedAtUtc = DateTime.UtcNow;
            _repository.Insert(settings);
        }
        else
        {
            _repository.Update(settings);
        }

        _audit.Record("Actualizar", nameof(StoreSettings), settings.Id, settings.StoreName);
    }
}
