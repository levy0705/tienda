using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("expenses")]
public sealed class Expense
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string Concept { get; set; } = string.Empty;

    public string Category { get; set; } = "General";
    public double Amount { get; set; }
    public DateTime ExpenseDateUtc { get; set; } = DateTime.UtcNow;
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string? ReceiptImagePath { get; set; }
    public string? UserId { get; set; }
    public string? CashSessionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string UserName { get; set; } = string.Empty;

    [Ignore]
    public bool HasReceipt => !string.IsNullOrWhiteSpace(ReceiptImagePath);
}
