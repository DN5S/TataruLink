// File: TataruLink/Services/Filters/TranslationEnabledFilter.cs

using Dalamud.Game.Text;
using TataruLink.Config;
using TataruLink.Interfaces.Filtering;

namespace TataruLink.Services.Filtering;

/// <summary>
/// A filter that acts as the primary switch for translation features.
/// It checks if both global translations and automatic chat translations are enabled.
/// </summary>
public class TranslationStatusFilter(TranslationConfig translationConfig) : IMessageFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        return translationConfig is { EnableTranslations: true, EnableAutomaticChatTranslation: true };
    }
}
