namespace TiendaMvp.Core.Services;

public sealed class AppFeedbackService : IAppFeedbackService
{
    public Task ShowMessageAsync(string title, string message, string cancel = "Aceptar") =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = GetActivePage();
            if (page is null || page.Handler is null || page.Window is null)
                return;

            await page.DisplayAlertAsync(title, message, cancel);
        });

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Confirmar", string cancel = "Cancelar") =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = GetActivePage();
            if (page is null || page.Handler is null || page.Window is null)
                return false;

            return await page.DisplayAlertAsync(title, message, accept, cancel);
        });

    private static Page? GetActivePage() =>
        Application.Current?.Windows.FirstOrDefault()?.Page;
}
