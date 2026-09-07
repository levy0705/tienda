using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IUserSession _session;

    public PermissionService(IUserSession session) => _session = session;

    public bool Has(AppPermission permission)
    {
        var role = _session.CurrentUser?.Role;
        if (role == UserRoles.Administrator)
            return true;

        return role switch
        {
            UserRoles.Manager => permission != AppPermission.ManageUsers,
            UserRoles.Cashier => permission is AppPermission.ApplyDiscounts
                or AppPermission.RegisterExpenses
                or AppPermission.OpenCloseCash
                or AppPermission.GrantCredits
                or AppPermission.RegisterPayments
                or AppPermission.ConsultReports,
            UserRoles.Warehouse => permission is AppPermission.AdjustInventory or AppPermission.ConsultReports,
            _ => false
        };
    }

    public void Require(AppPermission permission)
    {
        if (!Has(permission))
            throw new InvalidOperationException($"El rol del usuario no permite esta acción ({PermissionLabel(permission)}).");
    }

    private static string PermissionLabel(AppPermission permission) => permission switch
    {
        AppPermission.ViewCostsAndProfit => "ver costos y utilidad",
        AppPermission.ChangePrices => "cambiar precios",
        AppPermission.ApplyDiscounts => "aplicar descuentos",
        AppPermission.CancelSales => "anular ventas",
        AppPermission.AdjustInventory => "ajustar inventario",
        AppPermission.RegisterExpenses => "registrar gastos",
        AppPermission.OpenCloseCash => "abrir o cerrar caja",
        AppPermission.GrantCredits => "otorgar créditos",
        AppPermission.RegisterPayments => "registrar abonos",
        AppPermission.ConsultReports => "consultar reportes",
        AppPermission.ManageUsers => "administrar usuarios",
        AppPermission.ManageBackups => "administrar respaldos",
        _ => "realizar esta acción"
    };
}
