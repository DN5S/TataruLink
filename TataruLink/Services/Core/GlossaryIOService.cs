// File: TataruLink/Services/Core/GlossaryIOService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements import/export functionality for the glossary.
/// </summary>
public class GlossaryIOService : IGlossaryIOService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Export(List<GlossaryEntry> glossary, GlossaryFormat format)
    {
        return format switch
        {
            GlossaryFormat.Json => JsonSerializer.Serialize(glossary, JsonOptions),
            GlossaryFormat.Csv => ExportToCsv(glossary),
            _ => throw new ArgumentOutOfRangeException(nameof(format), "Unsupported format")
        };
    }

    public List<GlossaryEntry>? Import(string data)
    {
        try
        {
            var entries = JsonSerializer.Deserialize<List<GlossaryEntry>>(data);
            // Basic validation: ensure it's a list and key fields are not null.
            if (entries != null && entries.All(_ => true))
            {
                return entries;
            }
        }
        catch (JsonException) { /* Fall through to CSV */ }

        try { return ImportFromCsv(data); }
        catch { /* Both failed */ }

        return null;
    }

    private static string ExportToCsv(IEnumerable<GlossaryEntry> glossary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("OriginalText,ReplacementText,IsEnabled"); // Header

        foreach (var entry in glossary)
        {
            sb.Append(EscapeCsvField(entry.OriginalText)).Append(',')
              .Append(EscapeCsvField(entry.ReplacementText)).Append(',')
              .Append(entry.IsEnabled)
              .AppendLine();
        }
        return sb.ToString();
    }

    private static List<GlossaryEntry> ImportFromCsv(string data)
    {
        var entries = new List<GlossaryEntry>();
        using var reader = new StringReader(data);

        reader.ReadLine(); // Skip header

        while (reader.ReadLine() is { } line)
        {
            // Use a more robust split method that handles quoted fields.
            var fields = ParseCsvLine(line);
            if (fields.Count != 3) continue;

            entries.Add(new GlossaryEntry
            {
                OriginalText = fields[0],
                ReplacementText = fields[1],
                IsEnabled = bool.TryParse(fields[2], out var isEnabled) && isEnabled
            });
        }
        return entries;
    }
    
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
