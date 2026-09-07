using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("sale_payments")]
public sealed class SalePayment
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string SaleId { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public double Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string AmountLabel => $"{PaymentMethod}: ${Amount:N0}";
}
