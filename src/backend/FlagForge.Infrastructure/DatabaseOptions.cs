using System.ComponentModel.DataAnnotations;

namespace FlagForge.Infrastructure;

public sealed class DatabaseOptions
{
    [Required(ErrorMessage = "Set ConnectionStrings__Sql to the SQL Server connection string.")]
    public string ConnectionString { get; set; } = string.Empty;
}
