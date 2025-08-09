using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace TataruLink.Utils;

public static class SeStringUtils
{
    public static string ExtractText(this SeString seString, bool includeSymbols = false)
    {
        if (seString.Payloads.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        
        foreach (var payload in seString.Payloads)
        {
            switch (payload)
            {
                case AutoTranslatePayload autoTranslate:
                    builder.Append(autoTranslate.Text);
                    break;
                    
                case IconPayload icon when includeSymbols:
                    builder.Append($"[{icon.Icon}]");
                    break;
                    
                case ItemPayload item when includeSymbols:
                    builder.Append($"[Item:{item.Item.RowId}]");
                    break;
                    
                case MapLinkPayload mapLink when includeSymbols:
                    builder.Append($"[Map:{mapLink.TerritoryType.RowId}]");
                    break;
                    
                case ITextProvider textProvider:
                    builder.Append(textProvider.Text);
                    break;
            }
        }
        
        return builder.ToString().Trim();
    }

    public static bool HasPlayer(this SeString seString)
    {
        return seString.Payloads.Any(p => p is PlayerPayload);
    }

    public static string? GetPlayerName(this SeString seString)
    {
        var playerPayload = seString.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        return playerPayload?.PlayerName;
    }

    public static List<string> GetAllPlayerNames(this SeString seString)
    {
        return seString.Payloads
            .OfType<PlayerPayload>()
            .Select(p => p.PlayerName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();
    }

    public static bool HasAutoTranslate(this SeString seString)
    {
        return seString.Payloads.Any(p => p is AutoTranslatePayload);
    }

    public static List<string> GetAutoTranslateTexts(this SeString seString)
    {
        return seString.Payloads
            .OfType<AutoTranslatePayload>()
            .Select(p => p.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    public static bool IsEmpty(this SeString seString)
    {
        if (seString.Payloads.Count == 0)
            return true;
        
        var text = seString.ExtractText();
        return string.IsNullOrWhiteSpace(text);
    }

    public static int TextLength(this SeString seString)
    {
        return seString.ExtractText().Length;
    }

    public static SeString CreateTranslation(string translatedText, SeString originalMessage)
    {
        var builder = new SeStringBuilder();
        
        var startPayloads = originalMessage.Payloads
            .TakeWhile(p => !(p is ITextProvider))
            .ToList();
        
        foreach (var payload in startPayloads)
        {
            builder.Add(payload);
        }
        
        builder.AddText(translatedText);
        
        var endPayloads = originalMessage.Payloads
            .AsEnumerable()
            .Reverse()
            .TakeWhile(p => !(p is ITextProvider))
            .Reverse()
            .ToList();
        
        foreach (var payload in endPayloads)
        {
            builder.Add(payload);
        }
        
        return builder.Build();
    }

    public static string Normalize(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        
        text = text.Trim();
        
        text = text.Replace("　", " ");
        
        text = System.Text.RegularExpressions.Regex.Replace(text, @"([.!?])\1+", "$1");
        
        return text;
    }

    public static bool ShouldTranslate(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        
        if (text.Length < 2)
            return false;
        
        if (text.Length > 5000)
            return false;
        
        if (IsOnlyPunctuation(text))
            return false;
        
        if (IsOnlyNumbers(text))
            return false;
        
        return true;
    }

    private static bool IsOnlyPunctuation(string text)
    {
        return text.All(c => char.IsPunctuation(c) || char.IsWhiteSpace(c));
    }

    private static bool IsOnlyNumbers(string text)
    {
        return text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || c == '.' || c == ',');
    }
}
