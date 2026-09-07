using System.Collections.ObjectModel;
using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class SellPage : ContentPage
{
    private readonly IAppFeedbackService _feedback;
    private readonly IProductCatalogService _catalog;
    private readonly ICustomerService _customers;
    private readonly ISaleService _sales;
    private readonly ICashService _cash;
    private readonly IServiceProvider _services;
    private readonly ObservableCollection<SaleLineDraft> _lines = new();
    private readonly ObservableCollection<SalePaymentDraft> _payments = new();
    private List<Product> _products = new();
    private bool _isSubmitting;

    public SellPage(IAppFeedbackService feedback, IProductCatalogService catalog, ICustomerService customers, ISaleService sales, ICashService cash, IServiceProvider services)
    {
        InitializeComponent();
        _feedback = feedback;
        _catalog = catalog;
        _customers = customers;
        _sales = sales;
        _cash = cash;
        _services = services;
        LinesCollection.ItemsSource = _lines;
        PaymentsCollection.ItemsSource = _payments;
        PaymentMethodPicker.ItemsSource = PaymentMethods.All.ToList();
        PaymentMethodPicker.SelectedIndex = 0;
        DueDatePicker.Date = DateTime.Today.AddDays(30);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _products = _catalog.GetProducts(includeInactive: false).ToList();
        LoadProductPicker();
        var customers = _customers.GetCustomers(includeBlocked: false).ToList();
        CustomerPicker.ItemsSource = customers;
        if (CustomerPicker.SelectedIndex < 0)
            CustomerPicker.SelectedItem = customers.FirstOrDefault(customer => customer.IsGeneral);
        var cash = _cash.GetOpenSession();
        CashStatusLabel.Text = cash is null ? "Caja cerrada" : "Caja abierta";
        CashStatusLabel.TextColor = cash is null ? Colors.IndianRed : Colors.ForestGreen;
        UpdateTotals();
    }

    private void LoadProductPicker()
    {
        var query = SearchBox.Text?.Trim();
        ProductPicker.ItemsSource = _products.Where(product => string.IsNullOrWhiteSpace(query) || product.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || product.InternalCode.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => LoadProductPicker();

    private void OnProductChanged(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedItem is Product product)
            QuantityEntry.Text = string.IsNullOrWhiteSpace(QuantityEntry.Text) ? "1" : QuantityEntry.Text;
    }

    private async void OnAddLineClicked(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedItem is not Product product)
        {
            await _feedback.ShowMessageAsync("Producto requerido", "Selecciona o escanea un producto.");
            return;
        }
        var quantity = ParseNumber(QuantityEntry.Text);
        if (quantity <= 0)
        {
            await _feedback.ShowMessageAsync("Cantidad inválida", "La cantidad debe ser mayor que cero.");
            return;
        }
        var line = _lines.FirstOrDefault(item => item.ProductId == product.Id);
        if (line is null)
            _lines.Add(new SaleLineDraft(product.Id, product.Name, quantity, product.SalePrice));
        else
        {
            line.Quantity += quantity;
            line.Refresh();
            LinesCollection.ItemsSource = null;
            LinesCollection.ItemsSource = _lines;
        }
        QuantityEntry.Text = "1";
        PaymentAmountEntry.Text = FormatNumber(Math.Max(0, CalculateTotal() - _payments.Sum(payment => payment.Amount)));
        UpdateTotals();
    }

    private void OnRemoveLineClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: SaleLineDraft line })
        {
            _lines.Remove(line);
            UpdateTotals();
        }
    }

    private void OnIncreaseLineClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: SaleLineDraft line })
        {
            line.Quantity++;
            line.Refresh();
            RefreshLines();
        }
    }

    private void OnDecreaseLineClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: SaleLineDraft line })
            return;
        line.Quantity--;
        if (line.Quantity <= 0)
            _lines.Remove(line);
        else
            line.Refresh();
        RefreshLines();
    }

    private void RefreshLines()
    {
        LinesCollection.ItemsSource = null;
        LinesCollection.ItemsSource = _lines;
        UpdateTotals();
    }

    private async void OnAddPaymentClicked(object? sender, EventArgs e)
    {
        if (PaymentMethodPicker.SelectedItem is not string method)
        {
            await _feedback.ShowMessageAsync("Método requerido", "Selecciona un método de pago.");
            return;
        }
        var amount = ParseNumber(PaymentAmountEntry.Text);
        if (amount <= 0)
        {
            await _feedback.ShowMessageAsync("Monto inválido", "El monto aplicado debe ser mayor que cero.");
            return;
        }
        var remaining = CalculateTotal() - _payments.Sum(payment => payment.Amount);
        if (amount > remaining + 0.001)
        {
            await _feedback.ShowMessageAsync("Monto inválido", "La suma de pagos no puede superar el total.");
            return;
        }
        var existing = _payments.FirstOrDefault(payment => payment.PaymentMethod == method);
        if (existing is null)
            _payments.Add(new SalePaymentDraft(method, amount));
        else
        {
            existing.Amount += amount;
            PaymentsCollection.ItemsSource = null;
            PaymentsCollection.ItemsSource = _payments;
        }
        PaymentAmountEntry.Text = FormatNumber(Math.Max(0, remaining - amount));
        UpdateTotals();
    }

    private void OnRemovePaymentClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: SalePaymentDraft payment })
        {
            _payments.Remove(payment);
            PaymentAmountEntry.Text = FormatNumber(Math.Max(0, CalculateTotal() - _payments.Sum(item => item.Amount)));
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
        SearchBox.Text = string.Empty;
        LoadProductPicker();
        ProductPicker.SelectedItem = _products.FirstOrDefault(item => item.Id == product.Id);
        QuantityEntry.Text = "1";
    }

    private async void OnRegisterSaleClicked(object? sender, EventArgs e) => await RegisterSaleAsync(_payments.Select(payment => payment.ToEntity()).ToList());

    private async void OnFullCreditClicked(object? sender, EventArgs e) => await RegisterSaleAsync(Array.Empty<SalePayment>());

    private async Task RegisterSaleAsync(IEnumerable<SalePayment> payments)
    {
        if (_isSubmitting)
            return;
        _isSubmitting = true;
        RegisterButton.IsEnabled = false;
        try
        {
            var customer = CustomerPicker.SelectedItem as Customer ?? _customers.GetGeneralCustomer();
            var sale = _sales.RegisterSale(new Sale
            {
                CustomerId = customer.Id,
                Discount = ParseNumber(DiscountEntry.Text)
            }, _lines.Select(line => line.ToEntity()), payments, DueDatePicker.Date, ParseNumber(CashTenderedEntry.Text));
            var receiptPath = _sales.CreateSaleReceipt(sale);
            if (await _feedback.ConfirmAsync("Venta registrada", sale.CreditAmount > 0 ? $"Venta a crédito. Saldo: ${sale.CreditAmount:N0}. ¿Deseas compartir el comprobante?" : "Pago registrado en caja. ¿Deseas compartir el comprobante?", "Compartir", "Cerrar"))
                await Share.Default.RequestAsync(new ShareFileRequest("Comprobante de venta", new ShareFile(receiptPath)));
            _lines.Clear();
            _payments.Clear();
            DiscountEntry.Text = "0";
            CashTenderedEntry.Text = "0";
            PaymentAmountEntry.Text = "0";
            UpdateTotals();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo registrar", exception.Message);
        }
        finally
        {
            _isSubmitting = false;
            RegisterButton.IsEnabled = true;
        }
    }

    private void UpdateTotals()
    {
        var total = CalculateTotal();
        TotalLabel.Text = $"${total:N0}";
        var cashApplied = _payments.Where(payment => payment.PaymentMethod == PaymentMethods.Cash).Sum(payment => payment.Amount);
        var paid = _payments.Sum(payment => payment.Amount);
        var change = paid >= total - 0.001 ? Math.Max(0, ParseNumber(CashTenderedEntry.Text) - cashApplied) : 0;
        ChangeLabel.Text = $"Cambio: ${change:N0}";
    }

    private double CalculateTotal() => Math.Max(0, _lines.Sum(line => line.LineTotal) - ParseNumber(DiscountEntry.Text));

    private void OnTotalsChanged(object? sender, TextChangedEventArgs e) => UpdateTotals();
    private static double ParseNumber(string? value) => double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
    private static string FormatNumber(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private sealed class SaleLineDraft
    {
        public SaleLineDraft(string productId, string productName, double quantity, double unitPrice)
        {
            ProductId = productId;
            ProductName = productName;
            Quantity = quantity;
            UnitPrice = unitPrice;
            Refresh();
        }

        public string ProductId { get; }
        public string ProductName { get; }
        public double Quantity { get; set; }
        public double UnitPrice { get; }
        public double LineTotal { get; private set; }
        public string LineLabel => $"{Quantity:0.##} × ${UnitPrice:N0} = ${LineTotal:N0}";
        public void Refresh() => LineTotal = Quantity * UnitPrice;
        public SaleItem ToEntity() => new() { ProductId = ProductId, Quantity = Quantity, UnitPrice = UnitPrice, LineTotal = LineTotal };
    }

    private sealed class SalePaymentDraft
    {
        public SalePaymentDraft(string paymentMethod, double amount)
        {
            PaymentMethod = paymentMethod;
            Amount = amount;
        }

        public string PaymentMethod { get; }
        public double Amount { get; set; }
        public string AmountLabel => $"{PaymentMethod}: ${Amount:N0}";
        public SalePayment ToEntity() => new() { PaymentMethod = PaymentMethod, Amount = Amount };
    }
}
