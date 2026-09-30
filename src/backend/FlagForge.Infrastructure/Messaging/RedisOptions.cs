using System.ComponentModel.DataAnnotations;

namespace FlagForge.Infrastructure.Messaging;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required(ErrorMessage = "Set Redis__ConnectionString, for example localhost:6379.")]
    public string ConnectionString { get; set; } = string.Empty;
}
