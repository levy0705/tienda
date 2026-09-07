using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class SalesHistoryPage : ContentPage
{
    private readonly ISaleService _sales;
    private readonly IAppFeedbackService _feedback;

    public SalesHistoryPage(ISaleService sales, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _sales = sales;
        _feedback = feedback;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSales();
    }

    private void LoadSales()
    {
        var query = SearchBox.Text?.Trim();
        SalesCollection.ItemsSource = _sales.GetSales().Where(sale => string.IsNullOrWhiteSpace(query) || sale.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => LoadSales();

    private async void OnDetailsClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Sale sale })
            return;
        var items = _sales.GetItems(sale.Id);
        var payments = _sales.GetPayments(sale.Id);
        var details = string.Join("\n", items.Select(item => $"{item.ProductName}: {item.LineLabel}"));
        var paymentDetails = payments.Count == 0 ? "Sin pagos (crédito total)" : string.Join("\n", payments.Select(payment => payment.AmountLabel));
        await _feedback.ShowMessageAsync("Detalle de venta", $"{details}\n\n{paymentDetails}\n\nTotal: ${sale.Total:N0}\nCambio: ${sale.ChangeAmount:N0}");
    }

    private async void OnReceiptClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Sale sale })
            return;
        try
        {
            var path = _sales.CreateSaleReceipt(sale);
            if (await _feedback.ConfirmAsync("Comprobante listo", "¿Deseas compartirlo?", "Compartir", "Cerrar"))
                await Share.Default.RequestAsync(new ShareFileRequest("Comprobante de venta", new ShareFile(path)));
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo generar", exception.Message);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Sale sale } || !await _feedback.ConfirmAsync("Anular / devolver", "Se devolverán las existencias y se registrará el reembolso en la caja abierta."))
            return;
        try
        {
            _sales.ReturnSale(sale.Id);
            await _feedback.ShowMessageAsync("Venta devuelta", "El inventario y la caja fueron actualizados.");
            LoadSales();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo devolver", exception.Message);
        }
    }
}
