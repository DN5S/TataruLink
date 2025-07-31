// File: TataruLink/Interfaces/Services/ITranslationEngineFactory.cs

using TataruLink.Config;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a contract for a factory that creates and manages translation engine instances.
/// </summary>
public interface ITranslationEngineFactory
{
    /// <summary>
    /// Gets a specific translation engine instance.
    /// Returns null if the engine is unavailable (e.g., missing API key).
    /// </summary>
    ITranslationEngine? GetEngine(TranslationEngine engineType);

    /// <summary>
    /// Clears the internal cache of engine instances.
    /// This should be called when API configurations change.
    /// </summary>
    void ClearCache();
}
