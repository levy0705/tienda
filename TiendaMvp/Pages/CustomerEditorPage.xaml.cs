using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class CustomerEditorPage : ContentPage
{
    private readonly ICustomerService _customers;
    private readonly IAppFeedbackService _feedback;
    private string? _customerId;
    private Customer? _customer;
    private bool _loaded;

    public CustomerEditorPage(ICustomerService customers, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _customers = customers;
        _feedback = feedback;
    }

    public void InitializeForCustomer(string? customerId)
    {
        _customerId = customerId;
        _loaded = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;
        _loaded = true;
        _customer = string.IsNullOrWhiteSpace(_customerId) ? new Customer() : _customers.GetCustomer(_customerId) ?? new Customer();
        var isNew = string.IsNullOrWhiteSpace(_customerId);
        PageTitle.Text = isNew ? "Nuevo cliente" : "Editar cliente";
        NameEntry.Text = _customer.Name;
        DocumentEntry.Text = _customer.Document;
        PhoneEntry.Text = _customer.Phone;
        CreditLimitEntry.Text = _customer.CreditLimit.ToString("0.##", CultureInfo.CurrentCulture);
        AddressEntry.Text = _customer.Address;
        NotesEditor.Text = _customer.Notes;
        ToggleButton.IsVisible = !isNew;
        ToggleButton.Text = _customer.IsBlocked ? "Desbloquear cliente" : "Bloquear cliente";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_customer is null)
            return;
        _customer.Name = NameEntry.Text?.Trim() ?? string.Empty;
        _customer.Document = DocumentEntry.Text?.Trim() ?? string.Empty;
        _customer.Phone = PhoneEntry.Text?.Trim() ?? string.Empty;
        _customer.CreditLimit = ParseNumber(CreditLimitEntry.Text);
        _customer.Address = AddressEntry.Text?.Trim() ?? string.Empty;
        _customer.Notes = NotesEditor.Text?.Trim() ?? string.Empty;
        try
        {
            _customers.SaveCustomer(_customer);
            await _feedback.ShowMessageAsync("Cliente guardado", "El cliente ya puede seleccionarse en una venta a crédito.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (_customer is null)
            return;
        var targetBlocked = !_customer.IsBlocked;
        if (!await _feedback.ConfirmAsync(targetBlocked ? "Bloquear cliente" : "Desbloquear cliente", _customer.Name))
            return;
        try
        {
            _customers.SetCustomerBlocked(_customer.Id, targetBlocked);
            _customer.IsBlocked = targetBlocked;
            ToggleButton.Text = targetBlocked ? "Desbloquear cliente" : "Bloquear cliente";
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }

    private static double ParseNumber(string? value) => double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
}
