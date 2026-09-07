using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class CreditsPage : ContentPage
{
    private readonly ICreditService _credits;
    private readonly IAppFeedbackService _feedback;
    private string? _customerFilter;

    public CreditsPage(ICreditService credits, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _credits = credits;
        _feedback = feedback;
        StatusPicker.ItemsSource = new[] { "Todos", CreditStatuses.Active, CreditStatuses.Overdue, CreditStatuses.Paid };
        StatusPicker.SelectedIndex = 0;
    }

    public void InitializeForCustomer(string? customerId) => _customerFilter = customerId;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCredits();
    }

    private void LoadCredits()
    {
        var status = StatusPicker.SelectedItem?.ToString();
        CreditsCollection.ItemsSource = _credits.GetCredits(_customerFilter, status == "Todos" ? null : status);
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            var query = SearchBox.Text.Trim();
            CreditsCollection.ItemsSource = _credits.GetCredits(_customerFilter, status == "Todos" ? null : status).Where(credit => credit.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => LoadCredits();
    private void OnStatusChanged(object? sender, EventArgs e) => LoadCredits();

    private async void OnPaymentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Credit credit })
            return;
        var value = await DisplayPromptAsync("Registrar abono", $"Saldo actual: ${credit.BalanceDue:N0}", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(value))
            return;
        try
        {
            var updated = _credits.RegisterPayment(credit.Id, ParseNumber(value));
            var movement = _credits.GetMovements(credit.Id).FirstOrDefault();
            string? receiptPath = null;
            if (movement is not null)
            {
                receiptPath = _credits.CreatePaymentReceipt(updated, movement);
                if (await _feedback.ConfirmAsync("Abono registrado", "¿Deseas compartir el comprobante?", "Compartir", "Cerrar"))
                    await Share.Default.RequestAsync(new ShareFileRequest("Comprobante de abono", new ShareFile(receiptPath)));
            }
            if (receiptPath is null)
                await _feedback.ShowMessageAsync("Abono registrado", $"Saldo: ${updated.BalanceDue:N0}.");
            LoadCredits();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo registrar", exception.Message);
        }
    }

    private async void OnAdjustClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Credit credit })
            return;
        var value = await DisplayPromptAsync("Ajuste autorizado", "Usa positivo para aumentar y negativo para reducir el saldo.", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(value))
            return;
        var reason = await DisplayPromptAsync("Motivo obligatorio", "Describe por qué se autoriza el ajuste.");
        if (string.IsNullOrWhiteSpace(reason))
            return;
        try
        {
            var updated = _credits.Adjust(credit.Id, ParseNumber(value), reason);
            await _feedback.ShowMessageAsync("Ajuste guardado", $"Nuevo saldo: ${updated.BalanceDue:N0}.");
            LoadCredits();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo ajustar", exception.Message);
        }
    }

    private void OnHistoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Credit credit })
            return;
        HistoryPanel.IsVisible = true;
        HistoryTitle.Text = $"Historial · {credit.CustomerName}";
        MovementsCollection.ItemsSource = _credits.GetMovements(credit.Id);
    }

    private static double ParseNumber(string? value) => double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
}
