using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class CustomersPage : ContentPage
{
    private readonly ICustomerService _customers;
    private readonly ISaleService _sales;
    private readonly IAppFeedbackService _feedback;
    private readonly IServiceProvider _services;

    public CustomersPage(ICustomerService customers, ISaleService sales, IAppFeedbackService feedback, IServiceProvider services)
    {
        InitializeComponent();
        _customers = customers;
        _sales = sales;
        _feedback = feedback;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCustomers();
    }

    private void LoadCustomers() => CustomersCollection.ItemsSource = _customers.GetCustomers(SearchBox.Text, includeBlocked: true);
    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => LoadCustomers();
    private async void OnNewClicked(object? sender, EventArgs e) => await OpenEditorAsync(null);

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Customer customer } && !customer.IsGeneral)
            await OpenEditorAsync(customer.Id);
        else if (sender is Button { CommandParameter: Customer general })
            await _feedback.ShowMessageAsync("Cliente general", "Se utiliza para ventas sin identificación y no se puede editar.");
    }

    private async void OnCreditsClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Customer customer })
            return;
        var page = _services.GetRequiredService<CreditsPage>();
        page.InitializeForCustomer(customer.Id);
        await Navigation.PushAsync(page);
    }

    private async void OnHistoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Customer customer })
            return;
        var sales = _sales.GetSales(customer.Id).Take(10).ToList();
        var message = sales.Count == 0
            ? "Este cliente aún no tiene ventas registradas."
            : string.Join("\n", sales.Select(sale => $"{sale.SaleDateUtc.ToLocalTime():dd/MM/yyyy} · ${sale.Total:N0} · {sale.Status}"));
        await _feedback.ShowMessageAsync($"Historial · {customer.Name}", message);
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Customer customer })
            return;
        var targetBlocked = !customer.IsBlocked;
        if (!await _feedback.ConfirmAsync(targetBlocked ? "Bloquear cliente" : "Desbloquear cliente", customer.Name))
            return;
        try
        {
            _customers.SetCustomerBlocked(customer.Id, targetBlocked);
            LoadCustomers();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }

    private async Task OpenEditorAsync(string? customerId)
    {
        var page = _services.GetRequiredService<CustomerEditorPage>();
        page.InitializeForCustomer(customerId);
        await Navigation.PushAsync(page);
    }
}
