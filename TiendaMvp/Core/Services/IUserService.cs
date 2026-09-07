using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IUserService
{
    IReadOnlyList<User> GetUsers(bool includeInactive = true);
    User? GetUser(string id);
    User Authenticate(string userId, string pin);
    void SaveUser(User user, string? newPin = null);
    void ChangeCurrentPin(string currentPin, string newPin);
    void SetUserActive(string id, bool isActive);
}
