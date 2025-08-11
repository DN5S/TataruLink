using System.Collections.Generic;
using TataruLink.Models;

namespace TataruLink.Configuration;

/// <summary>
/// Configuration for the user glossary system.
/// </summary>
public class GlossaryConfig
{
    public bool IsEnabled { get; set; } = true;
    public List<GlossaryEntry> Entries { get; set; } = [];
}
