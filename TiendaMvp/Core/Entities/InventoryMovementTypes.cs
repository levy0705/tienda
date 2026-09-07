namespace TiendaMvp.Core.Entities;

public static class InventoryMovementTypes
{
    public const string Initial = "Inventario inicial";
    public const string PurchaseEntry = "Entrada por compra";
    public const string SaleExit = "Salida por venta";
    public const string CustomerReturn = "Devolución de cliente";
    public const string SupplierReturn = "Devolución a proveedor";
    public const string PositiveAdjustment = "Ajuste positivo";
    public const string NegativeAdjustment = "Ajuste negativo";
    public const string Damaged = "Producto dañado";
    public const string Loss = "Pérdida";
    public const string CountCorrection = "Corrección de conteo";

    public static readonly IReadOnlyList<string> All =
    [
        Initial,
        PurchaseEntry,
        SaleExit,
        CustomerReturn,
        SupplierReturn,
        PositiveAdjustment,
        NegativeAdjustment,
        Damaged,
        Loss,
        CountCorrection
    ];

    public static bool IsPositive(string movementType) =>
        movementType is Initial or PurchaseEntry or CustomerReturn or PositiveAdjustment;

    public static bool IsNegative(string movementType) =>
        movementType is SaleExit or SupplierReturn or NegativeAdjustment or Damaged or Loss;
}
