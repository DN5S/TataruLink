// File: TataruLink/Services/Filtering/TranslationStatusFilter.cs

using Dalamud.Game.Text;
using TataruLink.Config;
using TataruLink.Interfaces.Filtering;

namespace TataruLink.Services.Filtering;

/// <summary>
/// A filter that acts as the primary, global switch for translation features.
/// It checks if both global translations and automatic chat translations are enabled in the configuration.
/// </summary>
public class TranslationStatusFilter(TranslationConfig translationConfig) : IMessageFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        // This filter passes only if both primary switches are enabled.
        return translationConfig is { EnableTranslations: true, EnableAutomaticChatTranslation: true };
    }
}
