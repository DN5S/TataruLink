// File: TataruLink/Services/Filters/TranslationEnabledFilter.cs
using Dalamud.Game.Text;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// Filter that checks if the translation features are globally enabled.
/// </summary>
public class TranslationEnabledFilter(Configuration.Configuration configuration) : IChatFilter
{
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        return configuration.Translation is { EnableTranslations: true, EnableAutomaticChatTranslation: true };
    }
}
