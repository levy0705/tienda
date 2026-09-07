using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class ExpensesPage : ContentPage
{
    private readonly IExpenseService _expenses;
    private readonly IAppFeedbackService _feedback;
    private readonly IServiceProvider _services;

    public ExpensesPage(IExpenseService expenses, IAppFeedbackService feedback, IServiceProvider services)
    {
        InitializeComponent();
        _expenses = expenses;
        _feedback = feedback;
        _services = services;
        PaymentPicker.ItemsSource = new[] { "Todos" }.Concat(PaymentMethods.All).ToList();
        CategoryPicker.ItemsSource = new[] { "Todas", "General", "Operación", "Servicios", "Transporte", "Personal", "Otro" };
        PaymentPicker.SelectedIndex = 0;
        CategoryPicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var category = CategoryPicker.SelectedItem?.ToString();
        var payment = PaymentPicker.SelectedItem?.ToString();
        ExpensesCollection.ItemsSource = _expenses.GetExpenses(
            SearchBar.Text,
            category is "Todas" ? null : category,
            payment is "Todos" ? null : payment);
    }

    private async void OnAddClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<ExpenseEditorPage>());

    private void OnFilterChanged(object? sender, EventArgs e) => Refresh();
}
