namespace TiendaMvp.Core.Entities;

public sealed class CashCloseSummary
{
    public string SessionId { get; set; } = string.Empty;
    public double OpeningAmount { get; set; }
    public double ExpectedAmount { get; set; }
    public double CountedAmount { get; set; }
    public double Difference { get; set; }
    public double SalesCollected { get; set; }
    public double VoidedSales { get; set; }
    public double Expenses { get; set; }
    public double Withdrawals { get; set; }
    public double ExtraordinaryIncome { get; set; }
    public double CreditPayments { get; set; }
    public string Observations { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, double> TotalsByPaymentMethod { get; set; } = new Dictionary<string, double>();
}
