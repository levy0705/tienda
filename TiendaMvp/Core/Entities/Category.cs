using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("categories")]
public sealed class Category
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [NotNull, Unique]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    [Ignore]
    public string Status => IsActive ? "Activa" : "Inactiva";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
