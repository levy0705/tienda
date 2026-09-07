using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class SupplierEditorPage : ContentPage
{
    private readonly ISupplierService _suppliers;
    private readonly IAppFeedbackService _feedback;
    private string? _supplierId;
    private Supplier? _supplier;
    private bool _loaded;

    public SupplierEditorPage(ISupplierService suppliers, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _suppliers = suppliers;
        _feedback = feedback;
    }

    public void InitializeForSupplier(string? supplierId)
    {
        _supplierId = supplierId;
        _loaded = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;
        _loaded = true;
        _supplier = string.IsNullOrWhiteSpace(_supplierId) ? new Supplier() : _suppliers.GetSupplier(_supplierId) ?? new Supplier();
        var isNew = string.IsNullOrWhiteSpace(_supplierId);
        PageTitle.Text = isNew ? "Nuevo proveedor" : "Editar proveedor";
        NameEntry.Text = _supplier.Name;
        TaxIdEntry.Text = _supplier.TaxId;
        PhoneEntry.Text = _supplier.Phone;
        EmailEntry.Text = _supplier.Email;
        AddressEntry.Text = _supplier.Address;
        NotesEditor.Text = _supplier.Notes;
        ToggleButton.IsVisible = !isNew;
        ToggleButton.Text = _supplier.IsActive ? "Desactivar proveedor" : "Activar proveedor";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_supplier is null)
            return;
        _supplier.Name = NameEntry.Text?.Trim() ?? string.Empty;
        _supplier.TaxId = TaxIdEntry.Text?.Trim() ?? string.Empty;
        _supplier.Phone = PhoneEntry.Text?.Trim() ?? string.Empty;
        _supplier.Email = EmailEntry.Text?.Trim() ?? string.Empty;
        _supplier.Address = AddressEntry.Text?.Trim() ?? string.Empty;
        _supplier.Notes = NotesEditor.Text?.Trim() ?? string.Empty;
        try
        {
            _suppliers.SaveSupplier(_supplier);
            await _feedback.ShowMessageAsync("Proveedor guardado", "La información quedó disponible para registrar compras.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (_supplier is null)
            return;
        var targetState = !_supplier.IsActive;
        if (!await _feedback.ConfirmAsync(targetState ? "Activar proveedor" : "Desactivar proveedor", _supplier.Name))
            return;
        try
        {
            _suppliers.SetSupplierActive(_supplier.Id, targetState);
            _supplier.IsActive = targetState;
            ToggleButton.Text = targetState ? "Desactivar proveedor" : "Activar proveedor";
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }
}
