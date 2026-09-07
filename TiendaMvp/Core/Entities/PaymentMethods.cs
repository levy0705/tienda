namespace TiendaMvp.Core.Entities;

public static class PaymentMethods
{
    public const string Cash = "Efectivo";
    public const string Card = "Tarjeta";
    public const string Transfer = "Transferencia";
    public const string Other = "Otro";

    public static readonly IReadOnlyList<string> All = [ Cash, Card, Transfer, Other ];
}
