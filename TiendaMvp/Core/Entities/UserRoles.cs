namespace TiendaMvp.Core.Entities;

public static class UserRoles
{
    public const string Administrator = "Administrador";
    public const string Manager = "Encargado";
    public const string Cashier = "Cajero";
    public const string Warehouse = "Bodega";

    public static readonly IReadOnlyList<string> All = [ Administrator, Manager, Cashier, Warehouse ];
}
