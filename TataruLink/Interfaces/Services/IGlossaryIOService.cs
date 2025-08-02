// File: TataruLink/Interfaces/Services/IGlossaryIOService.cs

using System.Collections.Generic;
using TataruLink.Config;

namespace TataruLink.Interfaces.Services;

public enum GlossaryFormat { Json, Csv }

/// <summary>
/// Defines a service for importing and exporting glossary data.
/// </summary>
public interface IGlossaryIOService
{
    /// <summary>
    /// Exports the given list of glossary entries to a string representation.
    /// </summary>
    /// <param name="glossary">The glossary entries to export.</param>
    /// <param name="format">The target format (JSON or CSV).</param>
    /// <returns>A string representing the glossary in the specified format.</returns>
    string Export(List<GlossaryEntry> glossary, GlossaryFormat format);

    /// <summary>
    /// Imports glossary entries from a string, attempting to parse it as JSON or CSV.
    /// </summary>
    /// <param name="data">The string data to import from.</param>
    /// <returns>A list of imported glossary entries, or null if parsing fails.</returns>
    List<GlossaryEntry>? Import(string data);
}
