using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class MorePage : ContentPage
{
    private readonly IAppFeedbackService _feedback;
    private readonly IPermissionService _permissions;
    private readonly IUserSession _session;
    private readonly IServiceProvider _services;

    public MorePage(IAppFeedbackService feedback, IPermissionService permissions, IUserSession session, IServiceProvider services)
    {
        InitializeComponent();
        _feedback = feedback;
        _permissions = permissions;
        _session = session;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CurrentUserLabel.Text = _session.CurrentUser is null
            ? "Sin sesión activa"
            : $"Sesión activa: {_session.CurrentUser.DisplayName} · {_session.CurrentUser.Role}";
    }

    private async void OnSuppliersClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<SuppliersPage>());

    private async void OnPurchasesClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<PurchasesPage>());

    private async void OnCreditsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<CreditsPage>());

    private async void OnCustomersClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<CustomersPage>());

    private async void OnSalesHistoryClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<SalesHistoryPage>());

    private async void OnExpensesClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<ExpensesPage>());

    private async void OnReportsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<ReportsPage>());

    private async void OnBackupsClicked(object? sender, EventArgs e)
    {
        try
        {
            _permissions.Require(AppPermission.ManageBackups);
            await Navigation.PushAsync(_services.GetRequiredService<BackupsPage>());
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("Acceso restringido", exception.Message);
        }
    }

    private async void OnUsersClicked(object? sender, EventArgs e)
    {
        try
        {
            _permissions.Require(AppPermission.ManageUsers);
            await Navigation.PushAsync(_services.GetRequiredService<UsersPage>());
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("Acceso restringido", exception.Message);
        }
    }

    private async void OnSwitchUserClicked(object? sender, EventArgs e)
    {
        if (!await _feedback.ConfirmAsync("Cambiar usuario", "Se cerrará la sesión actual en este teléfono.", "Continuar", "Cancelar"))
            return;
        if (Application.Current is App app)
            app.ShowLogin();
    }

    private async void OnComingSoonClicked(object? sender, EventArgs e) =>
        await _feedback.ShowMessageAsync("Módulo preparado", "Esta sección forma parte del siguiente incremento del MVP.");
}
