using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IStoreSettingsService
{
    StoreSettings Get();
    void Save(StoreSettings settings);
}
