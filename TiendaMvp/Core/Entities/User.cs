using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("users")]
public sealed class User
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull]
    public string DisplayName { get; set; } = string.Empty;

    [NotNull]
    public string Role { get; set; } = "Administrador";

    [NotNull]
    public string PinHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessAtUtc { get; set; }

    [Ignore]
    public string StatusLabel => IsActive ? "Activo" : "Bloqueado";

    [Ignore]
    public string LastAccessLabel => LastAccessAtUtc is null ? "Nunca" : LastAccessAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
