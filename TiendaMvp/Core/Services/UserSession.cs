using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class UserSession : IUserSession
{
    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public void SignIn(User user) => CurrentUser = user;

    public void SignOut() => CurrentUser = null;
}
