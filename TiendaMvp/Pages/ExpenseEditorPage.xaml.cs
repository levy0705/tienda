using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class ExpenseEditorPage : ContentPage
{
    private readonly IExpenseService _expenses;
    private readonly IImageStorageService _images;
    private readonly IAppFeedbackService _feedback;
    private FileResult? _selectedPhoto;

    public ExpenseEditorPage(IExpenseService expenses, IImageStorageService images, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _expenses = expenses;
        _images = images;
        _feedback = feedback;
        CategoryPicker.ItemsSource = new[] { "General", "Operación", "Servicios", "Transporte", "Personal", "Otro" };
        CategoryPicker.SelectedIndex = 0;
        PaymentPicker.ItemsSource = PaymentMethods.All.ToList();
        PaymentPicker.SelectedItem = PaymentMethods.Cash;
        ExpenseDatePicker.Date = DateTime.Today;
    }

    private async void OnChoosePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 });
            _selectedPhoto = photos.FirstOrDefault();
            if (_selectedPhoto is not null)
            {
                ReceiptImage.Source = ImageSource.FromFile(_selectedPhoto.FullPath);
                ReceiptImage.IsVisible = true;
            }
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo elegir la imagen", exception.Message);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var expense = new Expense
            {
                Concept = ConceptEntry.Text?.Trim() ?? string.Empty,
                Category = CategoryPicker.SelectedItem?.ToString() ?? "General",
                Amount = ParseNumber(AmountEntry.Text),
                PaymentMethod = PaymentPicker.SelectedItem?.ToString() ?? PaymentMethods.Cash,
                ExpenseDateUtc = ExpenseDatePicker.Date?.ToUniversalTime() ?? DateTime.UtcNow
            };
            if (_selectedPhoto is not null)
                expense.ReceiptImagePath = await _images.SaveAsync(_selectedPhoto);

            _expenses.RegisterExpense(expense);
            await _feedback.ShowMessageAsync("Gasto registrado", "El movimiento quedó asociado a la caja abierta.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo registrar", exception.Message);
        }
    }

    private static double ParseNumber(string? value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
}
