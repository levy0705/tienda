namespace TiendaMvp.Core.Services;

public interface IAppFeedbackService
{
    Task ShowMessageAsync(string title, string message, string cancel = "Aceptar");
    Task<bool> ConfirmAsync(string title, string message, string accept = "Confirmar", string cancel = "Cancelar");
}
