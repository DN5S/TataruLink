// File: TataruLink/Services/Interfaces/IChatMessageFormatter.cs
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;

namespace TataruLink.Services.Interfaces;

public interface IChatMessageFormatter
{
    SeString FormatMessage(TranslationRecord record);
}
