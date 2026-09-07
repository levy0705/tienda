using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IPermissionService
{
    bool Has(AppPermission permission);
    void Require(AppPermission permission);
}
