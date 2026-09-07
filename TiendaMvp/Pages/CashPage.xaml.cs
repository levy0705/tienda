using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class CashPage : ContentPage
{
    private readonly IAppFeedbackService _feedback;
    private readonly ICashService _cash;
    private readonly IServiceProvider _services;

    public CashPage(IAppFeedbackService feedback, ICashService cash, IServiceProvider services)
    {
        InitializeComponent();
        _feedback = feedback;
        _cash = cash;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var session = _cash.GetOpenSession();
        var isOpen = session is not null;
        OpenPanel.IsVisible = !isOpen;
        OpenActionsPanel.IsVisible = isOpen;
        ClosePanel.IsVisible = isOpen;
        StatusLabel.Text = isOpen ? "Abierta" : "Cerrada";
        StatusLabel.TextColor = isOpen ? Colors.Green : Colors.IndianRed;
        ClosedSessionsCollection.ItemsSource = _cash.GetClosedSessions();

        if (session is null)
        {
            ExpectedLabel.Text = "Efectivo esperado: $0";
            DifferencePreviewLabel.Text = "Diferencia: $0";
            SalesLabel.Text = "$0";
            PaymentsLabel.Text = "$0";
            ExpensesLabel.Text = "$0";
            WithdrawalsLabel.Text = "$0";
            PaymentTotalsLabel.Text = "Por medio de pago: sin movimientos";
            ExtraordinaryLabel.Text = "Entradas extraordinarias: $0";
            VoidedLabel.Text = "Anulaciones y devoluciones: $0";
            MovementsCollection.ItemsSource = Array.Empty<CashMovement>();
            return;
        }

        var summary = _cash.GetCloseSummary(session.Id);
        ExpectedLabel.Text = $"Efectivo esperado: ${summary.ExpectedAmount:N0}";
        var counted = ParseNumber(ClosingEntry.Text);
        DifferencePreviewLabel.Text = string.IsNullOrWhiteSpace(ClosingEntry.Text)
            ? "Diferencia: pendiente de arqueo"
            : $"Diferencia: ${(counted - summary.ExpectedAmount):N0}";
        SalesLabel.Text = $"${summary.SalesCollected:N0}";
        PaymentsLabel.Text = $"${summary.CreditPayments:N0}";
        ExpensesLabel.Text = $"${summary.Expenses:N0}";
        WithdrawalsLabel.Text = $"${summary.Withdrawals:N0}";
        ExtraordinaryLabel.Text = $"Entradas extraordinarias: ${summary.ExtraordinaryIncome:N0}";
        VoidedLabel.Text = $"Anulaciones y devoluciones: ${summary.VoidedSales:N0}";
        PaymentTotalsLabel.Text = summary.TotalsByPaymentMethod.Count == 0
            ? "Por medio de pago: sin movimientos"
            : "Por medio de pago: " + string.Join(" · ", summary.TotalsByPaymentMethod.Select(item => $"{item.Key} ${item.Value:N0}"));
        MovementsCollection.ItemsSource = _cash.GetMovements(session.Id);
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        try
        {
            _cash.Open(ParseNumber(OpeningEntry.Text));
            OpeningEntry.Text = string.Empty;
            Refresh();
            await _feedback.ShowMessageAsync("Caja abierta", "Ya puedes registrar ventas, abonos y gastos.");
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo abrir", exception.Message);
        }
    }

    private async void OnExtraordinaryIncomeClicked(object? sender, EventArgs e)
    {
        var value = await DisplayPromptAsync("Entrada extraordinaria", "Monto recibido", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(value))
            return;
        var notes = await DisplayPromptAsync("Concepto", "Describe el origen de la entrada.");
        if (string.IsNullOrWhiteSpace(notes))
            return;
        try
        {
            _cash.RegisterExtraordinaryIncome(ParseNumber(value), notes);
            Refresh();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo registrar", exception.Message);
        }
    }

    private async void OnWithdrawalClicked(object? sender, EventArgs e)
    {
        var value = await DisplayPromptAsync("Retiro de caja", "Monto retirado", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(value))
            return;
        var notes = await DisplayPromptAsync("Motivo", "Describe el motivo del retiro.");
        if (string.IsNullOrWhiteSpace(notes))
            return;
        try
        {
            _cash.RegisterWithdrawal(ParseNumber(value), notes);
            Refresh();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo registrar", exception.Message);
        }
    }

    private async void OnExpenseClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<ExpenseEditorPage>());

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        var pin = await DisplayPromptAsync("Confirmar cierre", "Escribe tu PIN", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(pin))
            return;
        try
        {
            var closed = _cash.Close(ParseNumber(ClosingEntry.Text), pin, ObservationsEditor.Text ?? string.Empty);
            ClosingEntry.Text = string.Empty;
            ObservationsEditor.Text = string.Empty;
            Refresh();
            var sign = closed.Difference >= 0 ? "sobrante" : "faltante";
            await _feedback.ShowMessageAsync("Caja cerrada", $"Esperado: ${closed.ExpectedAmount:N0}. Contado: ${closed.CountedAmount:N0}. {sign}: ${Math.Abs(closed.Difference):N0}.");
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo cerrar", exception.Message);
        }
    }

    private void OnClosingAmountChanged(object? sender, TextChangedEventArgs e) => Refresh();

    private async void OnClosedSessionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CashSession session)
            return;
        try
        {
            var summary = _cash.GetCloseSummary(session.Id);
            var paymentTotals = summary.TotalsByPaymentMethod.Count == 0
                ? "Sin pagos registrados"
                : string.Join("\n", summary.TotalsByPaymentMethod.Select(item => $"{item.Key}: ${item.Value:N0}"));
            await DisplayAlertAsync("Detalle del cierre",
                $"Usuario: {session.UserName}\n" +
                $"Inicial: ${summary.OpeningAmount:N0}\n" +
                $"Ventas cobradas: ${summary.SalesCollected:N0}\n" +
                $"Abonos: ${summary.CreditPayments:N0}\n" +
                $"Gastos: ${summary.Expenses:N0}\n" +
                $"Retiros: ${summary.Withdrawals:N0}\n" +
                $"Entradas extra: ${summary.ExtraordinaryIncome:N0}\n" +
                $"Devoluciones: ${summary.VoidedSales:N0}\n" +
                $"Esperado: ${summary.ExpectedAmount:N0}\n" +
                $"Contado: ${summary.CountedAmount:N0}\n" +
                $"Diferencia: ${summary.Difference:N0}\n\n" +
                $"Totales por medio:\n{paymentTotals}\n\n" +
                (string.IsNullOrWhiteSpace(summary.Observations) ? "Sin observaciones" : $"Observaciones: {summary.Observations}"),
                "Cerrar");
        }
        finally
        {
            ClosedSessionsCollection.SelectedItem = null;
        }
    }

    private static double ParseNumber(string? value) => double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
}
