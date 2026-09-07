namespace TiendaMvp.Core.Entities;

public static class PurchaseStatuses
{
    public const string Draft = "Borrador";
    public const string Received = "Recibida";
    public const string PendingPayment = "Pendiente de pago";
    public const string Paid = "Pagada";
    public const string Cancelled = "Anulada";

    public static readonly IReadOnlyList<string> All =
    [ Draft, Received, PendingPayment, Paid, Cancelled ];
}
