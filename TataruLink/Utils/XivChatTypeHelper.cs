// File: TataruLink/Utils/XivChatTypeHelper.cs
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using TataruLink.Localization;

namespace TataruLink.Utils;

/// <summary>
/// Provides utility methods for XivChatType, such as getting user-friendly display names
/// and categorized lists for UI rendering. This is the single source of truth for
/// all UI-related representations of chat types.
/// </summary>
public static class XivChatTypeHelper
{
    // A private dictionary to hold the mapping of enums to their display names for non-sequential types.
    private static readonly IReadOnlyDictionary<XivChatType, string> DisplayNames = new Dictionary<XivChatType, string>
    {
        { XivChatType.None, "None" },
        { XivChatType.Debug, "Debug" },
        { XivChatType.Urgent, "Urgent" },
        { XivChatType.Notice, "Notice" },
        { XivChatType.Say, "Say" },
        { XivChatType.Shout, "Shout" },
        { XivChatType.TellOutgoing, "Tell (Outgoing)" },
        { XivChatType.TellIncoming, "Tell (Incoming)" },
        { XivChatType.Party, "Party" },
        { XivChatType.Alliance, "Alliance" },
        { XivChatType.FreeCompany, "Free Company" },
        { XivChatType.NoviceNetwork, "Novice Network" },
        { XivChatType.CustomEmote, "Custom Emote" },
        { XivChatType.StandardEmote, "Standard Emote" },
        { XivChatType.Yell, "Yell" },
        { XivChatType.CrossParty, "Cross-World Party" },
        { XivChatType.PvPTeam, "PvP Team" },
        { XivChatType.Echo, "Echo" },
        { XivChatType.SystemError, "System Error" },
        { XivChatType.SystemMessage, "System Message" },
        { XivChatType.GatheringSystemMessage, "Gathering System Message" },
        { XivChatType.ErrorMessage, "Error Message" },
        { XivChatType.NPCDialogue, "NPC Dialogue" },
        { XivChatType.NPCDialogueAnnouncements, "NPC Announcements" },
        { XivChatType.RetainerSale, "Retainer Sale" }
    };

    // A static, publicly accessible structure for the UI to build its categorized view.
    public static readonly IReadOnlyDictionary<string, List<XivChatType>> CategorizedChatTypesForDisplay = new Dictionary<string, List<XivChatType>>
    {
        {
            Strings.CategorizedChatTypes_General, [
                XivChatType.Say, XivChatType.Shout, XivChatType.Yell, XivChatType.Party,
                XivChatType.CrossParty, XivChatType.Alliance, XivChatType.TellIncoming,
                XivChatType.TellOutgoing, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
                XivChatType.PvPTeam
            ]
        },
        {
            Strings.CategorizedChatTypes_Linkshells,
            Enumerable.Range((int)XivChatType.Ls1, 8).Select(i => (XivChatType)i).ToList()
        },
        {
            Strings.CategorizedChatTypes_CWLS,
            Enumerable.Range((int)XivChatType.CrossLinkShell1, 8).Select(i => (XivChatType)i).ToList()
        },
        {
            Strings.CategorizedChatTypes_System_and_Emotes, [
                XivChatType.Echo, XivChatType.SystemMessage, XivChatType.SystemError, XivChatType.ErrorMessage,
                XivChatType.StandardEmote, XivChatType.CustomEmote, XivChatType.Notice, XivChatType.Urgent,
                XivChatType.GatheringSystemMessage
            ]
        },
        {
            Strings.CategorizedChatTypes_NPC,
            [XivChatType.NPCDialogue, XivChatType.NPCDialogueAnnouncements, XivChatType.RetainerSale]
        }
    };

    /// <summary>
    /// Gets the user-friendly display name for a given XivChatType.
    /// </summary>
    public static string GetDisplayName(XivChatType type)
    {
        if (DisplayNames.TryGetValue(type, out var name))
        {
            return name;
        }

        var typeCode = (ushort)type;
        return typeCode switch
        {
            >= (ushort)XivChatType.Ls1 and <= (ushort)XivChatType.Ls8 =>
                $"Linkshell-{typeCode - (ushort)XivChatType.Ls1 + 1}",
            >= (ushort)XivChatType.CrossLinkShell1 and <= (ushort)XivChatType.CrossLinkShell8 =>
                $"CWLS-{typeCode - (ushort)XivChatType.CrossLinkShell1 + 1}",
            _ => type.ToString()
        };
    }
}
