using System.Collections.ObjectModel;
using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class PurchaseEditorPage : ContentPage
{
    private readonly IPurchaseService _purchases;
    private readonly ISupplierService _suppliers;
    private readonly IProductCatalogService _catalog;
    private readonly IAppFeedbackService _feedback;
    private readonly IServiceProvider _services;
    private readonly ObservableCollection<PurchaseLineDraft> _lines = new();
    private string? _purchaseId;
    private string? _supplierFilter;
    private Purchase? _purchase;
    private List<Product> _products = new();
    private bool _loaded;

    public PurchaseEditorPage(
        IPurchaseService purchases,
        ISupplierService suppliers,
        IProductCatalogService catalog,
        IAppFeedbackService feedback,
        IServiceProvider services)
    {
        InitializeComponent();
        _purchases = purchases;
        _suppliers = suppliers;
        _catalog = catalog;
        _feedback = feedback;
        _services = services;
        LinesCollection.ItemsSource = _lines;
    }

    public void InitializeForPurchase(string? purchaseId, string? supplierId = null)
    {
        _purchaseId = purchaseId;
        _supplierFilter = supplierId;
        _loaded = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;
        _loaded = true;
        LoadPurchase();
    }

    private void LoadPurchase()
    {
        _purchase = string.IsNullOrWhiteSpace(_purchaseId) ? new Purchase { SupplierId = _supplierFilter ?? string.Empty } : _purchases.GetPurchase(_purchaseId) ?? new Purchase();
        var suppliers = _suppliers.GetSuppliers(includeInactive: true).ToList();
        SupplierPicker.ItemsSource = suppliers;
        SupplierPicker.SelectedItem = suppliers.FirstOrDefault(supplier => supplier.Id == _purchase.SupplierId);
        _products = _catalog.GetProducts(includeInactive: false).ToList();
        ProductPicker.ItemsSource = _products;
        PageTitle.Text = string.IsNullOrWhiteSpace(_purchaseId) ? "Nueva compra" : "Compra";
        StatusLabel.Text = _purchase.Status;
        DocumentNumberEntry.Text = _purchase.DocumentNumber;
        DiscountEntry.Text = FormatNumber(_purchase.Discount);
        AdditionalCostsEntry.Text = FormatNumber(_purchase.AdditionalCosts);
        PaidAmountEntry.Text = "0";
        _lines.Clear();
        if (!string.IsNullOrWhiteSpace(_purchaseId))
        {
            foreach (var item in _purchases.GetItems(_purchase.Id))
                _lines.Add(new PurchaseLineDraft(item.ProductId, item.ProductName, item.Quantity, item.UnitCost, item.Discount));
            PaidAmountEntry.Placeholder = $"Saldo pendiente: ${_purchase.BalanceDue:N0}";
        }
        UpdateTotals();
        var editable = _purchase.Status == PurchaseStatuses.Draft;
        EditorForm.IsEnabled = editable;
        DiscountEntry.IsEnabled = editable;
        AdditionalCostsEntry.IsEnabled = editable;
        SaveButton.IsVisible = editable;
        ReceiveButton.IsVisible = editable;
        PaymentButton.IsVisible = _purchase.Status is PurchaseStatuses.PendingPayment or PurchaseStatuses.Received;
        CancelButton.IsVisible = !string.IsNullOrWhiteSpace(_purchaseId) && _purchase.Status != PurchaseStatuses.Cancelled && _purchase.PaidAmount <= 0;
        PaymentSection.IsVisible = editable || PaymentButton.IsVisible;
    }

    private void OnProductChanged(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedItem is Product product)
        {
            SelectedProductLabel.Text = $"{product.Name} · último costo ${product.LastPurchaseCost:N0}";
            if (string.IsNullOrWhiteSpace(UnitCostEntry.Text) || ParseNumber(UnitCostEntry.Text) == 0)
                UnitCostEntry.Text = FormatNumber(product.LastPurchaseCost > 0 ? product.LastPurchaseCost : product.PurchaseCost);
        }
    }

    private async void OnAddLineClicked(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedItem is not Product product)
        {
            await _feedback.ShowMessageAsync("Producto requerido", "Selecciona o escanea un producto.");
            return;
        }
        var quantity = ParseNumber(QuantityEntry.Text);
        var unitCost = ParseNumber(UnitCostEntry.Text);
        if (quantity <= 0 || unitCost < 0)
        {
            await _feedback.ShowMessageAsync("Datos inválidos", "La cantidad debe ser mayor que cero y el costo no puede ser negativo.");
            return;
        }

        var existing = _lines.FirstOrDefault(line => line.ProductId == product.Id);
        if (existing is null)
            _lines.Add(new PurchaseLineDraft(product.Id, product.Name, quantity, unitCost, 0));
        else
        {
            existing.Quantity += quantity;
            existing.UnitCost = unitCost;
            existing.Refresh();
            LinesCollection.ItemsSource = null;
            LinesCollection.ItemsSource = _lines;
        }
        QuantityEntry.Text = string.Empty;
        UnitCostEntry.Text = string.Empty;
        UpdateTotals();
    }

    private void OnRemoveLineClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: PurchaseLineDraft line })
        {
            _lines.Remove(line);
            UpdateTotals();
        }
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<QrScannerPage>();
        page.ProductDetected += OnProductDetected;
        await Navigation.PushAsync(page);
    }

    private void OnProductDetected(object? sender, Product product)
    {
        if (sender is QrScannerPage scanner)
            scanner.ProductDetected -= OnProductDetected;
        ProductPicker.SelectedItem = _products.FirstOrDefault(item => item.Id == product.Id);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            SaveDraftFromForm();
            await _feedback.ShowMessageAsync("Borrador guardado", "La compra queda pendiente de recepción.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnReceiveClicked(object? sender, EventArgs e)
    {
        try
        {
            SaveDraftFromForm();
            var received = _purchases.ReceivePurchase(_purchase!.Id, ParseNumber(PaidAmountEntry.Text));
            await _feedback.ShowMessageAsync("Compra recibida", $"Inventario actualizado. Estado: {received.Status}.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo recibir", exception.Message);
        }
    }

    private async void OnPaymentClicked(object? sender, EventArgs e)
    {
        try
        {
            var updated = _purchases.RegisterPayment(_purchase!.Id, ParseNumber(PaidAmountEntry.Text));
            await _feedback.ShowMessageAsync("Abono registrado", $"Saldo pendiente: ${updated.BalanceDue:N0}.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo registrar el abono", exception.Message);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_purchase is null || !await _feedback.ConfirmAsync("Anular compra", "Se revertirá el inventario si la compra ya fue recibida."))
            return;
        try
        {
            _purchases.CancelPurchase(_purchase.Id);
            await _feedback.ShowMessageAsync("Compra anulada", "Se registraron las devoluciones de inventario correspondientes.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo anular", exception.Message);
        }
    }

    private void SaveDraftFromForm()
    {
        if (_purchase is null)
            throw new InvalidOperationException("Compra no inicializada.");
        if (SupplierPicker.SelectedItem is not Supplier supplier)
            throw new InvalidOperationException("Selecciona un proveedor activo.");
        _purchase.SupplierId = supplier.Id;
        _purchase.DocumentNumber = DocumentNumberEntry.Text?.Trim() ?? string.Empty;
        _purchase.Discount = ParseNumber(DiscountEntry.Text);
        _purchase.AdditionalCosts = ParseNumber(AdditionalCostsEntry.Text);
        _purchases.SaveDraft(_purchase, _lines.Select(line => line.ToEntity(_purchase.Id)));
    }

    private void UpdateTotals()
    {
        var subtotal = _lines.Sum(line => line.LineTotal);
        var discount = ParseNumber(DiscountEntry.Text);
        var additional = ParseNumber(AdditionalCostsEntry.Text);
        var total = Math.Max(0, subtotal - discount + additional);
        SubtotalLabel.Text = $"${subtotal:N0}";
        TotalLabel.Text = $"${total:N0}";
    }

    private void OnTotalsChanged(object? sender, TextChangedEventArgs e) => UpdateTotals();

    private static double ParseNumber(string? value) => double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
    private static string FormatNumber(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private sealed class PurchaseLineDraft
    {
        public PurchaseLineDraft(string productId, string productName, double quantity, double unitCost, double discount)
        {
            ProductId = productId;
            ProductName = productName;
            Quantity = quantity;
            UnitCost = unitCost;
            Discount = discount;
            Refresh();
        }

        public string ProductId { get; }
        public string ProductName { get; }
        public double Quantity { get; set; }
        public double UnitCost { get; set; }
        public double Discount { get; set; }
        public double LineTotal { get; private set; }
        public string LineLabel => $"{Quantity:0.##} × ${UnitCost:N0} = ${LineTotal:N0}";
        public void Refresh() => LineTotal = Math.Max(0, Quantity * UnitCost - Discount);
        public PurchaseItem ToEntity(string purchaseId) => new() { PurchaseId = purchaseId, ProductId = ProductId, Quantity = Quantity, UnitCost = UnitCost, Discount = Discount, LineTotal = LineTotal };
    }
}
