using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IUserSession
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    void SignIn(User user);
    void SignOut();
}
