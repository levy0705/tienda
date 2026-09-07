using SQLite;

namespace TiendaMvp.Core.Entities;

[Table("database_metadata")]
public sealed class DatabaseMetadata
{
    [PrimaryKey]
    public string Id { get; set; } = "schema";

    public int SchemaVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
