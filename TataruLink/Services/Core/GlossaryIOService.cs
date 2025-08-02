// File: TataruLink/Services/Core/GlossaryIOService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements import/export functionality for the glossary in JSON and CSV formats.
/// </summary>
public class GlossaryIOService(ILogger<GlossaryIOService> logger) : IGlossaryIOService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Export(List<GlossaryEntry> glossary, GlossaryFormat format)
    {
        return format switch
        {
            GlossaryFormat.Json => JsonSerializer.Serialize(glossary, JsonOptions),
            GlossaryFormat.Csv => ExportToCsv(glossary),
            _ => throw new ArgumentOutOfRangeException(nameof(format), "Unsupported export format")
        };
    }

    public List<GlossaryEntry>? Import(string data)
    {
        // First, attempt to parse as JSON, as it's more structured.
        try
        {
            var entries = JsonSerializer.Deserialize<List<GlossaryEntry>>(data);
            if (entries != null)
            {
                logger.LogInformation("Successfully imported {count} entries from JSON.", entries.Count);
                return entries;
            }
        }
        catch (JsonException) 
        {
            logger.LogDebug("Could not parse data as JSON, falling back to CSV import.");
        }

        // If JSON fails, attempt to parse as CSV.
        try
        {
            var entries = ImportFromCsv(data);
            if (entries.Count != 0)
            {
                logger.LogInformation("Successfully imported {count} entries from CSV.", entries.Count);
                return entries;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CSV import failed unexpectedly.");
        }

        logger.LogWarning("Failed to import glossary: data was not valid JSON or CSV.");
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

        // Skip header
        var header = reader.ReadLine();
        if (header == null) return entries;

        while (reader.ReadLine() is { } line)
        {
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
    
    // This robust parser handles quotes correctly.
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            switch (c)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ',' when !inQuotes:
                    fields.Add(sb.ToString().Trim());
                    sb.Clear();
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        fields.Add(sb.ToString().Trim());
        return fields;
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            // Escape quotes by doubling them and wrap the whole field in quotes.
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
