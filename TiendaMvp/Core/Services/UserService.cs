using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class UserService : IUserService
{
    private readonly ILocalDatabase _database;
    private readonly IUserSession _session;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;

    public UserService(ILocalDatabase database, IUserSession session, IAuditService audit, IPermissionService permissions)
    {
        _database = database;
        _session = session;
        _audit = audit;
        _permissions = permissions;
    }

    public IReadOnlyList<User> GetUsers(bool includeInactive = true) =>
        _database.Connection.Table<User>().ToList()
            .Where(user => includeInactive || user.IsActive)
            .OrderBy(user => user.IsActive ? 0 : 1)
            .ThenBy(user => user.DisplayName)
            .ToList();

    public User? GetUser(string id) => _database.Connection.Find<User>(id);

    public User Authenticate(string userId, string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            throw new InvalidOperationException("Escribe el PIN para iniciar.");
        var user = GetUser(userId) ?? throw new InvalidOperationException("Usuario no encontrado.");
        if (!user.IsActive)
            throw new InvalidOperationException("Este usuario está bloqueado.");
        try
        {
            if (!PinHasher.Verify(pin.Trim(), user.PinHash))
                throw new InvalidOperationException("El PIN no es correcto.");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("El PIN de este usuario no es válido.");
        }

        user.LastAccessAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = user.LastAccessAtUtc.Value;
        _database.Connection.Update(user);
        _session.SignIn(user);
        _audit.Record("Inicio de sesión", nameof(User), user.Id, user.DisplayName);
        return user;
    }

    public void SaveUser(User user, string? newPin = null)
    {
        _permissions.Require(AppPermission.ManageUsers);
        ArgumentNullException.ThrowIfNull(user);
        user.DisplayName = user.DisplayName.Trim();
        user.Role = user.Role.Trim();
        if (string.IsNullOrWhiteSpace(user.DisplayName))
            throw new InvalidOperationException("El nombre del usuario es obligatorio.");
        if (!UserRoles.All.Contains(user.Role))
            throw new InvalidOperationException("Selecciona un rol válido.");

        var existing = GetUser(user.Id);
        if (existing is null && string.IsNullOrWhiteSpace(newPin))
            throw new InvalidOperationException("El PIN es obligatorio para un usuario nuevo.");
        if (existing is not null && _session.CurrentUser?.Id == user.Id && !user.IsActive)
            throw new InvalidOperationException("No puedes bloquear el usuario con el que has iniciado sesión.");
        if (existing is not null && existing.IsActive && existing.Role == UserRoles.Administrator
            && (user.Role != UserRoles.Administrator || !user.IsActive)
            && GetUsers(false).Count(item => item.Role == UserRoles.Administrator) <= 1)
            throw new InvalidOperationException("Debe existir al menos un administrador activo.");
        if (!string.IsNullOrWhiteSpace(newPin))
            user.PinHash = CreatePinHash(newPin);
        else if (existing is not null)
            user.PinHash = existing.PinHash;

        user.UpdatedAtUtc = DateTime.UtcNow;
        if (existing is null)
        {
            user.CreatedAtUtc = user.UpdatedAtUtc;
            _database.Connection.Insert(user);
        }
        else
        {
            user.CreatedAtUtc = existing.CreatedAtUtc;
            user.LastAccessAtUtc = existing.LastAccessAtUtc;
            _database.Connection.Update(user);
        }
        _audit.Record(existing is null ? "Crear" : "Editar", nameof(User), user.Id, $"{user.DisplayName} · {user.Role}");
    }

    public void ChangeCurrentPin(string currentPin, string newPin)
    {
        var user = _session.CurrentUser ?? throw new InvalidOperationException("No hay un usuario autenticado.");
        try
        {
            if (!PinHasher.Verify(currentPin.Trim(), user.PinHash))
                throw new InvalidOperationException("El PIN actual no es correcto.");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("El PIN actual no es válido.");
        }
        user.PinHash = CreatePinHash(newPin);
        user.UpdatedAtUtc = DateTime.UtcNow;
        _database.Connection.Update(user);
        _session.SignIn(user);
        _audit.Record("Cambiar PIN", nameof(User), user.Id, user.DisplayName);
    }

    public void SetUserActive(string id, bool isActive)
    {
        _permissions.Require(AppPermission.ManageUsers);
        var user = GetUser(id) ?? throw new InvalidOperationException("Usuario no encontrado.");
        if (!isActive && _session.CurrentUser?.Id == user.Id)
            throw new InvalidOperationException("No puedes bloquear el usuario con el que has iniciado sesión.");
        if (!isActive && user.Role == UserRoles.Administrator && GetUsers(false).Count(item => item.Role == UserRoles.Administrator) <= 1)
            throw new InvalidOperationException("Debe existir al menos un administrador activo.");
        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _database.Connection.Update(user);
        _audit.Record(isActive ? "Activar" : "Bloquear", nameof(User), id, user.DisplayName);
    }

    private static string CreatePinHash(string pin)
    {
        var normalized = pin.Trim();
        if (normalized.Length < 4 || normalized.Length > 8 || normalized.Any(character => !char.IsDigit(character)))
            throw new InvalidOperationException("El PIN debe tener entre 4 y 8 dígitos.");
        return PinHasher.Hash(normalized);
    }
}
