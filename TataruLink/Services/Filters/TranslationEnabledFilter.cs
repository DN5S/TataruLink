// File: TataruLink/Services/Filters/TranslationEnabledFilter.cs
using Dalamud.Game.Text;
using TataruLink.Configuration;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// A filter that acts as the primary switch for translation features.
/// It checks if both global translations and automatic chat translations are enabled.
/// </summary>
public class TranslationEnabledFilter(TranslationSettings translationSettings) : IChatFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        return translationSettings is { EnableTranslations: true, EnableAutomaticChatTranslation: true };
    }
}
