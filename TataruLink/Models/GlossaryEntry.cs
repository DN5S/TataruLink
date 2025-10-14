using System;

namespace TataruLink.Models;

public class GlossaryEntry
{
    public long Id { get; set; }
    public string Original { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
