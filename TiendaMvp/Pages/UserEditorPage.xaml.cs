using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class UserEditorPage : ContentPage
{
    private readonly IUserService _users;
    private readonly IAppFeedbackService _feedback;
    private string? _userId;
    private User? _user;
    private bool _loaded;

    public UserEditorPage(IUserService users, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _users = users;
        _feedback = feedback;
        RolePicker.ItemsSource = UserRoles.All.ToList();
    }

    public void InitializeForUser(string? userId)
    {
        _userId = userId;
        _loaded = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;
        _loaded = true;
        _user = string.IsNullOrWhiteSpace(_userId) ? new User() : _users.GetUser(_userId) ?? new User();
        var isNew = string.IsNullOrWhiteSpace(_userId);
        PageTitle.Text = isNew ? "Nuevo usuario" : "Editar usuario";
        NameEntry.Text = _user.DisplayName;
        RolePicker.SelectedItem = _user.Role;
        PinEntry.Text = string.Empty;
        ToggleButton.IsVisible = !isNew;
        ToggleButton.Text = _user.IsActive ? "Bloquear usuario" : "Activar usuario";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_user is null)
            return;
        _user.DisplayName = NameEntry.Text?.Trim() ?? string.Empty;
        _user.Role = RolePicker.SelectedItem?.ToString() ?? UserRoles.Cashier;
        try
        {
            _users.SaveUser(_user, string.IsNullOrWhiteSpace(PinEntry.Text) ? null : PinEntry.Text.Trim());
            await _feedback.ShowMessageAsync("Usuario guardado", "Los permisos se aplicarán al próximo inicio de sesión.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (_user is null)
            return;
        var activate = !_user.IsActive;
        if (!await _feedback.ConfirmAsync(activate ? "Activar usuario" : "Bloquear usuario", _user.DisplayName))
            return;
        try
        {
            _users.SetUserActive(_user.Id, activate);
            _user.IsActive = activate;
            ToggleButton.Text = activate ? "Bloquear usuario" : "Activar usuario";
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }
}
