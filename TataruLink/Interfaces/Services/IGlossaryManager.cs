// File: TataruLink/Interfaces/Services/IGlossaryManager.cs

using System.Collections.Generic;
using TataruLink.Config;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a service responsible for loading, managing, and applying the user's glossary.
/// This service is the single source of truth for all glossary data.
/// </summary>
public interface IGlossaryManager
{
    /// <summary>
    /// Gets a copy of the current glossary list for UI display.
    /// </summary>
    /// <returns>A list of all current glossary entries.</returns>
    List<GlossaryEntry> GetGlossary();

    /// <summary>
    /// Updates the entire glossary with a new list, persists the changes, and rebuilds the automaton.
    /// </summary>
    /// <param name="newGlossary">The new list of glossary entries to be saved.</param>
    void UpdateGlossary(List<GlossaryEntry> newGlossary);
    
    /// <summary>
    /// Applies the compiled glossary to the given text, replacing all matching terms.
    /// </summary>
    /// <param name="text">The input text to process.</param>
    /// <returns>The text with all glossary terms replaced.</returns>
    string Apply(string text);
    
    bool HasActiveEntries();
}
