using System.Collections.Generic;
using TataruLink.Models;

namespace TataruLink.Configuration;

public class GlossaryConfig
{
    public bool IsEnabled { get; set; } = true;
    public List<GlossaryEntry> Entries { get; set; } = [];
}
